using Serilog;
using Topshelf;

namespace SOFDCoreSignaturcentral.Service
{
    internal class SOFDCoreSignaturcentralService : ServiceControl
    {
        public ILogger Logger { get; set; }

        public bool Start(HostControl hostControl)
        {
            Logger.Information("SOFDCoreSignaturcentralService starting");
            return true;
        }

        public bool Stop(HostControl hostControl)
        {
            Logger.Information("SOFDCoreSignaturcentralService stopping");
            return true;
        }
    }
}
