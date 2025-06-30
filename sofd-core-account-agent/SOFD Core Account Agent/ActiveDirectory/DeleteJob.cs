using Active_Directory;
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
    public class DeleteJob
    {
        private static ILogger adLogger = new LoggerConfiguration().ReadFrom.AppSettings().CreateLogger().ForContext(typeof(ActiveDirectoryAccountService));
        private static ILogger log = new LoggerConfiguration().ReadFrom.AppSettings().CreateLogger().ForContext(typeof(DeleteJob));

        private static string adAttributeCpr = Properties.Settings.Default.ActiveDirectoryAttributeCpr;
        private static string adAttributeEmployeeId = Properties.Settings.Default.ActiveDirectoryAttributeEmployeeId;
        private static List<string> existingAccountExcludeOUs = String.IsNullOrEmpty(Properties.Settings.Default.ExistingAccountExcludeOUs) ? new List<string>() : Properties.Settings.Default.ExistingAccountExcludeOUs.Split(';').ToList();
        private static string ignoredDcPrefix = Properties.Settings.Default.IgnoredDCPrefix;
        private static bool powershellFirst = Properties.Settings.Default.ActiveDirectoryDeletePowershellBeforeDelete;

        private SOFDOrganizationService organizationService;
        private PowershellRunner powershellRunner;

        public DeleteJob()
        {
            this.organizationService = new SOFDOrganizationService(Properties.Settings.Default.SofdUrl, PAMService.GetApiKey());

            if (!string.IsNullOrEmpty(Properties.Settings.Default.ActiveDirectoryDeletePowershell))
            {
                this.powershellRunner = new PowershellRunner(Properties.Settings.Default.ActiveDirectoryDeletePowershell);
            }
        }

        public void Execute()
        {
            var response = organizationService.GetPendingOrders("ACTIVE_DIRECTORY", "DELETE");

            if (response.pendingOrders != null && response.pendingOrders.Count > 0)
            {
                var result = DeleteADAccounts(response);

                organizationService.SetOrderStatus("ACTIVE_DIRECTORY", result);
            }
        }

        private List<AccountOrderStatus> DeleteADAccounts(AccountOrderResponse response)
        {
            List<AccountOrderStatus> result = new List<AccountOrderStatus>();

            ActiveDirectoryAccountService activeDirectoryService = new ActiveDirectoryAccountService(new ActiveDirectoryConfig() {
                attributeCpr = adAttributeCpr,
                attributeEmployeeId = adAttributeEmployeeId,
                allowEnablingWithoutEmployeeIdMatch = response.singleAccount,
                existingAccountExcludeOUs = new List<String>(), // exclusion should not be used when deleting account
                ignoredDcPrefix = ignoredDcPrefix
            }, adLogger, organizationService);

            foreach (var order in response.pendingOrders)
            {
                AccountOrderStatus status = new AccountOrderStatus();
                status.id = order.id;

                try
                {
                    log.Information("Deleting account for " + order.person.firstname + " " + order.person.surname + ": " + order.userId);

                    // execute powershell before actually deleting account (DC will not be available)
                    if (powershellFirst)
                    {
                        if (this.powershellRunner != null)
                        {
                            string name = order.person.firstname + " " + order.person.surname;
                            string uuid = order.person.uuid;
                            string sAMAccountName = order.userId;
                            powershellRunner.Run(sAMAccountName, name, uuid, null, "NA", null, null, null);
                        }
                    }

                    var processOrderStatus = activeDirectoryService.ProcessDeleteOrder(order);

                    status.status = processOrderStatus.status;
                    status.affectedUserId = processOrderStatus.sAMAccountName;

                    // execute powershell after actually deleting account (DC will be available)
                    if (!powershellFirst)
                    {
                        if (this.powershellRunner != null)
                        {
                            string name = order.person.firstname + " " + order.person.surname;
                            string uuid = order.person.uuid;
                            string sAMAccountName = status.affectedUserId;
                            powershellRunner.Run(sAMAccountName, name, uuid, null, processOrderStatus.DC, null, null, null);
                        }
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
