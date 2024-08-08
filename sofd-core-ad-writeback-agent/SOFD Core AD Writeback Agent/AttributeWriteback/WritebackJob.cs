using Active_Directory;
using Quartz;
using Serilog;
using SOFD.PAM;
using SOFD_Core;
using SOFD_Core.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Xml;

namespace SOFD
{
    [DisallowConcurrentExecution]
    public class WritebackJob : IJob
    {
        private static ILogger adLogger = new LoggerConfiguration().ReadFrom.AppSettings().CreateLogger().ForContext(typeof(ActiveDirectoryAttributeService));
        private static ILogger log = new LoggerConfiguration().ReadFrom.AppSettings().CreateLogger().ForContext(typeof(WritebackJob));
        private static string updateUserType = Properties.Settings.Default.ActiveDirectoryUserType;
        private static string activeDirectoryWritebackExcludeOUs = Properties.Settings.Default.ActiveDirectoryWritebackExcludeOUs;

        private SOFDOrganizationService organizationService;

        public WritebackJob()
        {
            this.organizationService = new SOFDOrganizationService(Properties.Settings.Default.SofdUrl, PAMService.GetApiKey());
        }

        public void Execute(IJobExecutionContext context)
        {
            try
            {
                List<Person> persons = new List<Person>();
                List<OrgUnit> orgUnits = new List<OrgUnit>();
                var updateManagersEnabled = false; // we only update managers on full sync
                var managerNoClear = Properties.Settings.Default.ActiveDirectoryManagerUpdateNoClear;
                if (SOFDOrganizationService.fullSync)
                {
                    log.Information("Running full synchronization");
                    updateManagersEnabled = Properties.Settings.Default.ActiveDirectoryEnableManagerUpdate;
                    organizationService.MoveToHead();
                    orgUnits = organizationService.GetOrgUnits();
                    log.Information($"Found {orgUnits.Count} OrgUnits in SOFD");
                    persons = organizationService.GetPersons();
                    log.Information($"Found {persons.Count} Persons in SOFD");
                    if (updateManagersEnabled)
                    {
                        SetPersonManager(persons, orgUnits);
                    }
                    SOFDOrganizationService.fullSync = false;
                }
                else
                {
                    var deltaSyncPerson = organizationService.GetDeltaSyncPersons();

                    if (deltaSyncPerson.uuids.Count > 0)
                    {
                        List<string> uuids = deltaSyncPerson.uuids.Select(c => c.uuid).Distinct().ToList();

                        foreach (string uuid in uuids)
                        {
                            Person person = organizationService.GetPerson(uuid);

                            if (person != null)
                            {
                                persons.Add(person);
                            }
                            else
                            {
                                log.Warning("Could not find a person in SOFD with uuid = " + uuid);
                            }
                        }
                    }

                    // move offset
                    organizationService.setPersonOffset(deltaSyncPerson.offset);
                }

                UpdateAttributes(persons, orgUnits, updateManagersEnabled,managerNoClear);
            }
            catch (Exception ex)
            {
                log.Error(ex, "Failed to update attributes");
            }
        }

        private void SetPersonManager(List<Person> persons, List<OrgUnit> orgUnits)
        {
            // Set each persons manager to the primary ad user account of the manager of the primary affiliation
            foreach (var person in persons)
            {
                if (person.users != null)
                {
                    foreach (var user in person.users)
                    {
                        if (!user.userType.Equals(updateUserType))
                        {
                            continue;
                        }


                        // skip if user is not prime, unless explicity allowed in configuration
                        // (some municipalities do not want managers on secondary user objects because they show up in the "Outlook teams" feature)
                        if (Properties.Settings.Default.ActiveDirectoryManagerUpdateOnlyPrimes && !user.prime)
                        {
                            continue;
                        }

                        try
                        {
                            var primeAffiliation = person.affiliations.FirstOrDefault(a => a.prime);
                            if (primeAffiliation == null)
                            {
                                continue;
                            }

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

                            // fallback to prime affiliation
                            if (affiliation == null)
                            {
                                affiliation = primeAffiliation;
                            }

                            // check if affiliation belongs to a master that we update masters for
                            if (!string.IsNullOrEmpty(Properties.Settings.Default.ActiveDirectoryManagerUpdateMasters))
                            {
                                var validMasters = Properties.Settings.Default.ActiveDirectoryManagerUpdateMasters.Split(';');
                                if (!validMasters.Contains(affiliation.master))
                                {
                                    log.Debug($"Not setting manager for {person.name} because affiliation master {affiliation.master} is not a valid master for manager update.");
                                    continue;
                                }
                            }

                            // check if affiliation belongs an excluded orgunit
                            if (!string.IsNullOrEmpty(Properties.Settings.Default.ActiveDirectoryManagerUpdateExcludedOrgunits))
                            {
                                var excludedOrgUnitUuids = Properties.Settings.Default.ActiveDirectoryManagerUpdateExcludedOrgunits.Split(';').ToList();
                                excludedOrgUnitUuids = GetChildOrgUnitUuidsRecursive(orgUnits, excludedOrgUnitUuids);

                                if (excludedOrgUnitUuids.Contains(affiliation.calculatedOrgUnitUuid.ToString()))
                                {
                                    log.Debug($"Not setting manager for {person.name} because affiliation orgunit is excluded from manager updates");
                                    user.managerUpdateExcluded = true;
                                    continue;
                                }
                            }


                            var orgUnit = orgUnits.First(o => Object.Equals(o.uuid, affiliation.calculatedOrgUnitUuid));
                            var managerPersonUuid = orgUnit.manager.uuid;


                            // if the manager is the same as the person (it is for all managers), then set the manager to the parent manager
                            var currentOrgUnit = orgUnit;
                            while(string.Equals(person.uuid.ToString(), managerPersonUuid) && currentOrgUnit.parentUuid != null )
                            {
                                var parentOrgUnit = orgUnits.First(o => string.Equals(o.uuid.ToString(), currentOrgUnit.parentUuid));
                                managerPersonUuid =  parentOrgUnit?.manager?.uuid;
                                currentOrgUnit = parentOrgUnit;
                            }

                            var manager = persons.First(p => p.uuid.ToString() == managerPersonUuid);
                            var managerADUser = manager.users.FirstOrDefault(u => u.prime && u.userType == "ACTIVE_DIRECTORY");
                            if (managerADUser != null)
                            {
                                user.managerADAccountName = managerADUser.userId;
                            }
                        }
                        catch (Exception)
                        {
                            log.Debug($"Failed to find manager for person {person.name} ({person.uuid}) for {user.userId}");
                        }
                    }
                }
            }
        }

        private List<string> GetChildOrgUnitUuidsRecursive(List<OrgUnit> orgUnits, List<string> excludedOrgUnitUuids)
        {
            var result = new List<string>();
            result.AddRange(excludedOrgUnitUuids);
            foreach (var uuid in excludedOrgUnitUuids)
            {
                var children = orgUnits.Where(o => o.parentUuid == uuid).Select(o => o.uuid).ToList();
                result.AddRange(GetChildOrgUnitUuidsRecursive(orgUnits, children));
            }
            return result.Distinct().ToList();
        }

        private void UpdateAttributes(List<Person> persons, List<OrgUnit> orgUnits, bool updateManagersEnabled, bool managerNoClear)
        {
            var adMappingList = LoadMappingList("AttributeWriteback/ad-mapping.xml");

            var activeDirectoryWritebackExcludeOUsList = new List<string>();
            if (!string.IsNullOrEmpty(activeDirectoryWritebackExcludeOUs))
            {
                activeDirectoryWritebackExcludeOUsList.AddRange(activeDirectoryWritebackExcludeOUs.Split(';'));
            }

            ActiveDirectoryAttributeService activeDirectoryService = new ActiveDirectoryAttributeService(new ActiveDirectoryConfig()
            {
                map = adMappingList,
                ActiveDirectoryWritebackExcludeOUs = activeDirectoryWritebackExcludeOUsList,
                EnablePowershell = Properties.Settings.Default.ActiveDirectoryEnablePowershell,
                EnableFallbackToPrimeAffiliation = Properties.Settings.Default.EnableFallbackToPrimeAffiliation
            }, adLogger);

            bool orgUnitRequired = OrgUnitRequired(adMappingList.Values);
            bool orgUnitParentRequired = OrgUnitParentRequired(adMappingList.Values);

            // DEBUG: only return this user
            // persons = persons.Where(p => p.users.Any(u => u.userId == "usernamehere")).ToList();

            if (persons.Count > 0)
            {
                log.Information("Checking " + persons.Count + " persons");

                foreach (var person in persons)
                {
                    List<string> relevantOrgUnitUuids = new List<string>();
                    bool foundADAccount = false;

                    foreach (User user in person.users)
                    {
                        if (user.userType.Equals(updateUserType))
                        {
                            foundADAccount = true;

                            // check for relevancy in affiliations
                            if (!string.IsNullOrEmpty(user.employeeId) && person.affiliations != null && person.affiliations.Count > 0)
                            {
                                foreach (Affiliation a in person.affiliations)
                                {
                                    if (string.Equals(user.employeeId, a.employeeId))
                                    {
                                        relevantOrgUnitUuids.Add(a.calculatedOrgUnitUuid.ToString());
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (!foundADAccount)
                    {
                        continue;
                    }

                    List<OrgUnit> relevantOrgUnits = new List<OrgUnit>();
                    if (orgUnitRequired && person.affiliations != null && person.affiliations.Count > 0)
                    {
                        foreach (Affiliation a in person.affiliations)
                        {
                            // we care about relevant affiliations and prime affilations
                            if (!a.prime && !relevantOrgUnitUuids.Contains(a.calculatedOrgUnitUuid.ToString()))
                            {
                                continue;
                            }

                            // OrgUnit without parent
                            OrgUnit orgUnit = GetOrgUnit(orgUnits, a.calculatedOrgUnitUuid.ToString());
                            if (orgUnit == null)
                            {
                                continue;
                            }

                            relevantOrgUnits.Add(orgUnit);

                            // check if we need ou with parent
                            if (orgUnitParentRequired)
                            {
                                // declare pointers
                                string parentUUID = orgUnit.parentUuid;
                                OrgUnit ou = orgUnit;

                                while (parentUUID != null)
                                {
                                    ou.parent = GetOrgUnit(orgUnits, parentUUID);

                                    ou = ou.parent;
                                    parentUUID = ou.parentUuid;
                                }
                            }
                        }
                    }

                    try
                    {
                        activeDirectoryService.UpdatePerson(person, relevantOrgUnits, updateUserType, updateManagersEnabled, managerNoClear);
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex, "Failed to update attributes on " + person.uuid);
                    }
                }
                log.Information("Done updating AD");
            }
            activeDirectoryService.Dispose();
        }

        private OrgUnit GetOrgUnit(List<OrgUnit> orgUnits, string uuid)
        {
            foreach (var orgUnit in orgUnits)
            {
                if (orgUnit.uuid.ToString().Equals(uuid))
                {
                    return orgUnit;
                }
            }

            // fallback to calling the service
            return organizationService.GetOrgUnit(uuid);
        }

        private bool OrgUnitRequired(Dictionary<string, string>.ValueCollection values)
        {
            // orgUnit info is part of the dto that is sent to custom powershell.
            // so if custom powershell is enabled, orgunits are always required
            if (Properties.Settings.Default.ActiveDirectoryEnablePowershell)
            {
                return true;
            }
            // otherwise orgunits are only required if they are part of the mapping xml
            foreach (string key in values)
            {
                if (key.Contains("orgUnit"))
                {
                    return true;
                }
            }

            return false;
        }

        private bool OrgUnitParentRequired(Dictionary<string, string>.ValueCollection values)
        {
            foreach (string key in values)
            {
                string[] tokens = key.Split('.');

                if (tokens.Any(t => t.Contains("^") || t.Contains(">") || t.Contains("_")))
                {
                    return true;
                }
                if (tokens.Length >= 2 && tokens[1].Equals("orgUnit") && tokens[2].StartsWith("tag[") && tokens[2].Contains("true"))
                {
                    return true;
                }
                if (tokens.Length >= 2 && tokens[1].Equals("orgUnit") && (tokens[2] == "parent"))
                {
                    return true;
                }
            }

            return false;
        }

        private Dictionary<string, string> LoadMappingList(string fileName)
        {
            Dictionary<string, string> mappingList = new Dictionary<string, string>();
            XmlDocument xDoc = new XmlDocument();
            xDoc.Load(fileName);
            XmlNodeList mappings = xDoc.GetElementsByTagName("mapping");

            foreach (XmlNode mapping in mappings)
            {
                string property = mapping.Attributes["sofd"].Value;
                log.Debug("SOFD field: " + property + " is mapped to AD field: " + mapping.Attributes["ad"].Value);

                try
                {
                    mappingList.Add(mapping.Attributes["ad"].Value, mapping.Attributes["sofd"].Value);
                }
                catch (Exception innerException)
                {
                    throw new SystemException("Property mapping \"" + property + "\" is invalid.", innerException);
                }
            }

            return mappingList;
        }
    }
}