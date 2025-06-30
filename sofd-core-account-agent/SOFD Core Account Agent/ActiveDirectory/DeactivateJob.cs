using Active_Directory;
using Serilog;
using SOFD.PAM;
using SOFD_Core;
using SOFD_Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SOFD
{
    public class DeactivateJob
    {
        private static ILogger adLogger = new LoggerConfiguration().ReadFrom.AppSettings().CreateLogger().ForContext(typeof(ActiveDirectoryAccountService));
        private static ILogger log = new LoggerConfiguration().ReadFrom.AppSettings().CreateLogger().ForContext(typeof(DeactivateJob));

        private static string adAttributeCpr = Properties.Settings.Default.ActiveDirectoryAttributeCpr;
        private static string adAttributeEmployeeId = Properties.Settings.Default.ActiveDirectoryAttributeEmployeeId;
        private static List<string> existingAccountExcludeOUs = String.IsNullOrEmpty(Properties.Settings.Default.ExistingAccountExcludeOUs) ? new List<string>() : Properties.Settings.Default.ExistingAccountExcludeOUs.Split(';').ToList();
        private static string ignoredDcPrefix = Properties.Settings.Default.IgnoredDCPrefix;

        private SOFDOrganizationService organizationService;
        private PowershellRunner powershellRunner;

        public DeactivateJob()
        {
            this.organizationService = new SOFDOrganizationService(Properties.Settings.Default.SofdUrl, PAMService.GetApiKey());

            if (!string.IsNullOrEmpty(Properties.Settings.Default.ActiveDirectoryDeactivatePowershell))
            {
                this.powershellRunner = new PowershellRunner(Properties.Settings.Default.ActiveDirectoryDeactivatePowershell);
            }
        }

        public void Execute()
        {
            var response = organizationService.GetPendingOrders("ACTIVE_DIRECTORY", "DEACTIVATE");

            if (response.pendingOrders != null && response.pendingOrders.Count > 0)
            {
                var result = DisableADAccounts(response);

                organizationService.SetOrderStatus("ACTIVE_DIRECTORY", result);
            }
        }

        private List<AccountOrderStatus> DisableADAccounts(AccountOrderResponse response)
        {
            List<AccountOrderStatus> result = new List<AccountOrderStatus>();

            ActiveDirectoryAccountService activeDirectoryService = new ActiveDirectoryAccountService(new ActiveDirectoryConfig() {
                attributeCpr = adAttributeCpr,
                attributeEmployeeId = adAttributeEmployeeId,
                allowEnablingWithoutEmployeeIdMatch = response.singleAccount,
                existingAccountExcludeOUs = new List<String>(), // exclusion should not be used when disabling account
                ignoredDcPrefix = ignoredDcPrefix
            }, adLogger, organizationService);

            foreach (var order in response.pendingOrders)
            {
                AccountOrderStatus status = new AccountOrderStatus();
                status.id = order.id;

                try
                {
                    log.Information("Disabling account for " + order.person.firstname + " " + order.person.surname);

                    var processOrderStatus = activeDirectoryService.ProcessDisableOrder(order);

                    status.status = processOrderStatus.status;
                    status.affectedUserId = processOrderStatus.sAMAccountName;

                    // execute powershell
                    if (this.powershellRunner != null)
                    {
                        string name = order.person.firstname + " " + order.person.surname;
                        string uuid = order.person.uuid;
                        string sAMAccountName = status.affectedUserId;
                        powershellRunner.Run(sAMAccountName, name, uuid, null, processOrderStatus.DC, null, null, null);
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Failed to disable account");
                    status.status = Constants.FAILED;
                    status.message = ex.Message;
                }

                result.Add(status);
            }

            return result;
        }
    }
}
