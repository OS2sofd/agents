using Quartz;
using Serilog;
using System.Threading.Tasks;

namespace SOFDCoreAD.Service.Job
{
    public class ResetDirSyncJob : IJob
    {
        public ILogger Logger { get; set; }

        public Task Execute(IJobExecutionContext context)
        {
            Logger.Debug("Resetting DirSync cookie");

            SynchronizeJob.ResetDirSync();

            return Task.CompletedTask;
        }
    }
}
