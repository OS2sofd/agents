using Serilog;
using Topshelf;

namespace SOFDCoreAD.Service
{
    internal class SOFDCoreADService : ServiceControl
    {
        public ILogger Logger { get; set; }

        public bool Start(HostControl hostControl)
        {
            Logger.Information("SOFDCoreADService starting");
            return true;
        }

        public bool Stop(HostControl hostControl)
        {
            Logger.Information("SOFDCoreADService stopping");
            return true;
        }
    }
}
