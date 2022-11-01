using Quartz;
using Serilog;
using SOFDCoreSignaturcentral.Service.Backend;
using SOFDCoreSignaturcentral.Service.Signaturcentral;
using System.Threading.Tasks;

namespace SOFDCoreSignaturcentral.Service.Job
{
    [DisallowConcurrentExecution]
    public class SynchronizeJob : IJob
    {
        public ILogger Logger { get; set; }
        public BackendService BackendService { get; set; }
        public Dao Dao { get; set; }

        public Task Execute(IJobExecutionContext context)
        {
            Logger.Information("Performing full sync");

            try
            {
                var result = Dao.ReadAll();

                BackendService.FullSync(result);
            }
            catch (System.Exception e)
            {
                Logger.Error(e, "Exception caught in SynchronizeJob");
            }

            Logger.Information("Full sync complete");

            return Task.CompletedTask;
        }
    }
}