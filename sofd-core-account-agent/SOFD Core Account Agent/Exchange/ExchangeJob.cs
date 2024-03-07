using Active_Directory;
using Quartz;
using Serilog;
using SOFD_Core;
using SOFD_Core.Model;
using System;
using System.Collections.Generic;

namespace SOFD
{
    [DisallowConcurrentExecution]
    public class ExchangeJob : IJob
    {
        private static ILogger log = new LoggerConfiguration().ReadFrom.AppSettings().CreateLogger().ForContext(typeof(ExchangeJob));
        private static ILogger adLogger = new LoggerConfiguration().ReadFrom.AppSettings().CreateLogger().ForContext(typeof(ActiveDirectoryAttributeService));
        private static ILogger exchangeLogger = new LoggerConfiguration().ReadFrom.AppSettings().CreateLogger().ForContext(typeof(ExchangeService));
        private static bool createEnabled = Properties.Settings.Default.ExchangeCreateEnabled;
        private static bool deactivateEnabled = Properties.Settings.Default.ExchangeDeactivateEnabled;
        private static bool onlyPowershell = Properties.Settings.Default.ExchangeOnlyPowershell;
        private static string defaultDomain = Properties.Settings.Default.ExchangeDefaultMailDomain;
        private static string customDomains = Properties.Settings.Default.ExchangeCustomMailDomains;
        private SOFDOrganizationService organizationService;
        private PowershellRunner createPowershellRunner;
        private PowershellRunner deactivatePowershellRunner;

        public ExchangeJob()
        {
            if (!string.IsNullOrEmpty(Properties.Settings.Default.ExchangeServer) || Properties.Settings.Default.ExchangeOnlyPowershell )
            {
                this.organizationService = new SOFDOrganizationService(Properties.Settings.Default.SofdUrl, Properties.Settings.Default.SofdApiKey);

                if (!string.IsNullOrEmpty(Properties.Settings.Default.ExchangeCreatePowershell))
                {
                    this.createPowershellRunner = new PowershellRunner(Properties.Settings.Default.ExchangeCreatePowershell);
                }

                if (!string.IsNullOrEmpty(Properties.Settings.Default.ExchangeDeactivatePowershell))
                {
                    this.deactivatePowershellRunner = new PowershellRunner(Properties.Settings.Default.ExchangeDeactivatePowershell);
                }
            }
            else
            {
                createEnabled = false;
                deactivateEnabled = false;
            }

            var exchangeService = new ExchangeService(
                Properties.Settings.Default.ExchangeServer,
                Properties.Settings.Default.ExchangeOnlineMailDomain,
                Properties.Settings.Default.ExchangeOnline,
                Properties.Settings.Default.ExchangeUsePSSnapin,
                exchangeLogger);
        }

        public void Execute(IJobExecutionContext context)
        {
            if (createEnabled)
            {
                try
                {
                    var response = organizationService.GetPendingOrders("EXCHANGE", "CREATE");

                    if (response.pendingOrders != null && response.pendingOrders.Count > 0)
                    {
                        var result = EnableExchangeAccounts(response);

                        organizationService.SetOrderStatus("EXCHANGE", result);
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Failed to process Exchange orders");
                }
            }

            if (deactivateEnabled)
            {
                try
                {
                    var response = organizationService.GetPendingOrders("EXCHANGE", new List<string>() { "DEACTIVATE", "DELETE" });

                    if (response.pendingOrders != null && response.pendingOrders.Count > 0)
                    {
                        var result = DeactivateExchangeAccounts(response);

                        organizationService.SetOrderStatus("EXCHANGE", result);
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Failed to process Exchange orders");
                }
            }
        }

        private List<AccountOrderStatus> DeactivateExchangeAccounts(AccountOrderResponse response)
        {
            List<AccountOrderStatus> result = new List<AccountOrderStatus>();

            var exchangeService = new ExchangeService(
                Properties.Settings.Default.ExchangeServer,
                Properties.Settings.Default.ExchangeOnlineMailDomain,
                Properties.Settings.Default.ExchangeOnline,
                Properties.Settings.Default.ExchangeUsePSSnapin,
                exchangeLogger);

            foreach (var order in response.pendingOrders)
            {
                AccountOrderStatus status = new AccountOrderStatus();
                status.id = order.id;

                string name = order.person.firstname + " " + order.person.surname;
                try
                {
                    log.Information("Attempting to delete mailbox '" + order.userId + "'");

                    if (!onlyPowershell)
                    {
                        exchangeService.DisableMailbox(order.userId);
                    }

                    status.affectedUserId = order.userId;
                    status.status = Constants.DEACTIVATED;

                    if (this.deactivatePowershellRunner != null)
                    {
                        try
                        {
                            log.Information("Invoke powershell with arguments: " + order.linkedUserId + ", " + name + ", " + order.person.uuid + ", NULL, " + order.userId);
                            deactivatePowershellRunner.Run(order.linkedUserId, name, order.person.uuid, "", order.userId);
                        }
                        catch (Exception ex)
                        {
                            log.Warning("Failed to run powershell for " + order.userId, ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Warning("Failed to disable mailbox '" + order.userId + "'. ErrorMessage: " + ex.Message);
                    status.status = Constants.FAILED;
                    status.message = ex.Message;
                }

                result.Add(status);
            }

            return result;
        }

        private List<AccountOrderStatus> EnableExchangeAccounts(AccountOrderResponse response)
        {
            List<AccountOrderStatus> result = new List<AccountOrderStatus>();

            var exchangeService = new ExchangeService(
                Properties.Settings.Default.ExchangeServer,
                Properties.Settings.Default.ExchangeOnlineMailDomain,
                Properties.Settings.Default.ExchangeOnline,
                Properties.Settings.Default.ExchangeUsePSSnapin,
                exchangeLogger);

            var activeDirectoryService = new ActiveDirectoryAttributeService(new ActiveDirectoryConfig(), adLogger);

            foreach (var order in response.pendingOrders)
            {
                AccountOrderStatus status = new AccountOrderStatus();
                status.id = order.id;

                string name = order.person.firstname + " " + order.person.surname;
                string emailAlias = order.userId + GetMailDomain(order.person.uuid);
                try
                {
                    log.Information("Attempting to create mailbox '" + emailAlias + "' for AD account " + order.linkedUserId);

                    if (!onlyPowershell)
                    {
                        if (exchangeService.MailboxExists(emailAlias))
                        {
                            throw new Exception($"Mailbox {emailAlias} not enabled because it already exists");
                        }
                        var wrapper = activeDirectoryService.GetBySAMAccountName(order.linkedUserId);
                        exchangeService.EnableMailbox(order.linkedUserId, emailAlias, wrapper.DC);
                    }

                    // upon success, update the AD account, so the UPN matches the email alias                    
                    Boolean updateUserPrincipalName = Properties.Settings.Default.UPNChoice == "EXCHANGE" || Properties.Settings.Default.UPNChoice == "BOTH" ||  String.IsNullOrEmpty(Properties.Settings.Default.UPNChoice);
                    string dc = activeDirectoryService.SetUserPrincipleNameAndNickname(order.linkedUserId, emailAlias, updateUserPrincipalName);

                    status.affectedUserId = emailAlias;
                    status.status = Constants.CREATED;

                    if (this.createPowershellRunner != null)
                    {
                        try
                        {
                            log.Information("Invoke powershell with arguments: " + order.linkedUserId + ", " + name + ", " + order.person.uuid + ", " + dc + ", " + emailAlias);
                            createPowershellRunner.Run(order.linkedUserId, name, order.person.uuid, emailAlias, dc);
                        }
                        catch (Exception ex)
                        {
                            log.Warning("Failed to run powershell for " + order.userId, ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "Failed to enable mailbox for " + order.userId + " with alias " + emailAlias + ". ErrorMessage: " + ex.Message);
                    status.status = Constants.FAILED;
                    status.message = ex.Message;
                }

                result.Add(status);
            }

            return result;
        }

        private string GetMailDomain(string uuid)
        {
            if (string.IsNullOrEmpty(customDomains))
            {
                return defaultDomain;
            }

            Person person = organizationService.GetPerson(uuid);
            if (person == null)
            {
                log.Error("Could not find person with uuid in SOFD - using deafult mailDomain: " + uuid);

                return defaultDomain;
            }

            // expected format: 205e4f88-6861-4731-916b-3b0ddb2b22f4=@kommune2.dk;c426e229-1dae-4d4c-ab3f-82581f17c848=@kommune3.dk
            string[] customDomainMappings = customDomains.Split(';');

            if (person.affiliations != null)
            {
                foreach (Affiliation affiliation in person.affiliations)
                {
                    if (affiliation.prime)
                    {
                        foreach (string customDomainMapping in customDomainMappings)
                        {
                            if (customDomainMapping.StartsWith(affiliation.orgUnitUuid.ToString()))
                            {
                                string[] tokens = customDomainMapping.Split('=');
                                if (tokens.Length >= 2)
                                {
                                    return tokens[1];
                                }
                            }
                        }

                        break;
                    }
                }
            }

            return defaultDomain;
        }
    }
}