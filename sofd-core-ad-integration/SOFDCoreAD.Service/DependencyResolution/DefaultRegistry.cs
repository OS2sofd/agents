using Serilog;
using SOFDCoreAD.Service.Job;
using SOFDCoreAD.Service.ActiveDirectory;
using StructureMap;
using SOFDCoreAD.Service.Backend;
using SOFDCoreAD.Service.Photo;

namespace SOFDCoreAD.Service.DependencyResolution
{
    public class DefaultRegistry : Registry
    {
        public DefaultRegistry()
        {
            Scan(_ =>
            {
                _.WithDefaultConventions();
            });

            // singleton required to persist dirsync cookie
            For<SynchronizeJob>().Singleton();

            // policies
            Policies.FillAllPropertiesOfType<ActiveDirectoryService>();
            Policies.FillAllPropertiesOfType<BackendService>();
            Policies.FillAllPropertiesOfType<PhotoHashRepository>();
            Policies.Add<LoggingForClassPolicy>();
        }
    }
}
