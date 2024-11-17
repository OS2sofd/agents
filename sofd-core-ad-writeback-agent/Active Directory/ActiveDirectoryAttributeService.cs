using System;
using System.DirectoryServices;
using SOFD_Core.Model;
using Serilog;
using System.Linq;
using System.Collections.Generic;

namespace Active_Directory
{
    public class ActiveDirectoryAttributeService : IDisposable
    {
        protected ILogger log;
        protected ActiveDirectoryConfig config;
        private PowershellService powershellService;

        public ActiveDirectoryAttributeService(ActiveDirectoryConfig config, ILogger log)
        {
            this.config = config;
            this.log = log;
            if (config.EnablePowershell)
            {
                powershellService = new PowershellService(log);
            }
        }

        public void Dispose()
        {
            if (powershellService != null)
            {
                powershellService.Dispose();
            }            
        }

        public void UpdatePerson(Person person, List<OrgUnit> orgUnits, string updateUserType, bool updateManagersEnabled, bool managerNoClear)
        {
            if (person.users == null || person.deleted)
            {
                return;
            }

            Affiliation primeAffiliation = null;
            if (person.affiliations != null && person.affiliations.Count > 0)
            {
                foreach (Affiliation affiliation in person.affiliations)
                {
                    if (affiliation.prime)
                    {
                        primeAffiliation = affiliation;
                        break;
                    }
                }
            }

            foreach (User user in person.users)
            {
                try
                {
                    // only update AD accounts
                    if (!user.userType.Equals(updateUserType))
                    {
                        continue;
                    }

                    var filter = string.Format("(&(objectClass=user)(objectClass=person)(sAMAccountName={0}))", user.userId);

                    // find an affiliation that matches the employeeId on the user account
                    Affiliation affiliation = null;
                    if (!string.IsNullOrEmpty(user.employeeId) && person.affiliations != null && person.affiliations.Count > 0)
                    {
                        foreach (Affiliation aff in person.affiliations)
                        {
                            if (string.Equals(aff.employeeId, user.employeeId))
                            {
                                affiliation = aff;
                                break;
                            }
                        }
                    }

                    // fallback to prime affiliation if enabled (note that primeAffiliation also might be null ;))
                    if (affiliation == null && config.EnableFallbackToPrimeAffiliation)
                    {
                        affiliation = primeAffiliation;
                    }

                    // find orgUnit to map data from
                    OrgUnit orgUnit = null;
                    if (affiliation != null && orgUnits != null && orgUnits.Count > 0)
                    {
                        foreach (var ou in orgUnits) 
                        {
                            if (string.Equals(affiliation.calculatedOrgUnitUuid, ou.uuid))
                            {
                                orgUnit = ou;
                                break;
                            }
                        }
                    }

                    using (DirectoryEntry entry = new DirectoryEntry())
                    {
                        using (DirectorySearcher search = new DirectorySearcher(entry))
                        {
                            search.Filter = filter;

                            // Fetch from AD only the properties we have in mapping for
                            foreach (var item in config.map)
                            {
                                var key = GetKey(item.Key);

                                search.PropertiesToLoad.Add(key);
                            }

                            if (updateManagersEnabled)
                            {
                                search.PropertiesToLoad.Add("manager");
                            }
                            search.PropertiesToLoad.Add("distinguishedName");

                            SearchResult result = search.FindOne();

                            if (result != null)
                            {
                                using (var de = result.GetDirectoryEntry())
                                {
                                    if (de.Properties == null)
                                    {
                                        log.Warning("de.Properties is null for: " + user.userId);
                                        continue;
                                    }

                                    if (config.ActiveDirectoryWritebackExcludeOUs.Count > 0)
                                    {
                                        using (var userOU = de.Parent)
                                        {
                                            var ouDN = (string)userOU.Properties["distinguishedName"].Value;
                                            if (config.ActiveDirectoryWritebackExcludeOUs.Any(ex => ouDN.Contains(ex.Trim())))
                                            {
                                                log.Debug($"Skipping writeback for {user.userId} because user is in excluded OU");
                                                continue;
                                            }
                                        }
                                    }

                                    string newCn = null;
                                    bool changes = false;

                                    foreach (var item in config.map)
                                    {
                                        try
                                        {
                                            var key = GetKey(item.Key);
                                            string adValue = de.Properties.Contains(key) ? de.Properties[key].Value.ToString() : null;
                                            string sofdValue = FieldMapper.GetValue(user.userId, item.Value, person, affiliation, orgUnit);

                                            // Handle special case: initials can only take 6 chars, so skip if more than 7 chars
                                            if ("initials".Equals(key, StringComparison.InvariantCultureIgnoreCase))
                                            {
                                                if (sofdValue != null && sofdValue.Length > 6)
                                                {
                                                    log.Debug("Skipping initials for " + user.userId + " because length > 6");
                                                    continue;
                                                }
                                            }

                                            // Handle special case: accountExpires - data needs to be processed first
                                            if ("accountExpires".Equals(key, StringComparison.InvariantCultureIgnoreCase))
                                            {
                                                adValue = result.Properties.Contains(key) ? ((Int64)result.Properties[key][0]).ToString() : null;
                                                if (affiliation == null)
                                                {
                                                    // if there is no affiliation found, we do not want to update accountExpires in AD, as this would open op the account again.
                                                    continue;
                                                }
                                                DateTime accountExpires;
                                                if (DateTime.TryParse(sofdValue, out accountExpires))
                                                {
                                                    // we expect the input to be affiliation stop date or similar date
                                                    // but we do not want the account to expire on the last day of work, so we add 1 day
                                                    accountExpires = new DateTime(accountExpires.Year, accountExpires.Month, accountExpires.Day).AddDays(1);
                                                    // accountExpires expects the number of 100-nanosecond intervals since January 1, 1601 (UTC) 
                                                    // we can get this using ToFileTime
                                                    sofdValue = Convert.ToString((Int64)accountExpires.ToFileTime());
                                                }
                                                else
                                                {
                                                    // A value of 0 means that the account never expires
                                                    sofdValue = "0";
                                                }
                                            }

                                            // handle special case for commonName
                                            if (!"cn".Equals(key))
                                            {

                                                if (string.IsNullOrEmpty(sofdValue))
                                                {
                                                    if (!string.IsNullOrEmpty(adValue) && ShouldClear(item.Key) && ShouldReplace(item.Key))
                                                    {
                                                        log.Information("Clearing attribute for " + user.userId + ": " + key);
                                                        de.Properties[key].Clear();
                                                        changes = true;
                                                    }
                                                }
                                                // update if values differ, but do not replace values in AD if NOREPLACE is used
                                                else if (!sofdValue.Equals(adValue) && (String.IsNullOrEmpty(adValue) || ShouldReplace(item.Key) ))
                                                {
                                                    log.Information("Setting attribute for " + user.userId + ": " + key + "=" + sofdValue);
                                                    de.Properties[key].Value = sofdValue;
                                                    changes = true;
                                                }
                                            }
                                            else
                                            {
                                                // if we have a new name for the user, store it here
                                                if (!string.IsNullOrEmpty(sofdValue) && !object.Equals(sofdValue, adValue))
                                                {
                                                    log.Information("Renaming " + user.userId + " to " + sofdValue);
                                                    newCn = sofdValue;
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            log.Error(ex, "Property mapping \"" + item.Key + "\" is incorrect for: " + user.userId);

                                            return;
                                        }
                                    }

                                    if (updateManagersEnabled && !user.managerUpdateExcluded)
                                    {
                                        try
                                        {
                                            string currentManagerDN = de.Properties.Contains("manager") ? de.Properties["manager"].Value.ToString() : null;
                                            string actualManagerDN = null;

                                            string managerADAccountName = user.managerADAccountName;
                                            if (string.IsNullOrEmpty(managerADAccountName))
                                            {
                                                if (!string.IsNullOrEmpty(currentManagerDN) && !managerNoClear)
                                                {
                                                    log.Information($"Clearing attribute for {user.userId}: manager");
                                                    de.Properties["manager"].Clear();
                                                    changes = true;
                                                }
                                            }
                                            else
                                            {
                                                // lookup manager in AD to get DN from the accountname
                                                using (DirectoryEntry managerEntry = new DirectoryEntry())
                                                {
                                                    using (DirectorySearcher managerSearch = new DirectorySearcher(managerEntry))
                                                    {
                                                        managerSearch.Filter = $"(SAMAccountName={managerADAccountName})";
                                                        SearchResult managerResult = managerSearch.FindOne();
                                                        if (managerResult == null)
                                                        {
                                                            log.Warning($"Could not lookup manager in AD. Filter={managerSearch.Filter}");
                                                        }
                                                        else
                                                        {
                                                            using (var managerDE = managerResult.GetDirectoryEntry())
                                                            {
                                                                actualManagerDN = managerDE.Properties["distinguishedName"].Value.ToString();
                                                            }
                                                        }
                                                    }
                                                }

                                                if (!string.Equals(currentManagerDN, actualManagerDN))
                                                {
                                                    log.Information($"Setting attribute for {user.userId}: manager={actualManagerDN}");
                                                    de.Properties["manager"].Value = actualManagerDN;
                                                    changes = true;
                                                }
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            log.Error(e, $"Failed to set manager for {user.userId}");
                                        }
                                    }

                                    if (changes)
                                    {
                                        de.CommitChanges();
                                    }

                                    // has to happen after CommitChanges, otherwise changes will not be committed
                                    if (newCn != null)
                                    {
                                        de.Rename("cn=" + newCn);
                                    }
                                    // invoke custom powershell if any change was made
                                    if (config.EnablePowershell && changes)
                                    {
                                        powershellService.UserChanged(person, user, affiliation, orgUnit);
                                    }
                                }                                
                            }
                            else
                            {
                                log.Warning("Could not find user with sAMAccountName: " + user.userId);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ex is UnauthorizedAccessException)
                    {
                        Log.Warning("UnauthorizedAccessException when updating " + user?.userId + " : " + ex.Message);
                    }
                    else
                    {
                        log.Error("Failed to connect to AD for user: '" + user?.userId + "'");
                        throw ex;
                    }
                }
            }
        }

        private string GetKey(string key)
        {
            if (key.StartsWith("NOCLEAR("))
            {
                return key.Substring(8, key.Length - 9);
            }
            else if (key.StartsWith("NOREPLACE("))
            {
                return key.Substring(10, key.Length - 11);
            }
            return key;
        }

        private bool ShouldClear(string key)
        {
            if (key.StartsWith("NOCLEAR("))
            {
                return false;
            }            
            return true;
        }

        private bool ShouldReplace(string key)
        {
            if (key.StartsWith("NOREPLACE("))
            {
                return false;
            }

            return true;
        }

    }
}
