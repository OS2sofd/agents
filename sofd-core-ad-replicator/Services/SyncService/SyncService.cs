using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using sofd_core_ad_replicator.Services.ActiveDirectory;
using sofd_core_ad_replicator.Services.Sofd.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices;
using System.Linq;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.DirectoryServices.ActiveDirectory;
using System.Reflection;
using System.Xml.Linq;

namespace sofd_core_ad_replicator.Services.Sofd
{
    internal class SyncService : ServiceBase<SyncService>
    {
        private readonly ActiveDirectoryService activeDirectoryService;
        private readonly SofdService sofdService;

        private readonly string excludeFromSyncTagName;
        private readonly bool moveUsersEnabled;
        private readonly bool groupsEnabled;
        private readonly bool useFastMethod;
        private readonly int daysBeforeFirstWorkday;
        private readonly List<string> dontMoveUserRegularExpressions;
        private readonly List<string> dontMoveUserFromTheseOUs;

        public SyncService(IServiceProvider sp) : base(sp)
        {
            activeDirectoryService = sp.GetService<ActiveDirectoryService>();
            sofdService = sp.GetService<SofdService>();

            excludeFromSyncTagName = settings.SofdSettings.ExcludeFromSyncTagName;
            moveUsersEnabled = settings.ActiveDirectorySettings.MoveUsersEnabled;
            dontMoveUserRegularExpressions = settings.ActiveDirectorySettings.DontMoveUserRegularExpressions;
            dontMoveUserFromTheseOUs = settings.ActiveDirectorySettings.DontMoveUserFromTheseOUs;
            groupsEnabled = settings.ActiveDirectorySettings.GroupSettings.Enabled;
            daysBeforeFirstWorkday = settings.ActiveDirectorySettings.GroupSettings.DaysBeforeFirstWorkday;
            useFastMethod = settings.ActiveDirectorySettings.GroupSettings.UseFastMethod;
        }

        public void Synchronize()
        {
            try
            {
                logger.LogInformation("Executing FullSyncJob");
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                List<OrgUnit> orgUnits = sofdService.GetActiveOrgUnits();
                MarkExcludedOrgUnits(orgUnits);
                SetParentReference(orgUnits);
                logger.LogInformation("Updating OUs");
                var orgUnitMap = activeDirectoryService.HandleOrgUnits(orgUnits);

                List<Person> persons = null;
                Dictionary<string, string> userLocations = null;
                if (moveUsersEnabled)
                {
                    logger.LogInformation("Updating Users");

                    userLocations = activeDirectoryService.GetUserLocations();

                    persons = sofdService.GetPersons();
                    logger.LogInformation("Found " + persons.Count + " potential users in SOFD");

                    foreach (Person person in persons)
                    {
                        List<Affiliation> activeAffiliations = person.Affiliations == null ? new List<Affiliation>() : person.Affiliations.Where(a => IsActiveAffiliation(a)).ToList();
                        if (person.Deleted || person.Users == null || person.Users.Count == 0 || activeAffiliations.Count == 0)
                        {
                            continue;
                        }

                        foreach (User user in person.Users)
                        {
                            string userId = user.UserId.ToLower();

                            // check if the user is active and of type ACTIVE_DIRECTORY
                            if (user.Disabled || !user.UserType.Equals("ACTIVE_DIRECTORY"))
                            {
                                continue;
                            }

                            // check if the userId matches a pattern that means it can't be moved
                            if (dontMoveUserRegularExpressions != null && dontMoveUserRegularExpressions.Any(p => Regex.IsMatch(userId, p)))
                            {
                                continue;
                            }

                            try
                            {
                                activeDirectoryService.CheckForMove(person, user, activeAffiliations, userLocations, orgUnitMap, dontMoveUserFromTheseOUs, orgUnits);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Failed to move " + user.UserId);
                            }
                        }
                    }
                }

                if (groupsEnabled)
                {
                    logger.LogInformation("Updating Groups");

                    // reload userLocations, some users might have moved :)
                    userLocations = activeDirectoryService.GetUserLocations();

                    // 1 - find the root OU, for later iteration
                    OrgUnit rootOU = orgUnits.Where(o => o.ParentUuid == null).First();
                    if (rootOU == null)
                    {
                        throw new Exception("Could not find a root OU - aborting group creation");
                    }

                    // 2 - map AD users into tree structure map
                    Dictionary<string, OrgUnit> ouMap = new Dictionary<string, OrgUnit>();
                    foreach (OrgUnit ou in orgUnits)
                    {
                        ouMap.Add(ou.Uuid, ou);
                    }

                    if (persons == null)
                    {
                        persons = sofdService.GetPersons();
                    }

                    if (userLocations == null)
                    {
                        userLocations = activeDirectoryService.GetUserLocations();
                    }

                    DateTime now = DateTime.Now;
                    foreach (Person person in persons)
                    {
                        if (person.Affiliations == null || person.Affiliations.Count == 0)
                        {
                            continue;
                        }

                        if (person.Users != null)
                        {
                            List<string> reservedAffiliations = new List<string>();

                            foreach (User user in person.Users)
                            {
                                if (user.UserType.Equals("ACTIVE_DIRECTORY"))
                                {
                                    if (!string.IsNullOrEmpty(user.EmployeeId))
                                    {
                                        reservedAffiliations.Add(user.EmployeeId);
                                    }
                                }
                            }

                            foreach (User user in person.Users)
                            {
                                if (user.UserType.Equals("ACTIVE_DIRECTORY") && !user.Disabled)
                                {
                                    foreach (Affiliation affiliation in person.Affiliations)
                                    {
                                        // skip past affiliations (have to add 1 day, as it also checks on time, and it is 00:00 on affiliation)
                                        if (affiliation.StopDate != null && DateTime.Compare(now, ((DateTime) affiliation.StopDate).AddDays(1)) > 0)
                                        {
                                            continue;
                                        }

                                        // skip future affiliations
                                        if (affiliation.StartDate != null && DateTime.Compare(now, ((DateTime)affiliation.StartDate).AddDays(-1 * daysBeforeFirstWorkday)) < 0)
                                        {
                                            continue;
                                        }

                                        // if we have reserved Affiliations, we need to do extra checking
                                        if (reservedAffiliations.Count() > 0)
                                        {
                                            // if the affiliations is linked to a single userId, then only map it for that userId
                                            bool reserved = false;
                                            foreach (string employeeId in reservedAffiliations)
                                            {
                                                if (employeeId.Equals(affiliation.EmployeeId))
                                                {
                                                    reserved = true;
                                                }
                                            }

                                            // if the affiliation is RESERVED, and this user does not match, skip
                                            if (reserved && !affiliation.EmployeeId.Equals(user.EmployeeId))
                                            {
                                                continue;
                                            }

                                            // if the affiliation is NOT RESERVED, and the user is linked, then skip
                                            if (!reserved && !string.IsNullOrEmpty(user.EmployeeId))
                                            {
                                                continue;
                                            }
                                        }

                                        string ouUuid = string.IsNullOrEmpty(affiliation.AlternativeOrgunitUuid) ? affiliation.OrgUnitUuid : affiliation.AlternativeOrgunitUuid;
                                        if (ouMap.ContainsKey(ouUuid))
                                        {
                                            ouMap[ouUuid].Users.Add(user.UserId);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // 2.5 map managers into OUs
                    foreach (OrgUnit orgUnit in orgUnits)
                    {
                        if (orgUnit.Manager != null && orgUnit.Manager.Uuid != null)
                        {
                            foreach (Person person in persons)
                            {
                                if (person.Uuid.Equals(orgUnit.Manager.Uuid))
                                {
                                    foreach (User user in person.Users)
                                    {
                                        if (user.UserType.Equals("ACTIVE_DIRECTORY") && user.Prime)
                                        {
                                            orgUnit.Manager.UserId = user.UserId;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // 2.8 expand users
                    PullUpUsers(rootOU);

                    // 3 - read all existing groups
                    logger.LogInformation("Reading existing group data");
                    List<GroupDetails> groups = activeDirectoryService.GetAllGroups(userLocations.ToDictionary(x => x.Value, x => x.Key), useFastMethod);

                    // 4 - iterate over all OUs starting with the root
                    logger.LogInformation("Updating group data");
                    bool success = UpdateGroups(rootOU, groups, userLocations);

                    // 5 - delete any group that does not match an OU (do not perform delete if any updates failed)
                    if (success)
                    {
                        logger.LogInformation("Cleanup group data");
                        deleteGroups(groups);
                    }
                }

                stopWatch.Stop();
                logger.LogInformation($"Finished executing FullSyncJob in {stopWatch.ElapsedMilliseconds / 1000} seconds");
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to execute FullSyncJob");
            }
        }

        private void PullUpUsers(OrgUnit ou)
        {
            // bottom up, so start by pulling up children
            if (ou.Children != null && ou.Children.Count > 0)
            {
                foreach (OrgUnit child in ou.Children)
                {
                    PullUpUsers(child);
                }
            }

            ou.UsersExtended.UnionWith(ou.Users);
            if (ou.Children != null && ou.Children.Count > 0)
            {
                foreach (OrgUnit child in ou.Children)
                {
                    ou.UsersExtended.UnionWith(child.UsersExtended);
                }
            }
        }

        private void deleteGroups(List<GroupDetails> groups)
        {
            foreach (GroupDetails group in groups)
            {
                if (!group.Found)
                {
                    activeDirectoryService.DeleteGroup(group);
                }
            }
        }

        private bool UpdateGroups(OrgUnit ou, List<GroupDetails> groups, Dictionary<string, string> userLocations)
        {
            bool success = true;

            // 4.1 - create or update each group as needed
            // 4.2 - update group members for each group
            try
            {
                bool membersGroup = false;
                bool managerGroup = false;
                bool extendedMembersGroup = false;

                string managerName = TemplateName(ou, settings.ActiveDirectorySettings.GroupSettings.ManagerGroup.Name, 63);
                string managerSamAccountName = TemplateName(ou, settings.ActiveDirectorySettings.GroupSettings.ManagerGroup.SAMaccountName, 63);
                string managerDescription = TemplateName(ou, settings.ActiveDirectorySettings.GroupSettings.ManagerGroup.Description);
                string managerDisplayName = TemplateName(ou, settings.ActiveDirectorySettings.GroupSettings.ManagerGroup.DisplayName, 255);

                string membersName = TemplateName(ou, settings.ActiveDirectorySettings.GroupSettings.DirectMemberGroup.Name, 63);
                string membersSamAccountName = TemplateName(ou, settings.ActiveDirectorySettings.GroupSettings.DirectMemberGroup.SAMaccountName, 63);
                string membersDescription = TemplateName(ou, settings.ActiveDirectorySettings.GroupSettings.DirectMemberGroup.Description);
                string membersDisplayName = TemplateName(ou, settings.ActiveDirectorySettings.GroupSettings.DirectMemberGroup.DisplayName, 255);

                string extMembersName = TemplateName(ou, settings.ActiveDirectorySettings.GroupSettings.MemberGroup.Name, 63);
                string extMembersSamAccountName = TemplateName(ou, settings.ActiveDirectorySettings.GroupSettings.MemberGroup.SAMaccountName, 63);
                string extMembersDescription = TemplateName(ou, settings.ActiveDirectorySettings.GroupSettings.MemberGroup.Description);
                string extMembersDisplayName = TemplateName(ou, settings.ActiveDirectorySettings.GroupSettings.MemberGroup.DisplayName, 255);

                foreach (var group in groups)
                {
                    string[] idTokens = group.Id.Split(":");

                    // ignore groups with invalid ID
                    if (idTokens.Length != 2)
                    {
                        logger.LogInformation("Skipping group because it does not have a valid ID : " + group.Name);
                        continue;
                    }

                    // update scenarios
                    if (idTokens[0].Equals(ou.Uuid))
                    {
                        if (idTokens[1].Equals("manager"))
                        {
                            managerGroup = true;
                            group.Found = true;

                            List<string> managers = new List<string>();
                            if (ou.Manager != null && ou.Manager.UserId != null)
                            {
                                managers.Add(ou.Manager.UserId);
                            }

                            if (settings.ActiveDirectorySettings.GroupSettings.ManagerGroup.Enabled)
                            {
                                activeDirectoryService.UpdateGroup(group, managerName, managerSamAccountName, managerDescription, managerDisplayName, managers, userLocations);
                            }
                        }
                        else if (idTokens[1].Equals("members"))
                        {
                            membersGroup = true;
                            group.Found = true;

                            if (settings.ActiveDirectorySettings.GroupSettings.DirectMemberGroup.Enabled)
                            {
                                activeDirectoryService.UpdateGroup(group, membersName, membersSamAccountName, membersDescription, membersDisplayName, ou.Users, userLocations);
                            }
                        }
                        else if (idTokens[1].Equals("extmembers"))
                        {
                            extendedMembersGroup = true;
                            group.Found = true;

                            if (settings.ActiveDirectorySettings.GroupSettings.MemberGroup.Enabled)
                            {
                                activeDirectoryService.UpdateGroup(group, extMembersName, extMembersSamAccountName, extMembersDescription, extMembersDisplayName, ou.UsersExtended, userLocations);
                            }
                        }
                    }
                }

                // create scenario

                if (!managerGroup)
                {
                    List<string> managers = new List<string>();
                    if (ou.Manager != null && ou.Manager.UserId != null)
                    {
                        managers.Add(ou.Manager.UserId);
                    }

                    if (settings.ActiveDirectorySettings.GroupSettings.ManagerGroup.Enabled)
                    {
                        activeDirectoryService.CreateGroup(managerName, managerDisplayName, managerDescription, managerSamAccountName, ou.Uuid + ":manager", managers, userLocations);
                    }
                }

                if (!extendedMembersGroup)
                {
                    if (settings.ActiveDirectorySettings.GroupSettings.MemberGroup.Enabled)
                    {
                        activeDirectoryService.CreateGroup(extMembersName, extMembersDisplayName, extMembersDescription, extMembersSamAccountName, ou.Uuid + ":extmembers", ou.UsersExtended, userLocations);
                    }
                }

                if (!membersGroup)
                {
                    if (settings.ActiveDirectorySettings.GroupSettings.DirectMemberGroup.Enabled)
                    {
                        activeDirectoryService.CreateGroup(membersName, membersDisplayName, membersDescription, membersSamAccountName, ou.Uuid + ":members", ou.Users, userLocations);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during group creations for " + ou.Name);
                success = false;
            }

            if (ou.Children != null)
            {
                foreach (OrgUnit child in ou.Children)
                {
                    if (child.ShouldBeExcluded)
                    {
                        continue;
                    }

                    success |= UpdateGroups(child, groups, userLocations);
                }
            }

            return success;
        }

        private string TemplateName(OrgUnit ou, string format, int maxLen = 512)
        {
            string value = format
                .Replace("{NAME}", activeDirectoryService.GetNameForOU(ou))
                .Replace("{ID}", ou.MasterId);

            if (value.Length > maxLen)
            {
                value = value.Substring(0, maxLen).Trim();
                logger.LogWarning("Field to long - group attribute trimmed to " + value);
            }

            return value;
        }

        private bool IsActiveAffiliation(Affiliation affiliation)
        {
            DateTime? startDate = affiliation.StartDate;
            DateTime? stopDate = affiliation.StopDate;

            var tomorrow = DateTime.Today.AddDays(1);
            var yesterday = DateTime.Today.AddDays(-1);

            /* nope, this means that we don't move users that are created before they start, which is not right
            var isActive = (!startDate.HasValue || startDate < tomorrow); // check start date
            isActive &= (!stopDate.HasValue || stopDate > yesterday); // check stop date
            */
            var isActive = (!stopDate.HasValue || stopDate > yesterday); // check stop date

            return isActive;
        }

        private void MarkExcludedOrgUnits(List<OrgUnit> orgUnits)
        {
            MarkExcludedOrgUnitsRecursive(orgUnits.Where(o => o.ParentUuid == null).First(), false, orgUnits);
        }

        private void MarkExcludedOrgUnitsRecursive(OrgUnit current, bool parentIsExcluded, List<OrgUnit> allOrgUnits)
        {
            bool shouldBeExcluded = parentIsExcluded || HasExcludedTag(current);
            current.ShouldBeExcluded = shouldBeExcluded;

            foreach (OrgUnit childOu in allOrgUnits.Where(o => o.ParentUuid != null && o.ParentUuid.Equals(current.Uuid)).ToList())
            {
                MarkExcludedOrgUnitsRecursive(childOu, shouldBeExcluded, allOrgUnits);
            }
        }

        private void SetParentReference(List<OrgUnit> orgUnits)
        {
            SetParentReferenceRecursive(orgUnits.Where(o => o.ParentUuid == null).First(), orgUnits);
        }

        private void SetParentReferenceRecursive(OrgUnit current, List<OrgUnit> allOrgUnits)
        {
            foreach (OrgUnit childOu in allOrgUnits.Where(o => o.ParentUuid != null && o.ParentUuid.Equals(current.Uuid)).ToList())
            {
                current.Children.Add(childOu);
                childOu.Parent = current;
                SetParentReferenceRecursive(childOu, allOrgUnits);
            }
        }

        private bool HasExcludedTag(OrgUnit current)
        {
            bool hasTag = false;

            if (current.Tags != null)
            {
                if (current.Tags.Any(t => string.Equals(t.Tag, excludeFromSyncTagName, StringComparison.OrdinalIgnoreCase)))
                {
                    hasTag = true;
                }
            }

            return hasTag;
        }
    }   
}
