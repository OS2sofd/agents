using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using SOFD_Core.Model;
using Serilog;
using SOFD_Core;
using System.Text.RegularExpressions;

namespace Active_Directory
{
    public class ActiveDirectoryAccountService : ActiveDirectoryServiceBase
    {
        private bool allowEnablingWithoutEmployeeIdMatch = false;
        private string uPNChoice = "EXCHANGE";
        private string defaultUPNDomain = "";
        private string alternativeUPNDomains = "";
        private SOFDOrganizationService organizationService;
        private Regex rx = new Regex("^vik\\d{4}$");
        private bool failReactivateOnMultipleDisabled = false;


        public ActiveDirectoryAccountService(ActiveDirectoryConfig config, ILogger log, SOFDOrganizationService organizationService) : base(config, log)
        {
            this.allowEnablingWithoutEmployeeIdMatch = config.allowEnablingWithoutEmployeeIdMatch;
            this.uPNChoice = config.uPNChoice;
            this.defaultUPNDomain = config.defaultUPNDomain;
            this.alternativeUPNDomains = config.alternativeUPNDomains;
            this.organizationService = organizationService;
            this.failReactivateOnMultipleDisabled = config.failReactivateOnMultipleDisabled;
        }

        public ProcessStatus ProcessDisableOrder(AccountOrder order)
        {
            ProcessStatus response = new ProcessStatus();

            if (string.IsNullOrEmpty(order.userId))
            {
                throw new Exception("Ingen brugerkonto angivet");
            }

            AccountStatus status = GetAccountStatusBySAMAccountName(order.userId);
            if (status != null)
            {
                response = DisableAccount(status);
                if (response == null)
                {
                    throw new Exception("Kunne ikke disable brugerkontoen: " + order.userId);
                }
            }
            else
            {
                throw new Exception("Brugerkontoen findes ikke: " + order.userId);
            }

            return response;
        }

        public ProcessStatus ProcessDeleteOrder(AccountOrder order)
        {
            if (string.IsNullOrEmpty(order.userId))
            {
                throw new Exception("Ingen brugerkonto angivet");
            }

            ProcessStatus response = DeleteAccount(order.userId);
            if (response == null)
            {
                throw new Exception("Kunne ikke slette brugerkontoen: " + order.userId);
            }

            return response;
        }

        public ProcessStatus ProcessExpireOrder(AccountOrder order)
        {
            if (string.IsNullOrEmpty(order.userId))
            {
                throw new Exception("Ingen brugerkonto angivet");
            }

            ProcessStatus response = ExpireAccount(order.userId, order.endDate);
            if (response == null)
            {
                throw new Exception("Kunne ikke sætte udløbsdato på brugerkontoen: " + order.userId);
            }

            return response;
        }

        public ProcessStatus ProcessCreateOrder(AccountOrder order)
        {
            // should we create a new account?
            bool createNewAccount = true;

            // find all existing accounts with the supplied cpr (twice, one with and one without dash)
            List<AccountStatus> existingAccounts = GetAccountStatiByCpr(order.userId, order.person.cpr);
            List<AccountStatus> extraExistingAccounts = GetAccountStatiByCpr(order.userId, GetCprWithDash(order.person.cpr));
            existingAccounts.AddRange(extraExistingAccounts);

            foreach (var existingAccount in existingAccounts)
            {
                // skip any account with a username like vikXXXX as those are substitute accounts
                string existingAccountUserId = existingAccount.sAMAccountName.ToLower();
                if (rx.IsMatch(existingAccountUserId))
                {
                    continue;
                }

                // if an employeeId was supplied, see if an account exist with this employeeId
                if (!allowEnablingWithoutEmployeeIdMatch && !string.IsNullOrEmpty(order.person.employeeId))
                {
                    if (order.person.employeeId.Equals(existingAccount.employeeId))
                    {
                        if (existingAccount.disabled)
                        {
                            createNewAccount = false;
                        }
                        else
                        {
                            // when matching accounts with employeeIds, only one account is allowed
                            throw new Exception("Der er en AD konto til ansættelsen (" + order.person.employeeId + ") allerede: " + existingAccount.sAMAccountName);
                        }
                    }
                }
                else // ordinary scenario
                {
                    // if an existing account is disabled, reenable that instead
                    if (existingAccount.disabled)
                    {
                        createNewAccount = false;
                    }
                }
            }

            if (createNewAccount)
            {
                return CreateAccount(order);
            }
            
            return EnableAccount(order, existingAccounts);
        }

        private string GetCprWithDash(string cpr)
        {
            if (cpr.Length != 10)
            {
                return cpr;
            }

            return cpr.Substring(0, 6) + "-" + cpr.Substring(6, 4);
        }

        private ProcessStatus EnableAccount(AccountOrder order, List<AccountStatus> existingAccounts)
        {
            ProcessStatus result = new ProcessStatus();
            result.status = Constants.REACTIVATED;

            // if there are multiple disabled AD accounts, some municipalies do not want to just pick a random one
            // to reactivate - instead they want a failed create, so they can manually pick which one to use
            if (failReactivateOnMultipleDisabled)
            {
                // count disabled accounts
                List<string> disabledAccounts = new List<string>();
                foreach (var existingAccount in existingAccounts)
                {
                    // skip any account with a username like vikXXXX as those are substitute accounts
                    string existingAccountUserId = existingAccount.sAMAccountName.ToLower();
                    if (rx.IsMatch(existingAccountUserId))
                    {
                        continue;
                    }

                    if (existingAccount.disabled)
                    {
                        disabledAccounts.Add(existingAccount.sAMAccountName);
                    }
                }

                // abort if there are more than 1 - some human must decide which one to reactivate
                if (disabledAccounts.Count > 1)
                {
                    throw new Exception("Kan ikke reaktivere en AD konto - der er flere spærrede AD konto at vælge mellem: " + string.Join(",", disabledAccounts));
                }
            }

            foreach (var existingAccount in existingAccounts)
            {
                // skip any account with a username like vikXXXX as those are substitute accounts
                string existingAccountUserId = existingAccount.sAMAccountName.ToLower();
                if (rx.IsMatch(existingAccountUserId))
                {
                    continue;
                }

                // attempt to re-enable disabled accounts
                if (existingAccount.disabled)
                {
                    // if and employeeId was supplied, only reactivate accounts that matches this employeeId
                    if (!string.IsNullOrEmpty(order.person.employeeId) && !order.person.employeeId.Equals(existingAccount.employeeId))
                    {
                        continue;
                    }

                    result.DC = EnableAccount(existingAccount, order);
                    result.sAMAccountName = existingAccount.sAMAccountName;

                    break;
                }
            }

            if (string.IsNullOrEmpty(result.sAMAccountName))
            {
                // if the config allows re-activating without employeeId match, and there is at least one
                // disabled account, then we re-activate that one
                if (allowEnablingWithoutEmployeeIdMatch)
                {
                    foreach (var existingAccount in existingAccounts)
                    {
                        // skip any account with a username like vikXXXX as those are substitute accounts
                        string existingAccountUserId = existingAccount.sAMAccountName.ToLower();
                        if (rx.IsMatch(existingAccountUserId))
                        {
                            continue;
                        }

                        if (existingAccount.disabled)
                        {
                            result.DC = EnableAccount(existingAccount, order);
                            result.sAMAccountName = existingAccount.sAMAccountName;

                            break;
                        }
                    }
                }

                // everything failed
                if (string.IsNullOrEmpty(result.sAMAccountName))
                {
                    throw new Exception("Der var hverken muligt at oprette elle reaktivere en AD konto");
                }
            }

            return result;
        }

        private ProcessStatus DisableAccount(AccountStatus status)
        {
            var wrapper = GetBySAMAccountName(status.sAMAccountName);
            using (var de = wrapper.Entry)
            {
                if (de != null)
                {
                    int flags = (int)de.Properties["useraccountcontrol"].Value;
                    de.Properties["useraccountcontrol"].Value = (flags | 0x0002);
                    de.CommitChanges();

                    return new ProcessStatus()
                    {
                        status = Constants.DEACTIVATED,
                        sAMAccountName = status.sAMAccountName,
                        DC = wrapper.DC
                    };
                }

                return null;
            }
        }

        private ProcessStatus ExpireAccount(string userId, DateTime? endDate)
        {
            // hack to pick a DC to expire against
            var wrapper = GenerateDirectoryEntry();
            using (wrapper.Entry)
            {
                log.Information("Attempting to expire AD account: " + userId + " through " + wrapper.DC);
            }

            using (PrincipalContext ctx = new PrincipalContext(ContextType.Domain, wrapper.DC))
            {
                UserPrincipal user = UserPrincipal.FindByIdentity(ctx, userId);
                if (user != null)
                {
                    if (endDate != null)
                    {
                        DateTime dt = (DateTime)(endDate);
                        DateTime actualExpire = new DateTime(dt.Year, dt.Month, dt.Day, 3, 0, 0); // ensure timezone stuff doesn't hurt us

                        user.AccountExpirationDate = actualExpire;
                    }
                    else
                    {
                        user.AccountExpirationDate = null;
                    }

                    user.Save();

                    return new ProcessStatus()
                    {
                        status = Constants.EXPIRED,
                        sAMAccountName = userId,
                        DC = wrapper.DC
                    };
                }
            }

            return null;
        }

        private ProcessStatus DeleteAccount(string userId)
        {
            // hack to pick a DC to create against
            string dc = null;
            var wrapper = GenerateDirectoryEntry();
            using (wrapper.Entry)
            {
                dc = wrapper.DC;
                log.Information("Attempting to delete AD account: " + userId + " through " + wrapper.DC);
            }

            using (PrincipalContext ctx = new PrincipalContext(ContextType.Domain, dc))
            {
                UserPrincipal user = UserPrincipal.FindByIdentity(ctx, userId);
                if (user != null)
                {
                    var entry = user.GetUnderlyingObject() as DirectoryEntry;
                    entry.DeleteTree();

                    return new ProcessStatus()
                    {
                        status = Constants.DELETED,
                        sAMAccountName = userId,
                        DC = dc
                    };
                }
            }

            return null;
        }

        private ProcessStatus CreateAccount(AccountOrder order)
        {
            ProcessStatus result = new ProcessStatus();

            string sAMAccountName = order.userId;
            if (string.IsNullOrEmpty(sAMAccountName))
            {
                result.status = Constants.FAILED;

                return result;
            }

            // hack to pick a DC to create against
            var wrapper = GenerateDirectoryEntry();
            using (wrapper.Entry)
            {
                result.DC = wrapper.DC;
                log.Information("Attempting to create AD account: " + sAMAccountName + " through " + wrapper.DC);
            }

            using (PrincipalContext ctx = new PrincipalContext(ContextType.Domain, wrapper.DC, config.userOU))
            {
                bool failed = true;

                using (var newUser = new UserPrincipal(ctx))
                {
                    newUser.Name = order.person.firstname + " " + order.person.surname + " (" + sAMAccountName + ")";
                    newUser.GivenName = order.person.firstname;
                    newUser.Surname = order.person.surname;
                    newUser.SamAccountName = sAMAccountName;
                    newUser.DisplayName = order.person.firstname + " " + order.person.surname;
                    newUser.SetPassword(string.IsNullOrEmpty(config.initialPassword) ? Guid.NewGuid().ToString() : config.initialPassword);

                    newUser.AccountExpirationDate = order.endDate;
                    newUser.ExpirePasswordNow();
                    newUser.PasswordNotRequired = false;
                    newUser.Enabled = true;

                    if (uPNChoice == "AD" || uPNChoice == "BOTH")
                    {
                        Person person = organizationService.GetPerson(order.person.uuid);
                        if (person == null)
                        {
                            log.Error("Could not find person with uuid in SOFD: " + order.person.uuid);
                        }
                        
                        newUser.UserPrincipalName = sAMAccountName + defaultUPNDomain;
                        if (person?.affiliations != null && person.affiliations.Count > 0)
                        {
                            List<string> alternatives = new List<string>(alternativeUPNDomains.Split(';')); 
                            if (alternatives.Count > 0)
                            {
                                if (!string.IsNullOrEmpty(order.person.employeeId))
                                {
                                    foreach (Affiliation affiliation in person.affiliations)
                                    {
                                        if (affiliation.employeeId.Equals(order.person.employeeId))
                                        {
                                            foreach (string alternativeUPNDomain in alternatives)
                                            {
                                                if (alternativeUPNDomain.Contains(affiliation.orgUnitUuid.ToString()))
                                                {
                                                    string domain = alternativeUPNDomain.Split('=')[1];
                                                    newUser.UserPrincipalName = sAMAccountName + domain;
                                                    break;
                                                }
                                            }
                                        }
                                    }

                                }
                                else
                                {
                                    foreach (Affiliation affiliation in person.affiliations)
                                    {
                                        if (affiliation.prime)
                                        {
                                            foreach (string alternativeUPNDomain in alternatives)
                                            {
                                                if (alternativeUPNDomain.Contains(affiliation.orgUnitUuid.ToString()))
                                                {
                                                    string domain = alternativeUPNDomain.Split('=')[1];
                                                    newUser.UserPrincipalName = sAMAccountName + domain;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        log.Debug($"Setting UserPrincipalName to {newUser.UserPrincipalName}");
                    }

                    newUser.Save();

                    using (DirectoryEntry de = newUser.GetUnderlyingObject() as DirectoryEntry)
                    {
                        if (de != null)
                        {
                            de.Properties[config.attributeCpr].Value = order.person.cpr;

                            if (!string.IsNullOrEmpty(config.attributeEmployeeId) && !string.IsNullOrEmpty(order.person.employeeId))
                            {
                                de.Properties[config.attributeEmployeeId].Value = order.person.employeeId;
                            }

                            de.CommitChanges();

                            failed = false;
                        }
                    }
                }

                if (failed)
                {
                    using (UserPrincipal user = UserPrincipal.FindByIdentity(ctx, sAMAccountName))
                    {
                        if (user != null)
                        {
                            user.Delete();
                        }
                    }

                    throw new Exception("Ikke muligt at registrere CPR på AD konto!");
                }
            }

            result.status = Constants.CREATED;
            result.sAMAccountName = sAMAccountName;

            return result;
        }

        private string EnableAccount(AccountStatus status, AccountOrder order)
        {
            log.Information("Attempting to re-enable AD account: " + status.sAMAccountName);

            var wrapper = GetBySAMAccountName(status.sAMAccountName);
            using (var de = wrapper.Entry)
            {
                if (de != null)
                {
                    int flags = (int)de.Properties["useraccountcontrol"].Value;
                    de.Properties["useraccountcontrol"].Value = (flags & ~0x0002);
                    if (order.endDate.HasValue)
                    {
                        de.Properties["accountExpires"].Value = order.endDate.Value.ToFileTime().ToString();
                    }
                    else
                    {
                        de.Properties["accountExpires"].Value = 0;
                    }

                    if (!string.IsNullOrEmpty(config.attributeEmployeeId) && !string.IsNullOrEmpty(order.person.employeeId))
                    {
                        de.Properties[config.attributeEmployeeId].Value = order.person.employeeId;
                    }

                    if (!string.IsNullOrEmpty(config.initialPassword)) {
                        de.Invoke("SetPassword", new object[] { config.initialPassword });
                    }

                    de.CommitChanges();
                }
            }

            return wrapper.DC;
        }
    }
}
