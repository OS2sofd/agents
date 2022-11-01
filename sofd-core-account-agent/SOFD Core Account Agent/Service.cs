using Quartz;
using Quartz.Impl;
using Serilog;
using System;

namespace SOFD
{
    class Service
    {
        private static ILogger log = new LoggerConfiguration().ReadFrom.AppSettings().CreateLogger().ForContext(typeof(Service));
        private IScheduler sched;
   
        public Service()
        {
            ISchedulerFactory schedFact = new StdSchedulerFactory();
            sched = schedFact.GetScheduler();
        }

        public void Start() {
            log.Information("Service Started.");

            try
            {
                ConfigurationUploader.UploadConfiguration();
            }
            catch (Exception ex)
            {
                log.Error(ex, "Failed to upload configuration");
            }
            
            // Run according to config (default run every 5 minutes, at 00, 05, 10, etc)
            StartJob<ActiveDirectoryJob>("ActiveDirectoryJob", Properties.Settings.Default.ActiveDirectoryJobCron);

            // Run according to config (default run every 5 minutes at 01, 06, 11, etc)
            StartJob<ExchangeJob>("ExchangeJob", Properties.Settings.Default.ExchangeJobCron);

            sched.Start();
        }

        public void Stop() {
            log.Information("Service Stopped.");

            sched.Shutdown();
        }

        private void StartJob<T>(string name, string cron) where T : IJob
        {
            IJobDetail job = JobBuilder.Create<T>()
                .WithIdentity(name + "Job", "group1")
                .Build();

            ITrigger interval = TriggerBuilder.Create()
              .WithIdentity(name + "Interval", "group1")
              .WithCronSchedule(cron)
              .Build();

            sched.ScheduleJob(job, interval);

            sched.TriggerJob(job.Key);
        }
    }
}
