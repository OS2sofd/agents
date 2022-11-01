using Active_Directory;
using Quartz;
using Serilog;
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

        private SOFDOrganizationService organizationService;
        private PowershellRunner powershellRunner;

        public DeleteJob()
        {
            this.organizationService = new SOFDOrganizationService(Properties.Settings.Default.SofdUrl, Properties.Settings.Default.SofdApiKey);

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
                existingAccountExcludeOUs = existingAccountExcludeOUs
            }, adLogger, organizationService);

            foreach (var order in response.pendingOrders)
            {
                AccountOrderStatus status = new AccountOrderStatus();
                status.id = order.id;

                try
                {
                    var processOrderStatus = activeDirectoryService.ProcessDeleteOrder(order);

                    log.Information("Deleting account for " + order.person.firstname + " " + order.person.surname + ": " + order.userId);

                    status.status = processOrderStatus.status;
                    status.affectedUserId = processOrderStatus.sAMAccountName;

                    // execute powershell
                    if (this.powershellRunner != null)
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
                    status.status = Constants.FAILED;
                    status.message = ex.Message;
                }

                result.Add(status);
            }

            return result;
        }
    }
}
