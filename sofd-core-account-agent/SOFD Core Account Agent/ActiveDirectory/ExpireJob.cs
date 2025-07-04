﻿using Active_Directory;
using Quartz;
using Serilog;
using SOFD.PAM;
using SOFD_Core;
using SOFD_Core.Model;
using SOFD_Core.Model.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SOFD
{
    public class ExpireJob
    {
        private static ILogger adLogger = new LoggerConfiguration().ReadFrom.AppSettings().CreateLogger().ForContext(typeof(ActiveDirectoryAccountService));
        private static ILogger log = new LoggerConfiguration().ReadFrom.AppSettings().CreateLogger().ForContext(typeof(ExpireJob));

        private static string adAttributeCpr = Properties.Settings.Default.ActiveDirectoryAttributeCpr;
        private static List<string> existingAccountExcludeOUs = String.IsNullOrEmpty(Properties.Settings.Default.ExistingAccountExcludeOUs) ? new List<string>() : Properties.Settings.Default.ExistingAccountExcludeOUs.Split(';').ToList();
        private static string ignoredDcPrefix = Properties.Settings.Default.IgnoredDCPrefix;

        private SOFDOrganizationService organizationService;
        private PowershellRunner powershellRunner;


        public ExpireJob()
        {
            this.organizationService = new SOFDOrganizationService(Properties.Settings.Default.SofdUrl, PAMService.GetApiKey());
            if (!string.IsNullOrEmpty(Properties.Settings.Default.ActiveDirectoryExpirePowershell))
            {
                this.powershellRunner = new PowershellRunner(Properties.Settings.Default.ActiveDirectoryExpirePowershell);
            }
        }

        public void Execute()
        {
            var response = organizationService.GetPendingOrders("ACTIVE_DIRECTORY", "EXPIRE");

            if (response.pendingOrders != null && response.pendingOrders.Count > 0)
            {
                var result = ExpireADAccounts(response);

                organizationService.SetOrderStatus("ACTIVE_DIRECTORY", result);
            }
        }

        private List<AccountOrderStatus> ExpireADAccounts(AccountOrderResponse response)
        {
            List<AccountOrderStatus> result = new List<AccountOrderStatus>();

            ActiveDirectoryAccountService activeDirectoryService = new ActiveDirectoryAccountService(new ActiveDirectoryConfig() {
                attributeCpr = adAttributeCpr,
                existingAccountExcludeOUs = new List<String>(), // exclusion should not be used when expiring account
                ignoredDcPrefix = ignoredDcPrefix
            }, adLogger, organizationService);

            foreach (var order in response.pendingOrders)
            {
                AccountOrderStatus status = new AccountOrderStatus();
                status.id = order.id;

                try
                {
                    var processOrderStatus = activeDirectoryService.ProcessExpireOrder(order);

                    log.Information("Setting expireDate=" + order.endDate?.Date + " for " + order.person.firstname + " " + order.person.surname + ": " + order.userId);

                    status.status = processOrderStatus.status;
                    status.affectedUserId = processOrderStatus.sAMAccountName;

                    // Run powershell
                    if (this.powershellRunner != null) 
                    {
                        string name = order.person.firstname + " " + order.person.surname;
                        string uuid = order.person.uuid;
                        string sAMAccountName = status.affectedUserId;
                        string endDate = order.endDate.HasValue ? order.endDate.Value.ToString("yyyy-MM-dd") : null;
                        string domainController = processOrderStatus.DC;
                        powershellRunner.Run(sAMAccountName, name, uuid, null, domainController, null, endDate, null);
                    }
                }
                catch (Exception ex)
                {
                    status.status = Constants.FAILED;
                    status.message = ex.Message;
                }

                result.Add(status);
            }

            return result;
        }
    }
}
