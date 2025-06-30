using Microsoft.Extensions.Logging;
using sofd_core_ad_replicator.Services.Sofd.Model;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

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
        private readonly bool excludeExternalUsers;
        private readonly bool testOURun;

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
            excludeExternalUsers = settings.ActiveDirectorySettings.ExcludeExternalUsers;
            testOURun = settings.ActiveDirectorySettings.TestOURun;
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
                            if (testOURun)
                            {
                                logger.LogWarning("SIMULATE: would have deleted " + ou.Name);
                            }
                            else
                            {
                                MoveToDeletedOUs(ou);
                            }
                        }
                        else
                        {
                            // check if it has the excluded tag in sofd (or has inherited it)
                            if (match.ShouldBeExcluded)
                            {
                                if (testOURun)
                                {
                                    logger.LogWarning("SIMULATE: would have deleted " + ou.Name);
                                }
                                else
                                {
                                    logger.LogInformation($"OrgUnit with sofd uuid {match.Uuid} and name {match.Name} has the excluded tag in sofd, but is already in Active Directory. Moving Active Directory OrgUnit to OrgUnit for deleted OrgUnits.");
                                    MoveToDeletedOUs(ou);
                               }
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

        internal void CheckForMove(Person person, User user, List<Affiliation> activeAffiliations, Dictionary<string, string> userLocations, Dictionary<string, string> orgUnitMap, List<string> dontMoveUserFromTheseOUs, List<OrgUnit> orgUnitTree)
        {
            logger.LogDebug($"Checking if user with userId {user.UserId} should be moved");

            if (!userLocations.ContainsKey(user.UserId.ToLower()))
            {
                logger.LogWarning($"Recieved user with userId {user.UserId} from sofd person with uuid {person.Uuid}, but the user can not be found in Active Directory");
                return;
            }

            string path = userLocations[user.UserId.ToLower()];

            if (dontMoveUserFromTheseOUs != null && dontMoveUserFromTheseOUs.Count > 0)
            {
                string lowerPath = path.ToLower();
                foreach (var ou in dontMoveUserFromTheseOUs)
                {
                    if (lowerPath.Contains(ou.ToLower()))
                    {
                        logger.LogDebug("Not moving " + user.UserId + " because user somewhere inside " + ou);
                        return;
                    }
                }
            }

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
            // check if user is external and if configured to exclude external users
            if (excludeExternalUsers && affiliation.AffiliationType == "EXTERNAL")
            {
                logger.LogDebug($"User with userId {user.UserId} will not be moved because external users are excluded.");
                return;
            }

            string affOUUuid = String.IsNullOrEmpty(affiliation.AlternativeOrgunitUuid) ? affiliation.OrgUnitUuid : affiliation.AlternativeOrgunitUuid;
            if (!orgUnitMap.ContainsKey(affOUUuid))
            {
                // find current ou in OrgUnitTree
                bool foundParent = false;
                OrgUnit currentOU = orgUnitTree.Where(ou => ou.Uuid == affOUUuid).FirstOrDefault();
                if (currentOU != null)
                {
                    //Try to find a parent ou
                    OrgUnit parentOU = currentOU.Parent;
                    while (foundParent == false)
                    {
                        if (parentOU != null && orgUnitMap.ContainsKey(parentOU.Uuid))
                        {
                            logger.LogInformation($"Found alternative OrgUnit: {parentOU.Uuid}");
                            affOUUuid = parentOU.Uuid;
                            foundParent = true;
                        } else
                        {
                            if (parentOU != null && parentOU.Parent != null)
                            {
                                parentOU = parentOU.Parent;
                            }
                            else
                            {
                                //no parent found. give up
                                break;
                            }
                        }
                    }
                    
                }

                if (!foundParent)
                {
                    logger.LogWarning($"Tried to find OrgUnit with id {affOUUuid} for affiliation with uuid {affiliation.Uuid}, but it could not be found with Active Directory - for user " + user.UserId);
                    return;
                }
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

        public void DeleteGroup(GroupDetails group)
        {
            if (settings.ActiveDirectorySettings.GroupSettings.DryRun)
            {
                logger.LogInformation("DRYRUN: deleting group " + group.Name);
                return;
            }

            GroupPrincipal gp = GroupPrincipal.FindByIdentity(new PrincipalContext(ContextType.Domain), group.SamAccountName);
            if (gp != null)
            {
                try
                {
                    logger.LogInformation("Deleting group " + group.Name);
                    gp.Delete();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Could not delete group " + group.Name);
                }
            }
        }

        public void UpdateGroup(GroupDetails group, string name, string samAccountName, string description, string displayName, IEnumerable<string> members, Dictionary<string, string> userLocations)
        {
            try
            {
                bool changes = false;
                bool samAccountNameChanges = false, descriptionChanges = false, displayNameChanges = false;
                bool rename = false;
                bool memberChanges = false;

                if (name.Length > 63)
                {
                    name = name.Substring(0, 63).Trim();
                }

                if (!string.Equals(group.Name, name))
                {
                    logger.LogInformation("Group " + group.Name + " - set name to " + name);
                    rename = true;
                }

                if (!string.Equals(group.SamAccountName, samAccountName))
                {
                    logger.LogInformation("Group " + group.Name + " - set samAccountName to " + samAccountName);
                    changes = true;
                    samAccountNameChanges = true;
                }

                if (!string.Equals(group.Description, description))
                {
                    logger.LogInformation("Group " + group.Name + " - set description to " + description);
                    changes = true;
                    descriptionChanges = true;
                }

                if (!string.Equals(group.DisplayName, displayName))
                {
                    logger.LogInformation("Group " + group.Name + " - set displayName to " + displayName);
                    changes = true;
                    displayNameChanges = true;
                }

                if (members != null && members.Count() > 0)
                {
                    members = members.Distinct();
                }

                // compare members
                foreach (string member in group.Members)
                {
                    bool found = false;

                    foreach (string shouldBeMember in members)
                    {
                        if (member.ToLower().Equals(shouldBeMember.ToLower()))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        group.ToRemove.Add(member);
                    }
                }

                foreach (string shouldBeMember in members)
                {
                    bool found = false;

                    foreach (string member in group.Members)
                    {
                        if (member.ToLower().Equals(shouldBeMember.ToLower()))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        group.ToAdd.Add(shouldBeMember);
                    }
                }

                if (group.ToAdd.Count > 0 || group.ToRemove.Count > 0)
                {
                    logger.LogInformation("Group " + group.Name + " - has membership changes");

                    logger.LogDebug("ToAdd: " + string.Join(", ", group.ToAdd));
                    logger.LogDebug("ToRemove: " + string.Join(", ", group.ToRemove));

                    memberChanges = true;
                }

                if (changes || rename || memberChanges)
                {
                    if (settings.ActiveDirectorySettings.GroupSettings.DryRun)
                    {
                        logger.LogInformation("DRYRUN: would have updated group " + group.Name);
                    }
                    else
                    {
                        GroupPrincipal gp = GroupPrincipal.FindByIdentity(new PrincipalContext(ContextType.Domain), group.SamAccountName);
                        if (gp != null)
                        {
                            if (changes)
                            {
                                if (descriptionChanges)
                                {
                                    gp.Description = description;
                                }

                                if (displayNameChanges)
                                {
                                    gp.DisplayName = displayName;
                                }

                                if (samAccountNameChanges)
                                {
                                    gp.SamAccountName = samAccountName;
                                }

                                gp.Save();
                            }

                            if (rename || memberChanges)
                            {
                                DirectoryEntry de = ((DirectoryEntry)gp.GetUnderlyingObject());

                                if (rename)
                                {
                                    de.Rename("CN=" + name);
                                }

                                List<string> currentMembers = GetMembers(group.DN);

                                if (memberChanges)
                                {
                                    foreach (string member in group.ToAdd)
                                    {
                                        string dn = null;
                                        if (userLocations.ContainsKey(member))
                                        {
                                            dn = userLocations[member];
                                        }
                                        else
                                        {
                                            continue;
                                        }

                                        if (!currentMembers.Contains(dn))
                                        {
                                            de.Properties["member"].Add(dn);
                                        }
                                        else
                                        {
                                            logger.LogWarning("Could not add " + member + " user was already in grup");
                                        }
                                    }

                                    foreach (string member in group.ToRemove)
                                    {
                                        string dn = null;
                                        if (userLocations.ContainsKey(member))
                                        {
                                            dn = userLocations[member];
                                        }
                                        else
                                        {
                                            logger.LogWarning("Could not find " + member + " for group removal");
                                            continue;
                                        }

                                        if (currentMembers.Contains(dn))
                                        {
                                            de.Properties["member"].Remove(dn);
                                        }
                                        else
                                        {
                                            logger.LogWarning("Could not remove " + member + " did not find user in group");
                                        }
                                    }
                                }

                                de.CommitChanges();
                            }
                        }
                        else
                        {
                            logger.LogError("Could not find group " + group.SamAccountName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update group " + name);
                logger.LogWarning("Tried to add " + string.Join(";", group.ToAdd));
                logger.LogWarning("Tried to remove " + string.Join(";", group.ToAdd));
            }
        }

        public List<GroupDetails> GetAllGroups(Dictionary<string, string> usersDnToSamAccountName, bool useFastMethod)
        {
            List<GroupDetails> res = new List<GroupDetails>();

            using (PrincipalContext context = new PrincipalContext(ContextType.Domain, null, settings.ActiveDirectorySettings.GroupSettings.GroupOUDN))
            {
                GroupPrincipal template = new GroupPrincipal(context);
                using (PrincipalSearcher searcher = new PrincipalSearcher(template))
                {
                    ((DirectorySearcher)searcher.GetUnderlyingSearcher()).SearchScope = SearchScope.OneLevel;
                    ((DirectorySearcher)searcher.GetUnderlyingSearcher()).PageSize = 500;

                    using (var result = searcher.FindAll())
                    {
                        foreach (var group in result)
                        {
                            GroupPrincipal groupPrincipal = group as GroupPrincipal;
                            if (groupPrincipal != null)
                            {
                                DirectoryEntry de = (DirectoryEntry)group.GetUnderlyingObject();

                                if (de.Properties.Contains(settings.ActiveDirectorySettings.GroupSettings.GroupIdField))
                                {
                                    string id = (string)de.Properties[settings.ActiveDirectorySettings.GroupSettings.GroupIdField][0];
                                    if (id.Split(":").Length == 2)
                                    {
                                        GroupDetails groupDetails = new GroupDetails();
                                        groupDetails.Id = id;
                                        groupDetails.Name = groupPrincipal.Name;
                                        groupDetails.Description = groupPrincipal.Description;
                                        groupDetails.SamAccountName = groupPrincipal.SamAccountName;
                                        groupDetails.DisplayName = groupPrincipal.DisplayName;
                                        groupDetails.DN = groupPrincipal.DistinguishedName;

                                        if (!useFastMethod)
                                        {
                                            foreach (var member in groupPrincipal.GetMembers())
                                            {
                                                if (member is UserPrincipal)
                                                {
                                                    groupDetails.Members.Add(member.SamAccountName);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            List<string> members = GetMembers(groupPrincipal.DistinguishedName);
                                            foreach (string member in members)
                                            {
                                                if (usersDnToSamAccountName.ContainsKey(member))
                                                {
                                                    groupDetails.Members.Add(usersDnToSamAccountName[member]);
                                                }
                                            }
                                        }

                                        res.Add(groupDetails);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return res;
        }

        public List<string> GetMembers(string groupDN)
        {
            List<string> members = new List<string>();

            using (DirectoryEntry group = new DirectoryEntry("LDAP://" + groupDN))
            {
                group.RefreshCache(new[] { "member" });

                var membersFound = 0;
                while (true)
                {
                    var memberDns = group.Properties["member"];
                    if (memberDns.Count == 0)
                    {
                        break;
                    }

                    foreach (string member in memberDns)
                    {
                        members.Add(member);
                    }

                    membersFound += memberDns.Count;

                    try
                    {
                        group.RefreshCache(new[] { $"member;range={membersFound}-*" });
                    }
                    catch (COMException e)
                    {
                        // out of results
                        if (e.ErrorCode == unchecked((int)0x80072020))
                        {
                            break;
                        }

                        // anything else is bad
                        throw;
                    }
                }
            }

            return members;
        }

        public void CreateGroup(string name, string displayName, string description, string samAccountName, string id, IEnumerable<string> members, Dictionary<string, string> userLocations)
        {
            if (settings.ActiveDirectorySettings.GroupSettings.DryRun)
            {
                logger.LogInformation("DRYRUN: would have created group " + name);
                return;
            }

            logger.LogInformation("Creating group " + name);

            DirectoryEntry directoryEntry = new DirectoryEntry(@"LDAP://" + settings.ActiveDirectorySettings.GroupSettings.GroupOUDN);
            if (directoryEntry == null)
            {
                logger.LogError("Could not find " + settings.ActiveDirectorySettings.GroupSettings.GroupOUDN);
                return;
            }

            DirectoryEntry newGroup = directoryEntry.Children.Add("CN=" + name, "group");
            newGroup.Properties["displayName"].Value = displayName;
            newGroup.Properties["description"].Value = description;
            newGroup.Properties["sAMAccountName"].Value = samAccountName;
            // universal security group
            newGroup.Properties["groupType"].Add(unchecked((int)-2147483640));
            newGroup.Properties[settings.ActiveDirectorySettings.GroupSettings.GroupIdField].Value = id;

            if (members != null && members.Count() > 0)
            {
                members = members.Distinct();

                foreach (string member in members)
                {
                    string dn = null;
                    if (userLocations.ContainsKey(member))
                    {
                        dn = userLocations[member];
                    }
                    else
                    {
                        continue;
                    }

                    newGroup.Properties["member"].Add(dn);
                }
            }

            newGroup.CommitChanges();
            newGroup.Close();
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
                if (testOURun)
                {
                    logger.LogWarning("SIMULATE: would have created " + name);
                }
                else
                {
                    match = CreateOU(createIn, name, currentNode, allOrgUnits);
                }
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

        public string GetNameForOU(OrgUnit orgUnit)
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
                if (testOURun)
                {
                    logger.LogWarning("SIMULATE: would have renamed " + match.Name + " to " + name);
                }
                else
                {
                    logger.LogInformation("Renaming " + match.Name + " to " + name);

                    match.Rename("OU=" + name);
                    changes = true;
                }
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
                    if (testOURun)
                    {
                        logger.LogWarning("SIMULATE: would have cleared EAN for " + name);
                    }
                    else
                    {
                        logger.LogInformation("Clearing EAN for " + name);

                        match.Properties[eanField].Clear();
                        changes = true;
                    }
                }
                else if ( ean != 0 && !object.Equals(match.Properties[eanField].Value, ean.ToString()))
                {
                    if (testOURun)
                    {
                        logger.LogWarning("SIMULATE: would have set EAN for " + name);
                    }
                    else
                    {
                        logger.LogInformation("Setting EAN for " + name + " to " + currentNode.Ean.ToString());

                        match.Properties[eanField].Value = ean.ToString();
                        changes = true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(losIdField) && !object.Equals(match.Properties[losIdField].Value, currentNode.MasterId))
            {
                if (currentNode.MasterId == null)
                {
                    if (testOURun)
                    {
                        logger.LogWarning("SIMULATE: would have cleared LOS for " + name);
                    }
                    else
                    {
                        if (match.Properties[losIdField].Value != null)
                        {
                            logger.LogInformation("Clearing LOS ID for " + name);

                            match.Properties[losIdField].Clear();
                            changes = true;
                        }
                    }
                }
                else
                {
                    if (testOURun)
                    {
                        logger.LogWarning("SIMULATE: would have set LOS for " + name);
                    }
                    else
                    {
                        logger.LogInformation("Setting LOS ID for " + name + " to " + currentNode.MasterId);

                        match.Properties[losIdField].Value = currentNode.MasterId;
                        changes = true;
                    }
                }
            }

            OrgUnitPost primaryPost = currentNode.PostAddresses.Where(p => p.Prime).FirstOrDefault();
            if (primaryPost != null)
            {
                if (!string.IsNullOrEmpty(streetAddressField) && !object.Equals(match.Properties[streetAddressField].Value, primaryPost.Street))
                {
                    if (testOURun)
                    {
                        logger.LogWarning("SIMULATE: would have set street for " + name);
                    }
                    else
                    {
                        logger.LogInformation("Setting streetAddress for " + name + " to " + primaryPost.Street);

                        match.Properties[streetAddressField].Value = primaryPost.Street;
                        changes = true;
                    }
                }
                if (!string.IsNullOrEmpty(cityField) && !object.Equals(match.Properties[cityField].Value, primaryPost.City))
                {
                    if (testOURun)
                    {
                        logger.LogWarning("SIMULATE: would have set city for " + name);
                    }
                    else
                    {
                        logger.LogInformation("Setting city for " + name + " to " + primaryPost.City);

                        match.Properties[cityField].Value = primaryPost.City;
                        changes = true;
                    }
                }
                if (!string.IsNullOrEmpty(postalCodeField) && !object.Equals(match.Properties[postalCodeField].Value, primaryPost.PostalCode))
                {
                    if (testOURun)
                    {
                        logger.LogWarning("SIMULATE: would have set postalCode for " + name);
                    }
                    else
                    {
                        logger.LogInformation("Setting postalCode for " + name + " to " + primaryPost.PostalCode);

                        match.Properties[postalCodeField].Value = primaryPost.PostalCode;
                        changes = true;
                    }
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(streetAddressField))
                {
                    if (match.Properties[streetAddressField].Value != null)
                    {
                        if (testOURun)
                        {
                            logger.LogWarning("SIMULATE: would have cleared street for " + name);
                        }
                        else
                        {
                            logger.LogInformation("Clearing streetAddress for " + name);
                            match.Properties[streetAddressField].Clear();
                            changes = true;
                        }
                    }
                }
                if (!string.IsNullOrEmpty(cityField))
                {
                    if (match.Properties[cityField].Value != null)
                    {
                        if (testOURun)
                        {
                            logger.LogWarning("SIMULATE: would have cleared city for " + name);
                        }
                        else
                        {
                            logger.LogInformation("Clearing city for " + name);
                            match.Properties[cityField].Clear();
                            changes = true;
                        }
                    }
                }
                if (!string.IsNullOrEmpty(postalCodeField))
                {
                    if (testOURun)
                    {
                        logger.LogWarning("SIMULATE: would have cleared postalCode for " + name);
                    }
                    else
                    {
                        if (match.Properties[postalCodeField].Value != null)
                        {
                            logger.LogInformation("Clearing postalCode for " + name);
                            match.Properties[postalCodeField].Clear();
                            changes = true;
                        }
                    }
                }
            }

            if (changes)
            {
                if (testOURun)
                {
                    logger.LogWarning("SIMULATE: would have committed changes on " + name);
                }
                else
                {
                    match.CommitChanges();
                }
            }

            // check if moved
            if (!match.Parent.Properties["distinguishedName"].Value.Equals(shouldBeIn.Properties["distinguishedName"].Value))
            {
                if (testOURun)
                {
                    logger.LogInformation($"SIMULATE: Moving OU {match.Properties["distinguishedName"].Value} to {shouldBeIn.Properties["distinguishedName"].Value}");
                }
                else
                {
                    logger.LogInformation($"Moving OU {match.Properties["distinguishedName"].Value} to {shouldBeIn.Properties["distinguishedName"].Value}");
                    var DNBeforeMove = match.Properties["distinguishedName"].Value;
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
                    var DNAfterMove = match.Properties["distinguishedName"].Value;

                    if (settings.ActiveDirectorySettings.OURunScriptOnMove != null && settings.ActiveDirectorySettings.OURunScriptOnMove.Length > 0)
                    {
                        using (PrincipalContext ctx = GetPrincipalContext())
                        {
                            String script = null;
                            string domainController = ctx.ConnectedServer;
                            using (PowerShell ps = PowerShell.Create())
                            {
                                ps.AddScript(File.ReadAllText(@"" + settings.ActiveDirectorySettings.OURunScriptOnMove));
                                script = script + "\n\n" +

                                       "$ppArg1=\"" + domainController + "\"\n" +

                                       "$ppArg2=\"" + DNBeforeMove + "\"\n" +

                                       "$ppArg3=\"" + DNAfterMove + "\"\n";

                                script += "\nInvoke-Method -DomainController $ppArg1 -DistinguishedNameFrom $ppArg2 -DistinguishedNameTo $ppArg3";
                                script += "\n";

                                ps.AddScript(script);
                                // logger.LogInformation($"Invoking powershell script {settings.ActiveDirectorySettings.OURunScriptOnMove}: {script}");
                                logger.LogInformation($"Invoking powershell script {settings.ActiveDirectorySettings.OURunScriptOnMove}");
                                ps.Invoke();
                            }
                        }
                    }
                }
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

            if (settings.ActiveDirectorySettings.OURunScriptOnCreate != null && settings.ActiveDirectorySettings.OURunScriptOnCreate.Length > 0)
            {
                using (PrincipalContext ctx = GetPrincipalContext())
                {
                    String script = null;
                    string domainController = ctx.ConnectedServer;
                    using (PowerShell ps = PowerShell.Create())
                    {
                        ps.AddScript(File.ReadAllText(@"" + settings.ActiveDirectorySettings.OURunScriptOnCreate));

                        script = script + "\n\n" +

                               "$ppArg1=\"" + domainController + "\"\n" +

                               "$ppArg2=\"" + name + "\"\n" +

                               "$ppArg3=\"" + newOU.Properties["distinguishedName"].Value + "\"\n";

                        script += "\nInvoke-Method -DomainController $ppArg1 -Name $ppArg2 -DistinguishedName $ppArg3";

                        script += "\n";

                        ps.AddScript(script);
                        // logger.LogInformation($"Invoking powershell script {settings.ActiveDirectorySettings.OURunScriptOnCreate}: {script}");
                        logger.LogInformation($"Invoking powershell script {settings.ActiveDirectorySettings.OURunScriptOnCreate}");
                        ps.Invoke();
                    }
                }
            }
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
            logger.LogInformation($"Invoking MoveTo - entry:{toMove.Path} newParent:{match.Path}");
            toMove.MoveTo(match);
            match.Close();

            if (settings.ActiveDirectorySettings.OURunScriptOnDelete != null && settings.ActiveDirectorySettings.OURunScriptOnDelete.Length > 0)
            {
                using (PrincipalContext ctx = GetPrincipalContext())
                {
                    String script = null;
                    string domainController = ctx.ConnectedServer;
                    using (PowerShell ps = PowerShell.Create())
                    {
                        ps.AddScript(File.ReadAllText(@"" + settings.ActiveDirectorySettings.OURunScriptOnDelete));

                        script = script + "\n\n" +

                               "$ppArg1=\"" + domainController + "\"\n" +

                               "$ppArg2=\"" + toMove.Name + "\"\n" +

                               "$ppArg3=\"" + toMove.Properties["distinguishedName"].Value + "\"\n";

                        script += "\nInvoke-Method -DomainController $ppArg1 -Name $ppArg2 -DistinguishedName $ppArg3";


                        script += "\n";


                        ps.AddScript(script);
                        //logger.LogInformation($"Invoking powershell script {settings.ActiveDirectorySettings.OURunScriptOnDelete}: {script}");
                        logger.LogInformation($"Invoking powershell script {settings.ActiveDirectorySettings.OURunScriptOnDelete}");
                        ps.Invoke();
                    }
                }
            }
        }

        private void MoveUser(string userId, string from, string to)
        {
            if (dryRunMoveUsers)
            {
                logger.LogInformation("DRYRUN moving user " + userId + " from " + from + " to " + to);
            }
            else
            {
                logger.LogInformation("moving user " + userId + " from " + from + " to " + to);
                // LDAP doesn't escape "/" specifically, so it's nescessary to escape it separately
                DirectoryEntry eLocation = new DirectoryEntry("LDAP://" + from.Replace("/", "\\/"));
                DirectoryEntry nLocation = new DirectoryEntry("LDAP://" + to.Replace("/", "\\/"));
                var DNBefore = eLocation.Parent.Properties["distinguishedName"].Value;
                var DNAfter = nLocation.Properties["distinguishedName"].Value;

                eLocation.MoveTo(nLocation);
                eLocation.Close();
                nLocation.Close();

                if (settings.ActiveDirectorySettings.UserRunScriptOnMove != null && settings.ActiveDirectorySettings.UserRunScriptOnMove.Length > 0)
                {
                    using (PrincipalContext ctx = GetPrincipalContext())
                    {
                        String script = null;
                        string domainController = ctx.ConnectedServer;
                        using (PowerShell ps = PowerShell.Create())
                        {
                            ps.AddScript(File.ReadAllText(@"" + settings.ActiveDirectorySettings.UserRunScriptOnMove));
                            script = script + "\n\n" +

                                   "$ppArg1=\"" + domainController + "\"\n" +

                                   "$ppArg2=\"" + userId + "\"\n" +

                                   "$ppArg3=\"" + DNBefore + "\"\n" +

                                   "$ppArg4=\"" + DNAfter + "\"\n";

                            script += "\nInvoke-Method -DomainController $ppArg1 -SamAccountName $ppArg2 -DistinguishedNameFrom $ppArg3 -DistinguishedNameTo $ppArg4";
                            script += "\n";

                            ps.AddScript(script);
                            // logger.LogInformation($"Invoking powershell script {settings.ActiveDirectorySettings.UserRunScriptOnMove}: {script}");
                            logger.LogInformation($"Invoking powershell script {settings.ActiveDirectorySettings.UserRunScriptOnMove}");
                            ps.Invoke();
                        }
                    }
                }

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
