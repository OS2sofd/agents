using Quartz;
using Serilog;
using System;

namespace SOFD
{
    [DisallowConcurrentExecution]
    public class ActiveDirectoryJob : IJob
    {
        private static ILogger log = new LoggerConfiguration().ReadFrom.AppSettings().CreateLogger().ForContext(typeof(ActiveDirectoryJob));
        private CreateJob createJob;
        private DeactivateJob deactivateJob;
        private DeleteJob deleteJob;
        private ExpireJob expireJob;

        public ActiveDirectoryJob()
        {
            if (Properties.Settings.Default.ActiveDirectoryEnableAccountCreation)
            {
                createJob = new CreateJob();
            }

            if (Properties.Settings.Default.ActiveDirectoryEnableAccountDeactivation)
            {
                deactivateJob = new DeactivateJob();
            }

            if (Properties.Settings.Default.ActiveDirectoryEnableAccountDeletion)
            {
                deleteJob = new DeleteJob();
            }

            if (Properties.Settings.Default.ActiveDirectoryEnableAccountExpire)
            {
                expireJob = new ExpireJob();
            }
        }

        public void Execute(IJobExecutionContext context)
        {
            if (Properties.Settings.Default.ActiveDirectoryEnableAccountCreation)
            {
                try
                {
                    createJob.Execute();
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Failed to process create orders");
                }
            }

            if (Properties.Settings.Default.ActiveDirectoryEnableAccountDeactivation)
            {
                try
                {
                    deactivateJob.Execute();
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Failed to process deactivate orders");
                }
            }

            if (Properties.Settings.Default.ActiveDirectoryEnableAccountDeletion)
            {
                try
                {
                    deleteJob.Execute();
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Failed to process delete orders");
                }
            }

            if (Properties.Settings.Default.ActiveDirectoryEnableAccountExpire)
            {
                try
                {
                    expireJob.Execute();
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Failed to process expire orders");
                }
            }
        }
    }
}