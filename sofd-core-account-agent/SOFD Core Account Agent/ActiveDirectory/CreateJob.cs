﻿using Active_Directory;
using Serilog;
using SOFD_Core;
using SOFD_Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SOFD
{
    public class CreateJob
    {
        private static ILogger log = new LoggerConfiguration().ReadFrom.AppSettings().CreateLogger().ForContext(typeof(CreateJob));
        private static ILogger adLogger = new LoggerConfiguration().ReadFrom.AppSettings().CreateLogger().ForContext(typeof(ActiveDirectoryAccountService));

        private static string userOU = Properties.Settings.Default.ActiveDirectoryUserOU;
        private static string adAttributeCpr = Properties.Settings.Default.ActiveDirectoryAttributeCpr;
        private static string adAttributeEmployeeId = Properties.Settings.Default.ActiveDirectoryAttributeEmployeeId;
        private static string adInitialPassword = Properties.Settings.Default.ActiveDirectoryInitialPassword;
        private static string uPNChoice = Properties.Settings.Default.UPNChoice;
        private static string defaultUPNDomain = Properties.Settings.Default.DefaultUPNDomain;
        private static string alternativeUPNDomains = Properties.Settings.Default.AlternativeUPNDomains;
        private static List<string> existingAccountExcludeOUs = String.IsNullOrEmpty(Properties.Settings.Default.ExistingAccountExcludeOUs) ? new List<string>() : Properties.Settings.Default.ExistingAccountExcludeOUs.Split(';').ToList();

        private SOFDOrganizationService organizationService;
        private PowershellRunner powershellRunner;

        public CreateJob()
        {
            this.organizationService = new SOFDOrganizationService(Properties.Settings.Default.SofdUrl, Properties.Settings.Default.SofdApiKey);
            if (!string.IsNullOrEmpty(Properties.Settings.Default.ActiveDirectoryCreatePowershell))
            {
                this.powershellRunner = new PowershellRunner(Properties.Settings.Default.ActiveDirectoryCreatePowershell);
            }
        }

        public void Execute()
        {
            var response = organizationService.GetPendingOrders("ACTIVE_DIRECTORY", "CREATE");

            if (response.pendingOrders != null && response.pendingOrders.Count > 0)
            {
                var result = CreateNewADAccounts(response);

                organizationService.SetOrderStatus("ACTIVE_DIRECTORY", result);
            }
        }

        private List<AccountOrderStatus> CreateNewADAccounts(AccountOrderResponse response)
        {
            List<AccountOrderStatus> result = new List<AccountOrderStatus>();
            
            ActiveDirectoryAccountService activeDirectoryService = new ActiveDirectoryAccountService(new ActiveDirectoryConfig()
            {
                attributeCpr = adAttributeCpr,
                attributeEmployeeId = adAttributeEmployeeId,
                userOU = userOU,
                allowEnablingWithoutEmployeeIdMatch = response.singleAccount,
                initialPassword = adInitialPassword,
                uPNChoice = uPNChoice,
                defaultUPNDomain = defaultUPNDomain,
                alternativeUPNDomains = alternativeUPNDomains,
                existingAccountExcludeOUs = existingAccountExcludeOUs
            }, adLogger, organizationService);
            
            foreach (var order in response.pendingOrders)
            {
                AccountOrderStatus status = new AccountOrderStatus();
                status.id = order.id;

                log.Information("Creating/reactivating account for " + order.person.firstname + " " + order.person.surname);

                try
                {
                    var processOrderStatus = activeDirectoryService.ProcessCreateOrder(order);

                    status.status = processOrderStatus.status;
                    status.affectedUserId = processOrderStatus.sAMAccountName;

                    // execute powershell on success
                    if (this.powershellRunner != null && (Constants.REACTIVATED.Equals(processOrderStatus.status) || Constants.CREATED.Equals(processOrderStatus.status)))
                    {
                        string name = order.person.firstname + " " + order.person.surname;
                        string uuid = order.person.uuid;
                        string sAMAccountName = status.affectedUserId;

                        try
                        {
                            log.Information("Invoke powershell with arguments: " + sAMAccountName + ", " + name + ", " + uuid + ", " + processOrderStatus.DC);
                            powershellRunner.Run(sAMAccountName, name, uuid, processOrderStatus.DC);
                        }
                        catch (Exception ex)
                        {
                            log.Warning("Failed to run powershell for " + sAMAccountName, ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Creating account failed");

                    status.status = Constants.FAILED;
                    status.message = ex.Message;
                }

                result.Add(status);
            }

            return result;
        }
    }
}