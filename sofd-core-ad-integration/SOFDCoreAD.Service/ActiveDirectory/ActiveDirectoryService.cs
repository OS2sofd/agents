using Serilog;
using SOFDCoreAD.Service.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace SOFDCoreAD.Service.ActiveDirectory
{
    public class ActiveDirectoryService
    {
        const int AccountDisable = 2;
        const int DontExpirePasssword = 65536;
        const string ExcludeOUsKey = "ActiveDirectory.ExcludeOUs";

        public ILogger Logger { get; set; }
        private readonly PropertyResolver propertyResolver = new PropertyResolver();
        private readonly Boolean allowMultipleUsers;
        private readonly Boolean treatDisabledAsEnabled;

        public ActiveDirectoryService()
        {
            allowMultipleUsers = Settings.GetBooleanValue("ActiveDirectory.AllowMultipleUsers");
            treatDisabledAsEnabled = Settings.GetBooleanValue("ActiveDirectory.TreatDisabledAsEnabled");
        }

        public IEnumerable<ADUser> GetFullSyncUsers(out byte[] directorySynchronizationCookie)
        {
            long maxPasswordAge = GetMaxPasswordAge();
            long lockoutDuration = GetLockoutDuration();

            Logger.Information("maxPasswordAge = " + maxPasswordAge + ", lockoutDuration = " + lockoutDuration);

            using (var directoryEntry = GenerateDirectoryEntry())
            {
                string filter = CreateFilter("!(isDeleted=TRUE)", propertyResolver.CprProperty + "=*");
                using (var directorySearcher = new DirectorySearcher(directoryEntry, filter, propertyResolver.AllProperties, SearchScope.Subtree))
                {
                    directorySearcher.DirectorySynchronization = new DirectorySynchronization(DirectorySynchronizationOptions.None);
                    var result = new List<ADUser>();
                    using (var searchResultCollection = directorySearcher.FindAll())
                    {
                        Logger.Information("Found {0} users in Active Directory", searchResultCollection.Count);

                        string excludedOUsString = Settings.GetStringValue(ExcludeOUsKey, "");
                        List<String> excludedOUs = new List<string>();
                        if (!String.IsNullOrEmpty(excludedOUsString))
                        {
                            // support & in OU names
                            excludedOUsString = excludedOUsString.Replace("&amp;", "&");
                            excludedOUs = excludedOUsString.Split(';').ToList();
                        }
                        foreach (SearchResult searchResult in searchResultCollection)
                        {
                            bool inExcludedOU = false;
                            foreach (string excludedOU in excludedOUs)
                            {
                                if (searchResult.Path.EndsWith(excludedOU))
                                {
                                    inExcludedOU = true;
                                    Logger.Information("User  " + searchResult.Path + " will not be synced, was in excluded orgUnit");
                                    break;
                                }
                            }

                            if (inExcludedOU)
                            {
                                continue;
                            }
                            
                            Logger.Verbose("Full sync searchResult: {@searchResult}", searchResult);
                            result.Add(CreateADUserFromDictionary(searchResult.Properties, maxPasswordAge, lockoutDuration));
                        }
                    }

                    directorySynchronizationCookie = directorySearcher.DirectorySynchronization.GetDirectorySynchronizationCookie();

                    if (!allowMultipleUsers)
                    {
                        RemoveDuplicateUsers(ref result);
                    }

                    return result;
                }
            }
        }

        public IEnumerable<ADUser> GetDeltaSyncUsers(ref byte[] directorySynchronizationCookie)
        {
            using (var directoryEntry = GenerateDirectoryEntry())
            {
                string filter = CreateFilter();
                using (var directorySearcher = new DirectorySearcher(directoryEntry, filter, propertyResolver.AllProperties, SearchScope.Subtree))
                {
                    directorySearcher.DirectorySynchronization = new DirectorySynchronization(DirectorySynchronizationOptions.None, directorySynchronizationCookie);
                    var result = new List<ADUser>();

                    using (var searchResults = directorySearcher.FindAll())
                    {
                        if (searchResults.Count > 0)
                        {
                            long maxPasswordAge = GetMaxPasswordAge();
                            long lockoutDuration = GetLockoutDuration();

                            string excludedOUsString = Settings.GetStringValue(ExcludeOUsKey, "");
                            List<String> excludedOUs = new List<string>();
                            if (!String.IsNullOrEmpty(excludedOUsString))
                            {
                                // support & in OU names
                                excludedOUsString = excludedOUsString.Replace("&amp;", "&");
                                excludedOUs = excludedOUsString.Split(';').ToList();
                            }
                            foreach (SearchResult searchResult in searchResults)
                            {
                                bool inExcludedOU = false;
                                foreach (string excludedOU in excludedOUs)
                                {
                                    if (searchResult.Path.EndsWith(excludedOU))
                                    {
                                        inExcludedOU = true;
                                        Logger.Information("User  " + searchResult.Path + " will not be synced, was in excluded orgUnit");
                                        break;
                                    }
                                }

                                if (inExcludedOU)
                                {
                                    continue; 
                                }

                                Logger.Verbose("Delta sync searchResult: {@searchResult}", searchResult);

                                if (searchResult.Properties.GetValue<Boolean>(propertyResolver.DeletedProperty, false))
                                {
                                    Logger.Information("Received delta sync deleted object {0}", searchResult.Path);
                                    result.Add(CreateADUserFromDictionary(searchResult.Properties, maxPasswordAge, lockoutDuration));
                                }
                                else if (searchResult.Path.Contains("CN=Deleted Objects"))
                                {
                                    Logger.Information("Object purged from AD Deleted Objects. Ignoring. {0}", searchResult.Path);
                                }
                                else
                                {
                                    Logger.Information("Received delta sync user {0}", searchResult.Path);
                                    //get properties from a full search result because the delta search result only contain the changed properties
                                    using (var entrySearcher = new DirectorySearcher(searchResult.GetDirectoryEntry(), filter, propertyResolver.AllProperties))
                                    {
                                        result.Add(CreateADUserFromDictionary(entrySearcher.FindOne().Properties, maxPasswordAge, lockoutDuration));
                                    }                                        
                                }
                            }

                            directorySynchronizationCookie = directorySearcher.DirectorySynchronization.GetDirectorySynchronizationCookie();

                            if (!allowMultipleUsers)
                            {
                                // we also need to query all users with the same cpr as the changed users to figure out which is the primary user
                                var usersWithSameCPR = new List<ADUser>();

                                foreach (var adUser in result.Where(u => !String.IsNullOrEmpty(u.Cpr)))
                                {
                                    filter = CreateFilter("!(isDeleted=TRUE)", propertyResolver.CprProperty + "=" + adUser.Cpr);
                                    using (var cprDirectorySearcher = new DirectorySearcher(directoryEntry, filter, propertyResolver.AllProperties, SearchScope.Subtree))
                                    {
                                        using (var cprResults = cprDirectorySearcher.FindAll())
                                        {
                                            foreach (SearchResult searchResult in cprResults)
                                            {
                                                usersWithSameCPR.Add(CreateADUserFromDictionary(searchResult.Properties, maxPasswordAge, lockoutDuration));
                                            }
                                        }
                                    }
                                }
                                result.AddRange(usersWithSameCPR);
                                RemoveDuplicateUsers(ref result);
                            }
                        }
                    }

                    return result;
                }
            }
        }

        private long GetMaxPasswordAge()
        {
            using (var rootEntry = GenerateDirectoryEntry())
            {
                using (var mySearcher = new DirectorySearcher(rootEntry))
                {
                    string filter = "maxPwdAge=*";
                    mySearcher.Filter = filter;
                    using (var results = mySearcher.FindAll())
                    {
                        long maxDays = 0;
                        if (results.Count >= 1)
                        {
                            Int64 maxPwdAge = (Int64)results[0].Properties["maxPwdAge"][0];
                            maxDays = maxPwdAge / -864000000000;
                        }
                        return maxDays;
                    }
                }
            }
        }

        private long GetLockoutDuration()
        {
            using (var rootEntry = GenerateDirectoryEntry())
            {
                using (var mySearcher = new DirectorySearcher(rootEntry))
                {
                    string filter = "lockoutDuration=*";
                    mySearcher.Filter = filter;
                    using (var results = mySearcher.FindAll())
                    {
                        long lockDurationInMinutes = 0;
                        if (results.Count >= 1)
                        {
                            Int64 lockoutDuration = (Int64)results[0].Properties["lockoutDuration"][0];
                            lockDurationInMinutes = lockoutDuration / -600000000;
                        }

                        return lockDurationInMinutes;
                    }
                }
            }
        }

        private DirectoryEntry GenerateDirectoryEntry()
        {
            DirectoryEntry directoryEntry = null;

            DomainControllerCollection domains = Domain.GetCurrentDomain().DomainControllers;
            foreach (DomainController controller in domains)
            {
                try
                {
                    directoryEntry = new DirectoryEntry(string.Format("LDAP://{0}", controller.Name));
                    if (directoryEntry.Properties.Count > 0) {
                        // accessing the nativeObject will throw an exception if we're not really connected to an operational DC
                        var nativeObejct = directoryEntry.NativeObject;
                        Logger.Verbose("Connected to " + controller.Name);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning("Failed to connect to " + controller.Name + ". Reason:" + ex.Message);
                }
            }

            if (directoryEntry == null)
            {
                throw new Exception("Failed to connect to AD");
            }

            if (!Settings.GetBooleanValue("ActiveDirectory.IntegratedSecurity"))
            {
                directoryEntry.Username = Settings.GetStringValue("ActiveDirectory.Username");
                directoryEntry.Password = Settings.GetStringValue("ActiveDirectory.Password");
            }

            if (Settings.GetBooleanValue("ActiveDirectory.RequireSigning"))
            {
                directoryEntry.AuthenticationType = AuthenticationTypes.Secure | AuthenticationTypes.Signing;
            }

            return directoryEntry;
        }

        [ComImport, Guid("9068270b-0939-11d1-8be1-00c04fd8d503"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
        internal interface IAdsLargeInteger
        {
            long HighPart
            {
                [SuppressUnmanagedCodeSecurity]
                get; [SuppressUnmanagedCodeSecurity]
                set;
            }

            long LowPart
            {
                [SuppressUnmanagedCodeSecurity]
                get; [SuppressUnmanagedCodeSecurity]
                set;
            }
        }


        private ADUser CreateADUserFromDictionary(IDictionary properties, long maxPasswordAge, long lockoutDuration)
        {
            var accountControlValue = properties.GetValue<Int32>(propertyResolver.UserAccountControlProperty, 0);

            var adUser = new ADUser()
            {
                ChosenName = properties.GetValue<String>(propertyResolver.ChosenNameProperty, null),
                Firstname = properties.GetValue<String>(propertyResolver.FirstnameProperty, null),
                Surname = properties.GetValue<String>(propertyResolver.SurnameProperty, null),
                Cpr = properties.GetValue<String>(propertyResolver.CprProperty, null),
                Title = properties.GetValue<String>(propertyResolver.TitleProperty, null),
                Email = properties.GetValue<String>(propertyResolver.EmailProperty, null),
                Mobile = properties.GetValue<String>(propertyResolver.MobileProperty, null),
                SecretMobile = properties.GetValue<String>(propertyResolver.SecretMobileProperty, null),
                Phone = properties.GetValue<String>(propertyResolver.PhoneProperty, null),
                DepartmentNumber = properties.GetValue<String>(propertyResolver.DepartmentNumberProperty, null),
                FaxNumber = properties.GetValue<String>(propertyResolver.FaxNumberProperty, null),
                UserId = properties.GetValue<String>(propertyResolver.UserIdProperty, null),
                EmployeeId = properties.GetValue<String>(propertyResolver.EmployeeIdProperty, null),
                Affiliation = properties.GetValue<String>(propertyResolver.AffiliationProperty, null),
                ObjectGuid = new Guid(properties.GetValue<byte[]>(propertyResolver.ObjectGuidProperty, null)).ToString(),
                Deleted = properties.GetValue<Boolean>(propertyResolver.DeletedProperty, false),
                Disabled = treatDisabledAsEnabled ? false : (accountControlValue & AccountDisable) == AccountDisable,
                UPN = properties.GetValue<String>(propertyResolver.UPNProperty, null),
                Photo = properties.GetValue<byte[]>(propertyResolver.PhotoProperty, null)
            };

            var created = properties.GetValue<DateTime?>(propertyResolver.WhenCreatedProperty, null);
            if (created != null)
            {
                adUser.WhenCreated = ((DateTime)created).ToString("yyyy-MM-dd");
            }

            if (!string.IsNullOrEmpty(propertyResolver.AltSecurityIdentitiesProperty))
            {
                var altSecIdentities = (ResultPropertyValueCollection)properties["altsecurityidentities"];
                if (altSecIdentities != null)
                {
                    string nemLoginUuid = null;

                    for (int i = 0; i < altSecIdentities.Count; i++)
                    {
                        string val = (string)altSecIdentities[i];
                        if (val == null)
                        {
                            continue;
                        }

                        if (val.StartsWith("NL3UUID-ACTIVE-NSIS"))
                        {
                            string[] tokens = val.Split('.');
                            if (tokens.Length >= 3)
                            {
                                nemLoginUuid = tokens[2];
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(nemLoginUuid))
                    {
                        adUser.MitIDUUID = nemLoginUuid;
                    }
                }
            }

            if (properties is ResultPropertyCollection)
            {
                ResultPropertyCollection rpc = (ResultPropertyCollection)properties;

                if (adUser.Deleted)
                {
                    // do not check expiry for deleted accounts
                    adUser.AccountExpireDate = null;
                }
                else
                {
                    long largeInt = (long)rpc[propertyResolver.AccountExpireProperty][0];
                    if (largeInt > 0 && largeInt < long.MaxValue)
                    {
                        var dateTime = DateTime.FromFileTime(largeInt);

                        adUser.AccountExpireDate = dateTime.ToString("yyyy-MM-dd");
                    }
                    else
                    {
                        adUser.AccountExpireDate = "9999-12-31";
                    }

                    if (lockoutDuration > 0)
                    {
                        if (rpc[propertyResolver.LockoutTimeProperty] != null && rpc[propertyResolver.LockoutTimeProperty].Count > 0)
                        {
                            largeInt = (long)rpc[propertyResolver.LockoutTimeProperty][0];
                            if (largeInt > 0 && largeInt < long.MaxValue)
                            {
                                // it seems the lockoutDuration is a VERY large number if it is permanent (so we check for 1 year)
                                if (lockoutDuration < (60 * 24 * 365))
                                {
                                    var dateTime = DateTime.FromFileTime(largeInt).AddMinutes(lockoutDuration);

                                    if (DateTime.Now.ToUniversalTime() < dateTime)
                                    {
                                        adUser.PasswordLocked = true;
                                    }
                                }
                                else
                                {
                                    adUser.PasswordLocked = true;
                                }
                            }
                        }
                    }
                }
            }
            else if (properties is PropertyCollection)
            {
                PropertyCollection pc = (PropertyCollection)properties;

                if (adUser.Deleted)
                {
                    // do not check expiry for deleted accounts
                    adUser.AccountExpireDate = null;
                }
                else
                {
                    // expire
                    var largeInt = (IAdsLargeInteger)pc[propertyResolver.AccountExpireProperty].Value;
                    var datelong = (largeInt.HighPart << 32) + largeInt.LowPart;

                    if (datelong > 0 && datelong < long.MaxValue)
                    {
                        var dateTime = DateTime.FromFileTime(datelong);

                        adUser.AccountExpireDate = dateTime.ToString("yyyy-MM-dd");
                    }
                    else
                    {
                        adUser.AccountExpireDate = "9999-12-31";
                    }

                    // lockout
                    if (lockoutDuration > 0 && pc[propertyResolver.LockoutTimeProperty]?.Value != null )
                    {
                        largeInt = (IAdsLargeInteger)pc[propertyResolver.LockoutTimeProperty].Value;
                        datelong = (largeInt.HighPart << 32) + largeInt.LowPart;

                        if (datelong > 0 && datelong < long.MaxValue)
                        {
                            // it seems the lockoutDuration is a VERY large number if it is permanent (so we check for 1 year)
                            if (lockoutDuration < (60 * 24 * 365))
                            {
                                var dateTime = DateTime.FromFileTime(datelong).AddMinutes(lockoutDuration);

                                if (DateTime.Now.ToUniversalTime() < dateTime)
                                {
                                    adUser.PasswordLocked = true;
                                }
                            }
                            else
                            {
                                adUser.PasswordLocked = true;
                            }
                        }
                    }
                }
            }

            if (accountControlValue == 0 && adUser.Deleted == false)
            {
                Logger.Error("userAccountControl 0 for user {0}", adUser.UserId);
            }

            adUser.DaysToPwdChange = GetDaysToPwdChange(properties, accountControlValue, maxPasswordAge);

            foreach (var localExtentionProperty in propertyResolver.LocalExtentionProperties)
            {
                var localExtentionValue = properties.GetValue<String>(localExtentionProperty.Value, null);
                if (localExtentionValue != null)
                {
                    adUser.LocalExtensions.Add(localExtentionProperty.Key, localExtentionValue);
                }
            }

            return adUser;
        }

        // 100000  : never expires (basically just a very high number, which the backend can store)
        // <= 0    : must be changed on next login
        // > 0     : still X days to password change is required
        private long GetDaysToPwdChange(IDictionary properties, int accountControlValue, long maxPasswordAge)
        {
            long daysLeft = 100000;

            if ((accountControlValue & DontExpirePasssword) != DontExpirePasssword)
            {
                if (maxPasswordAge > 0)
                {
                    long lastChanged = properties.GetValue<long>(propertyResolver.PwdLastSetProperty, -1);

                    if (lastChanged != -1)
                    {
                        daysLeft = maxPasswordAge - DateTime.Today.Date.Subtract(DateTime.FromFileTime(lastChanged).Date).Days;
                    }
                }
            }
            return daysLeft;
        }

        private string CreateFilter(params string[] filters)
        {
            var allFilters = filters.ToList();
            allFilters.Add("objectClass=user");
            allFilters.Add("!(objectclass=computer)");
            allFilters.Add(Settings.GetStringValue("ActiveDirectory.Filter"));

            return string.Format("(&{0})", string.Concat(allFilters.Where(x => !String.IsNullOrEmpty(x)).Select(x => '(' + x + ')').ToArray()));
        }

        private void RemoveDuplicateUsers(ref List<ADUser> adUsers)
        {
            var distinctUserList = new Dictionary<string, ADUser>();

            foreach (ADUser adUser in adUsers)
            {
                // users with empty cpr (deleted cpr) is not considered duplicate
                if (String.IsNullOrEmpty(adUser.Cpr))
                {
                    distinctUserList.Add(Guid.NewGuid().ToString(), adUser);
                }
                else
                {
                    if (!(distinctUserList.ContainsKey(adUser.Cpr) && distinctUserList[adUser.Cpr].IsHigherPriority(adUser)))
                    {
                        distinctUserList[adUser.Cpr] = adUser;
                    }
                }
            }

            adUsers = distinctUserList.Values.ToList();
        }
    }
}
