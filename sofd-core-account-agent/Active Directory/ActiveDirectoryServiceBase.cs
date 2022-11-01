﻿using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using Serilog;
using System.Linq;

namespace Active_Directory
{
    abstract public class ActiveDirectoryServiceBase
    {
        protected ILogger log;
        protected ActiveDirectoryConfig config;

        public ActiveDirectoryServiceBase(ActiveDirectoryConfig config, ILogger log)
        {
            this.config = config;
            this.log = log;
        }

        protected DirectoryEntryWrapper GetBySAMAccountName(string sAMAccountName)
        {
            var filter = string.Format("(&(objectClass=user)(objectClass=person)(sAMAccountName={0}))", sAMAccountName);

            var wrapper = GenerateDirectoryEntry();
            using (DirectoryEntry entry = wrapper.Entry)
            {
                using (DirectorySearcher search = new DirectorySearcher(entry))
                {
                    search.Filter = filter;
                    search.PropertiesToLoad.Add("userAccountControl");
                    search.PropertiesToLoad.Add("sAMAccountName");
                    search.PropertiesToLoad.Add("mail");
                    search.PropertiesToLoad.Add("userprincipalname");
                    search.PropertiesToLoad.Add("mailnickname");

                    var result = search.FindOne();
                    if (result != null)
                    {
                        var res = new DirectoryEntryWrapper();
                        res.DC = wrapper.DC;
                        res.Entry = result.GetDirectoryEntry();

                        return res;
                    }
                }
            }

            return null;
        }

        protected AccountStatus GetAccountStatusBySAMAccountName(string sAMAccountName)
        {
            var filter = string.Format("(&(objectClass=user)(objectClass=person)(sAMAccountName={0}))", sAMAccountName);

            var result = GetAccountStatus(filter);

            if (result.Count > 0)
            {
                return result[0];
            }

            return null;
        }

        protected List<AccountStatus> GetAccountStatiByCpr(string cpr)
        {
            var filter = string.Format("(&(objectClass=user)(objectClass=person)({0}={1}))", config.attributeCpr, cpr);

            return GetAccountStatus(filter);
        }

        protected List<AccountStatus> GetAccountStatus(string filter)
        {
            var stati = new List<AccountStatus>();

            var wrapper = GenerateDirectoryEntry();
            using (DirectoryEntry entry = wrapper.Entry)
            {
                using (DirectorySearcher search = new DirectorySearcher(entry))
                {
                    search.Filter = filter;
                    search.PropertiesToLoad.Add("userAccountControl");
                    search.PropertiesToLoad.Add("sAMAccountName");
                    search.PropertiesToLoad.Add("accountExpires");
                    if (!string.IsNullOrEmpty(config.attributeEmployeeId))
                    {
                        search.PropertiesToLoad.Add(config.attributeEmployeeId);
                    }

                    using (var searchResult = search.FindAll())
                    {
                        for (int counter = 0; counter < searchResult.Count; counter++)
                        {
                            var result = searchResult[counter];

                            // ignore existing user if it is in an excluded OU
                            if( config.existingAccountExcludeOUs.Any(excludedOU => !String.IsNullOrEmpty(excludedOU) && result.Path.ToLower().EndsWith( excludedOU.ToLower().Trim())) )
                            {
                                log.Debug($"Ignoring existing user {result.Path} because it is in an excluded OU");
                                continue;
                            }

                            var de = result.GetDirectoryEntry();

                            int flags = (int) de.Properties["useraccountcontrol"].Value;
                            string sAMAccountName = (string) de.Properties["samaccountname"].Value;
                            string employeeId = null;
                            if (!string.IsNullOrEmpty(config.attributeEmployeeId))
                            {
                                employeeId = (string)de.Properties[config.attributeEmployeeId].Value;
                            }

                            AccountStatus status = new AccountStatus();
                            status.disabled = Convert.ToBoolean(flags & 0x0002);
                            status.sAMAccountName = sAMAccountName;
                            status.employeeId = employeeId;

                            // check if the account is expired
                            if (!status.disabled && result.Properties.Contains("accountExpires"))
                            {
                                try
                                {
                                    Int64 expiry = (Int64)result.Properties["accountExpires"][0];
                                    if (expiry != 0 && expiry != 9223372036854775807) // magic markers for not-set
                                    {
                                        DateTime expires = DateTime.FromFileTime(expiry);
                                        if (expires.CompareTo(DateTime.Now) < 0)
                                        {
                                            status.disabled = true;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    log.Warning("Failed to read accountExpires on: " + sAMAccountName, ex);
                                }
                            }

                            stati.Add(status);
                        }
                    }
                }
            }

            return stati;
        }

        protected DirectoryEntryWrapper GenerateDirectoryEntry()
        {
            DomainControllerCollection domains = Domain.GetCurrentDomain().DomainControllers;
            foreach (DomainController controller in domains)
            {
                try
                {
                    var dc = controller.Name;
                    var directoryEntry = new DirectoryEntry(string.Format("LDAP://{0}", dc));

                    log.Verbose("Connected to " + controller.Name);

                    DirectoryEntryWrapper result = new DirectoryEntryWrapper();
                    result.Entry = directoryEntry;
                    result.DC = dc;

                    return result;
                }
                catch (Exception ex)
                {
                    log.Warning("Failed to connect to " + controller.Name + ". Reason:" + ex.Message);
                }
            }

            throw new Exception("Failed to connect to AD");
        }
    }
}
