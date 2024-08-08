using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using sofd_core_ad_replicator.Services.ActiveDirectory;
using sofd_core_ad_replicator.Services.Sofd.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace sofd_core_ad_replicator.Services.Sofd
{
    internal class SyncService : ServiceBase<SyncService>
    {
        private readonly ActiveDirectoryService activeDirectoryService;
        private readonly SofdService sofdService;

        private readonly string excludeFromSyncTagName;
        private readonly bool moveUsersEnabled;
        private readonly List<string> dontMoveUserRegularExpressions;

        public SyncService(IServiceProvider sp) : base(sp)
        {
            activeDirectoryService = sp.GetService<ActiveDirectoryService>();
            sofdService = sp.GetService<SofdService>();

            excludeFromSyncTagName = settings.SofdSettings.ExcludeFromSyncTagName;
            moveUsersEnabled = settings.ActiveDirectorySettings.MoveUsersEnabled;
            dontMoveUserRegularExpressions = settings.ActiveDirectorySettings.DontMoveUserRegularExpressions;
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
                logger.LogInformation("Updating OUs");
                var orgUnitMap = activeDirectoryService.HandleOrgUnits(orgUnits);

                if (moveUsersEnabled)
                {
                    logger.LogInformation("Updating Users");

                    var userLocations = activeDirectoryService.GetUserLocations();

                    List<Person> people = sofdService.GetPersons();
                    logger.LogInformation("Found " + people.Count + " potential users in SOFD");

                    foreach (Person person in people)
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
                                activeDirectoryService.CheckForMove(person, user, activeAffiliations, userLocations, orgUnitMap);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Failed to move " + user.UserId);
                            }
                        }
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
