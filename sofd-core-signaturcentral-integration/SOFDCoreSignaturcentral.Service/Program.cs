using System;
using StructureMap;
using Topshelf;
using Topshelf.StructureMap;
using Topshelf.Quartz.StructureMap;
using System.Net;
using Quartz;
using SOFDCoreSignaturcentral.Service.DependencyResolution;
using SOFDCoreSignaturcentral.Service.Job;

namespace SOFDCoreSignaturcentral.Service
{
    class Program
    {
        static int hour = 6;
        static int minute = 0;

        static void Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // setting Environment directory to store logs to relative paths correctly
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            InitCronSchedule();

            try
            {
                ConfigurationUploader.UploadConfiguration();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to upload configuration: " + ex.Message);
            }

            HostFactory.Run(h =>
            {
                h.UseStructureMap(new Container(c =>
                {
                    c.AddRegistry(new DefaultRegistry());
                }));
                h.SetDisplayName("SOFD Core Signaturcentral Dispatcher");
                h.SetServiceName("SOFD Core Signaturcentral Dispatcher");
                h.Service<SOFDCoreSignaturcentralService>(s =>
                {
                    s.ConstructUsingStructureMap();
                    s.UseQuartzStructureMap()
                        .ScheduleQuartzJob(q =>
                            q.WithJob(() => JobBuilder.Create<SynchronizeJob>().Build())
                                .AddTrigger(() => TriggerBuilder.Create()
                                .WithCronSchedule("0 " + minute + " " + hour + " ? * * *")
                            .Build()))
                        .ScheduleQuartzJob(q =>
                            q.WithJob(() => JobBuilder.Create<SynchronizeJob>().Build())
                                .AddTrigger(() => TriggerBuilder.Create()
                                .StartNow()
                            .Build()));
                });
            });
        }

        private static void InitCronSchedule()
        {
            // use password as random seed
            string tmp = Settings.GetStringValue("Backend.Password");
            if (!string.IsNullOrEmpty(tmp) && tmp.Length >= 2)
            {
                int x = (int) tmp[0];
                int y = (int) tmp[1];

                hour = (x % 2 == 0) ? 5 : 6;
                minute = (y % 30);

                if (hour == 5) { minute += 30; }
            }
        }
    }
}
