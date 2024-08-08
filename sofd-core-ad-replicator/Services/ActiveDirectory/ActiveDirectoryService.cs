using Microsoft.Extensions.Logging;
using sofd_core_ad_replicator.Services.Sofd;
using sofd_core_ad_replicator.Services.Sofd.Model;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Text.RegularExpressions;
using Unidecode.NET;

namespace sofd_core_ad_replicator.Services.ActiveDirectory
{
    internal class ActiveDirectoryService : ServiceBase<ActiveDirectoryService>
    {
        private readonly string rootOU; //deprecated
        private readonly string oUIdField;
        private readonly string rootDeletedOusOu;
        private readonly string userIdField = "sAMAccountName";
        private readonly string eanField;
        private readonly bool eanFieldInherit;
        private readonly string losIdField;
        private readonly string streetAddressField;
        private readonly string cityField;
        private readonly string postalCodeField;
        private readonly string rootOrgUnitUuid; //deprecated
        private readonly bool dryRunMoveUsers;
        private readonly Dictionary<string, string> branchRootMap;

        public ActiveDirectoryService(IServiceProvider sp) : base(sp)
        {
            rootOU = settings.ActiveDirectorySettings.RootOU; //deprecated
            oUIdField = settings.ActiveDirectorySettings.RequiredOUFields.OUIdField;
            rootDeletedOusOu = settings.ActiveDirectorySettings.RootDeletedOusOu;
            eanField = settings.ActiveDirectorySettings.OptionalOUFields.EanField;
            eanFieldInherit = settings.ActiveDirectorySettings.OptionalOUFields.EanFieldInherit;
            streetAddressField = settings.ActiveDirectorySettings.OptionalOUFields.StreetAddressField;
            cityField = settings.ActiveDirectorySettings.OptionalOUFields.CityField;
            postalCodeField = settings.ActiveDirectorySettings.OptionalOUFields.PostalCodeField;
            rootOrgUnitUuid = settings.SofdSettings.RootOrgUnitUuid; //deprecated
            dryRunMoveUsers = settings.ActiveDirectorySettings.DryRunMoveUsers;
            losIdField = settings.ActiveDirectorySettings.OptionalOUFields.LosIDField;
            branchRootMap = settings.SofdSettings.SOFDToADOrgUnitMap ?? new();
        }

        public Dictionary<string, string> HandleOrgUnits(List<OrgUnit> allOrgUnits)
        {
            Dictionary<string, string> orgUnitMap = new Dictionary<string, string>();

            // Backwards compatible if using old setting(rootOU) we add it to mapping dictionary
            if (!string.IsNullOrEmpty(rootOU))
            {
                if (string.IsNullOrEmpty(rootOrgUnitUuid))
                {
                    var rootUuid = allOrgUnits.Where(o => o.ParentUuid == null).First().Uuid;

                    if (!branchRootMap.ContainsKey(rootUuid))
                    {
                        branchRootMap.Add(rootUuid, rootOU);
                    }
                }
                else
                {
                    if (!branchRootMap.ContainsKey(rootOrgUnitUuid))
                    {
                        branchRootMap.Add(rootOrgUnitUuid, rootOU);
                    }
                }
            }

            foreach (var entry in branchRootMap)
            {
                OrgUnit branch = allOrgUnits.Where(o => o.Uuid.Equals(entry.Key)).FirstOrDefault();
                if (branch == null)
                {
                    logger.LogError($"Could not find a branch OU for Uuid: {entry.Key} from SOFD");
                    continue;
                }

                DirectoryEntry rootOUEntry = new DirectoryEntry(@"LDAP://" + entry.Value);
                HandleOrgUnit(allOrgUnits, orgUnitMap, branch, rootOUEntry);
            }

            return orgUnitMap;
        }

        private void HandleOrgUnit(List<OrgUnit> allOrgUnits, Dictionary<string, string> orgUnitMap, OrgUnit sofdBranch, DirectoryEntry branchOUEntry)
        {
            orgUnitMap.Add(sofdBranch.Uuid, branchOUEntry.Properties["distinguishedName"].Value.ToString());

            // delete
            foreach (DirectoryEntry ou in ListAllOrgUnitsFrom(branchOUEntry))
            {
                // if a DirectoryServicesCOMException is thrown, the object has already been moved (as a child of another moved ou)
                try
                {
                    // if already deleted - skip
                    if (ou.Properties["distinguishedName"].Value.ToString().EndsWith(rootDeletedOusOu))
                    {
                        continue;
                    }

                    if (ou.Properties[oUIdField].Value != null && !string.IsNullOrEmpty(ou.Properties[oUIdField].Value.ToString()))
                    {
                        string id = ou.Properties[oUIdField].Value.ToString();
                        OrgUnit match = allOrgUnits.Where(i => i.Uuid.Equals(id)).FirstOrDefault();
                        if (match == null)
                        {
                            MoveToDeletedOUs(ou);
                        }
                        else
                        {
                            // check if it has the excluded tag in sofd (or has inherited it)
                            if (match.ShouldBeExcluded)
                            {
                                logger.LogInformation($"OrgUnit with sofd uuid {match.Uuid} and name {match.Name} has the excluded tag in sofd, but is already in Active Directory. Moving Active Directory OrgUnit to OrgUnit for deleted OrgUnits.");
                                MoveToDeletedOUs(ou);
                            }
                        }
                    }
                    ou.Close();
                }
                catch (DirectoryServicesCOMException)
                {
                    // is already moved, just skip
                }
            }

            // update root if needed
            if (branchOUEntry.Properties[oUIdField].Value == null || !string.Equals(branchOUEntry.Properties[oUIdField].Value.ToString(), sofdBranch.Uuid))
            {
                branchOUEntry.Properties[oUIdField].Value = sofdBranch.Uuid;
                branchOUEntry.CommitChanges();
            }

            // create and update children
            foreach (OrgUnit childOu in allOrgUnits.Where(o => o.ParentUuid != null && o.ParentUuid.Equals(sofdBranch.Uuid)).ToList())
            {
                HandleOrgUnitsRecursive(childOu, allOrgUnits, branchOUEntry, orgUnitMap);
            }
        }

        internal void CheckForMove(Person person, User user, List<Affiliation> activeAffiliations, Dictionary<string, string> userLocations, Dictionary<string, string> orgUnitMap)
        {
            logger.LogDebug($"Checking if user with userId {user.UserId} should be moved");

            if (!userLocations.ContainsKey(user.UserId.ToLower()))
            {
                logger.LogWarning($"Recieved user with userId {user.UserId} from sofd person with uuid {person.Uuid}, but the user can not be found in Active Directory");
                return;
            }

            string path = userLocations[user.UserId.ToLower()];

            // find relevant affiliation
            Affiliation affiliation = activeAffiliations.Where(a => object.Equals(a.EmployeeId, user.EmployeeId)).FirstOrDefault();
            if (affiliation == null)
            {
                affiliation = activeAffiliations.Where(a => a.Prime).FirstOrDefault();
                if (affiliation == null)
                {
                    logger.LogDebug($"User with userId {user.UserId} will not be moved. No relevant affiliations for person with uuid {person.Uuid}");
                    return;
                }
            }

            string affOUUuid = String.IsNullOrEmpty(affiliation.AlternativeOrgunitUuid) ? affiliation.OrgUnitUuid : affiliation.AlternativeOrgunitUuid;
            if (!orgUnitMap.ContainsKey(affOUUuid))
            {
                logger.LogWarning($"Tried to find OrgUnit with id {affOUUuid} for affiliation with uuid {affiliation.Uuid}, but it could not be found with Active Directory - for user " + user.UserId);
                return;
            }

            string dn = orgUnitMap[affOUUuid];

            string currentOuPath = path;
            int idx = currentOuPath.IndexOf(",");
            if (idx >= 0)
            {
                currentOuPath = currentOuPath.Substring(idx + 1);
            }

            if (!string.Equals(currentOuPath, dn))
            {
                MoveUser(user.UserId, path, dn);
            }
        }

        private List<DirectoryEntry> ListAllOrgUnitsFrom(DirectoryEntry entry)
        {
            List<DirectoryEntry> all = new List<DirectoryEntry>();

            ListAllRecursive(all, entry);

            return all;
        }

        private void ListAllRecursive(List<DirectoryEntry> entries, DirectoryEntry entry)
        {
            if (entry.Children != null)
            {
                foreach (DirectoryEntry child in entry.Children)
                {
                    if (!child.SchemaClassName.Equals("organizationalUnit"))
                    {
                        continue;
                    }

                    entries.Add(child);
                    ListAllRecursive(entries, child);
                }
            }
        }

        public void HandleOrgUnitsRecursive(OrgUnit currentNode, List<OrgUnit> allOrgUnits, DirectoryEntry createIn, Dictionary<string, string> orgUnitMap)
        {
            // skip if it should be excluded. If this one is excluded, all the children should be excluded as well
            if (currentNode.ShouldBeExcluded)
            {
                return;
            }

            // check if exists 
            DirectoryEntry match = GetOUFromId(currentNode.Uuid);
            string name = GetNameForOU(currentNode);

            if (match == null)
            {
                // create
                match = CreateOU(createIn, name, currentNode, allOrgUnits);
            }
            else
            {
                // check if ou should be updated
                UpdateOU(match, name, currentNode, createIn, allOrgUnits);
            }

            orgUnitMap.Add(currentNode.Uuid, match.Properties["distinguishedName"][0].ToString());

            // handle children
            foreach (OrgUnit childOu in allOrgUnits.Where(o => o.ParentUuid != null && o.ParentUuid.Equals(currentNode.Uuid)).ToList())
            {
                HandleOrgUnitsRecursive(childOu, allOrgUnits, match, orgUnitMap);
            }

            match.Close();
        }

        public DirectoryEntry GetUserFromUserId(string userId)
        {
            var filter = string.Format("(&(objectClass=user)(objectClass=person)({0}={1}))", userIdField, userId);
            return SearchForDirectoryEntry(filter);
        }

        private string CreateFilter(params string[] filters)
        {
            var allFilters = filters.ToList();
            allFilters.Add("objectClass=user");
            allFilters.Add("!(objectclass=computer)");

            return string.Format("(&{0})", string.Concat(allFilters.Where(x => !String.IsNullOrEmpty(x)).Select(x => '(' + x + ')').ToArray()));
        }

        public Dictionary<string, string> GetUserLocations()
        {
            using (var directoryEntry = new DirectoryEntry())
            {
                string filter = CreateFilter("!(isDeleted=TRUE)");
                using (var directorySearcher = new DirectorySearcher(directoryEntry, filter, new string[] { "sAMAccountName", "distinguishedName" }))
                {
                    directorySearcher.PageSize = 1000;
                    Dictionary<string, string> userLocations = new Dictionary<string, string>();

                    using (var searchResultCollection = directorySearcher.FindAll())
                    {
                        logger.LogInformation("Found {0} users in Active Directory", searchResultCollection.Count);

                        foreach (SearchResult searchResult in searchResultCollection)
                        {
                            string userId = searchResult.Properties["sAMAccountName"][0].ToString().ToLower();
                            string path = searchResult.Properties["distinguishedName"][0].ToString();

                            userLocations.Add(userId, path);
                        }
                    }

                    return userLocations;
                }
            }
        }

        public DirectoryEntry GetOUFromId(string id)
        {
            if (id == null)
            {
                return null;
            }

            var filter = string.Format("(&(objectClass=organizationalUnit)({0}={1}))", oUIdField, id);
            return SearchForDirectoryEntry(filter);
        }

        private DirectoryEntry SearchForDirectoryEntry(string filter)
        {
            using DirectoryEntry entry = new DirectoryEntry();
            using DirectorySearcher search = new DirectorySearcher(entry);
            search.Filter = filter;

            var result = search.FindOne();
            if (result != null)
            {
                return result.GetDirectoryEntry();
            }

            return null;
        }

        private string GetNameForOU(OrgUnit orgUnit)
        {
            try
            {
                string name = orgUnit.Name;
                if (!string.IsNullOrEmpty(orgUnit.DisplayName))
                {
                    name = orgUnit.DisplayName;
                }
                name = EscapeCharactersForAD(name);
                name = name.Trim();
                return name;
            }
            catch (Exception)
            {
                logger.LogError("Failed to get name for OU " + orgUnit.Uuid + " / name = '" + orgUnit.Name + "' / displayName = '" + orgUnit.DisplayName + "'");
                throw;
            }
        }

        private void UpdateOU(DirectoryEntry match, string name, OrgUnit currentNode, DirectoryEntry shouldBeIn, List<OrgUnit> allOrgUnits)
        {
            bool changes = false;
            if (!match.Name.Equals("OU=" + name))
            {
                logger.LogInformation("Renaming " + match.Name + " to " + name);

                match.Rename("OU=" + name);
                changes = true;
            }

            if (!string.IsNullOrEmpty(eanField))
            {
                long ean = currentNode.Ean;
                if (ean == 0 && eanFieldInherit)
                {
                    ean = FindInheritedEan(currentNode, allOrgUnits);
                }

                if (ean == 0 && match.Properties[eanField].Value != null)
                {
                    logger.LogInformation("Clearing EAN for " + name);

                    match.Properties[eanField].Clear();
                    changes = true;
                }
                else if ( ean != 0 && !object.Equals(match.Properties[eanField].Value, ean.ToString()))
                {
                    logger.LogInformation("Setting EAN for " + name + " to " + currentNode.Ean.ToString());

                    match.Properties[eanField].Value = ean.ToString();
                    changes = true;
                }
            }

            if (!string.IsNullOrEmpty(losIdField) && !object.Equals(match.Properties[losIdField].Value, currentNode.MasterId))
            {
                if (currentNode.MasterId == null)
                {
                    if (match.Properties[losIdField].Value != null)
                    {
                        logger.LogInformation("Clearing LOS ID for " + name);

                        match.Properties[losIdField].Clear();
                        changes = true;
                    }
                }
                else
                {
                    logger.LogInformation("Setting LOS ID for " + name + " to " + currentNode.MasterId);

                    match.Properties[losIdField].Value = currentNode.MasterId;
                    changes = true;
                }
            }

            OrgUnitPost primaryPost = currentNode.PostAddresses.Where(p => p.Prime).FirstOrDefault();
            if (primaryPost != null)
            {
                if (!string.IsNullOrEmpty(streetAddressField) && !object.Equals(match.Properties[streetAddressField].Value, primaryPost.Street))
                {
                    logger.LogInformation("Setting streetAddress for " + name + " to " + primaryPost.Street);

                    match.Properties[streetAddressField].Value = primaryPost.Street;
                    changes = true;
                }
                if (!string.IsNullOrEmpty(cityField) && !object.Equals(match.Properties[cityField].Value, primaryPost.City))
                {
                    logger.LogInformation("Setting city for " + name + " to " + primaryPost.City);

                    match.Properties[cityField].Value = primaryPost.City;
                    changes = true;
                }
                if (!string.IsNullOrEmpty(postalCodeField) && !object.Equals(match.Properties[postalCodeField].Value, primaryPost.PostalCode))
                {
                    logger.LogInformation("Setting postalCode for " + name + " to " + primaryPost.PostalCode);

                    match.Properties[postalCodeField].Value = primaryPost.PostalCode;
                    changes = true;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(streetAddressField))
                {
                    if (match.Properties[streetAddressField].Value != null)
                    {
                        logger.LogInformation("Clearing streetAddress for " + name);
                        match.Properties[streetAddressField].Clear();
                        changes = true;
                    }
                }
                if (!string.IsNullOrEmpty(cityField))
                {
                    if (match.Properties[cityField].Value != null)
                    {
                        logger.LogInformation("Clearing city for " + name);
                        match.Properties[cityField].Clear();
                        changes = true;
                    }
                }
                if (!string.IsNullOrEmpty(postalCodeField))
                {
                    if (match.Properties[postalCodeField].Value != null)
                    {
                        logger.LogInformation("Clearing postalCode for " + name);
                        match.Properties[postalCodeField].Clear();
                        changes = true;
                    }
                }
            }

            if (changes)
            {
                match.CommitChanges();
            }

            // check if moved
            if (!match.Parent.Properties["distinguishedName"].Value.Equals(shouldBeIn.Properties["distinguishedName"].Value))
            {
                logger.LogInformation($"Moving OU {match.Properties["distinguishedName"].Value} to {shouldBeIn.Properties["distinguishedName"].Value}");

                // try to remove the "protect against accidental deletion" flag
                try
                {
                    System.Security.Principal.IdentityReference newOwner = new System.Security.Principal.NTAccount("Everyone").Translate(typeof(System.Security.Principal.SecurityIdentifier));
                    ActiveDirectoryAccessRule rule = new ActiveDirectoryAccessRule(newOwner, ActiveDirectoryRights.Delete | ActiveDirectoryRights.DeleteChild | ActiveDirectoryRights.DeleteTree, System.Security.AccessControl.AccessControlType.Deny);
                    match.ObjectSecurity.RemoveAccessRule(rule);

                    match.CommitChanges();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Tried to remove 'protect against accidental deletion' on OU, but it failed");
                }

                match.MoveTo(shouldBeIn);
            }
        }

        private DirectoryEntry CreateOU(DirectoryEntry ouToCreateIn, string name, OrgUnit currentNode, List<OrgUnit> allOrgUnits)
        {
            DirectoryEntry newOU = ouToCreateIn.Children.Add("OU=" + name, "OrganizationalUnit");
            newOU.Properties[oUIdField].Value = currentNode.Uuid;

            if (!string.IsNullOrEmpty(eanField))
            {
                long ean = currentNode.Ean;
                if (eanFieldInherit)
                {
                    if (ean == 0)
                    {
                        ean = FindInheritedEan(currentNode, allOrgUnits);
                    }
                }

                if (ean > 0)
                {
                    newOU.Properties[eanField].Value = ean.ToString();
                }
            }

            if (!string.IsNullOrEmpty(losIdField))
            {
                if (currentNode.MasterId != null)
                {
                    newOU.Properties[losIdField].Value = currentNode.MasterId;
                }
            }

            OrgUnitPost primaryPost = currentNode.PostAddresses.Where(p => p.Prime).FirstOrDefault();
            if (primaryPost != null)
            {
                if (!string.IsNullOrEmpty(streetAddressField))
                {
                    newOU.Properties[streetAddressField].Value = primaryPost.Street;
                }

                if (!string.IsNullOrEmpty(cityField))
                {
                    newOU.Properties[cityField].Value = primaryPost.City;
                }

                if (!string.IsNullOrEmpty(postalCodeField))
                {
                    newOU.Properties[postalCodeField].Value = primaryPost.PostalCode;
                }
            }

            logger.LogInformation("Creating OU " + name);

            newOU.CommitChanges();

            return newOU;
        }

        private string EscapeCharactersForAD(string name)
        {
            // escape specific characters (nb! keep the backslash first in the list to prevent replacing replacements)
            name = name.Replace(@"\", @"\\");
            name = name.Replace(@"+", @"\+");
            name = name.Replace(@",", @"\,");
            name = name.Replace(@"<", @"\<");
            name = name.Replace(@">", @"\>");
            name = name.Replace(@";", @"\;");
            name = name.Replace(@"/", @"\/");
            name = name.Replace(@"=", @"\=");
            name = name.Replace(@"""", @"\""");

            // remove specific characters
            name = name.Replace("#", "");

            // replace ampersand with "og"
            name = name.Replace("&", " og ");
            
            // replace multiple white spaces with single space
            name = Regex.Replace(name, @"\s+", " ");

            return name;
        }

        public void MoveToDeletedOUs(DirectoryEntry toMove)
        {
            var newOUName = "OU=slettede_ous_" + DateTime.Now.ToString("yyyy_MM_dd");
            DirectoryEntry rootDeletedOusOuEntry = new DirectoryEntry(@"LDAP://" + rootDeletedOusOu);
            if (rootDeletedOusOuEntry == null)
            {
                logger.LogError("Could not find " + rootDeletedOusOu + " so deleting OU is not possible");
                return;
            }

            DirectoryEntry match = null;
            foreach (DirectoryEntry ou in rootDeletedOusOuEntry.Children)
            {
                if (!ou.SchemaClassName.Equals("organizationalUnit"))
                {
                    continue;
                }

                if (ou.Name.Equals(newOUName))
                {
                    match = ou;
                }
            }

            if (match == null)
            {
                match = rootDeletedOusOuEntry.Children.Add(newOUName, "OrganizationalUnit");
                if (match != null)
                {
                    logger.LogInformation("Created deleted ous ou for today");

                    match.CommitChanges();
                }
                else
                {
                    logger.LogError("Could not create deleted OU for today!");
                    return;
                }
            }

            logger.LogInformation("Moving OU with name " + toMove.Name + " to deleted ous ou for today");

            // try to remove the "protect against accidental deletion" flag
            try
            {
                System.Security.Principal.IdentityReference newOwner = new System.Security.Principal.NTAccount("Everyone").Translate(typeof(System.Security.Principal.SecurityIdentifier));
                ActiveDirectoryAccessRule rule = new ActiveDirectoryAccessRule(newOwner, ActiveDirectoryRights.Delete | ActiveDirectoryRights.DeleteChild | ActiveDirectoryRights.DeleteTree, System.Security.AccessControl.AccessControlType.Deny);
                toMove.ObjectSecurity.RemoveAccessRule(rule);

                toMove.CommitChanges();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Tried to remove 'protect against accidental deletion' on OU, but it failed");
            }

            toMove.MoveTo(match);
            match.Close();
        }

        private void MoveUser(string userId, string from, string to)
        {
            logger.LogInformation("Moved user " + userId + " from " + from + " to " + to);

            if (!dryRunMoveUsers)
            {
                DirectoryEntry eLocation = new DirectoryEntry("LDAP://" + from);
                DirectoryEntry nLocation = new DirectoryEntry("LDAP://" + to);

                eLocation.MoveTo(nLocation);
                eLocation.Close();
                nLocation.Close();
            }
        }

        private PrincipalContext GetPrincipalContext(string ldapPath = null)
        {
            if (ldapPath != null)
            {
                return new PrincipalContext(ContextType.Domain, null, ldapPath);
            }

            return new PrincipalContext(ContextType.Domain);
        }

        private long FindInheritedEan(OrgUnit currentNode, List<OrgUnit> allOrgUnits)
        {
            if (currentNode == null)
            {
                return 0;
            }

            if (currentNode.Ean != 0)
            {
                return currentNode.Ean;
            }

            if (string.IsNullOrEmpty(currentNode.ParentUuid))
            {
                return 0;
            }

            var parentOrgUnit = allOrgUnits.FirstOrDefault(u => u.Uuid == currentNode.ParentUuid);

            return FindInheritedEan(parentOrgUnit, allOrgUnits);
        }

    }
}
