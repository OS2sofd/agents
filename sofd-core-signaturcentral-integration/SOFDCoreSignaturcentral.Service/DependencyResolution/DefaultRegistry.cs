using SOFDCoreSignaturcentral.Service.Job;
using StructureMap;
using SOFDCoreSignaturcentral.Service.Backend;
using SOFDCoreSignaturcentral.Service.Signaturcentral;

namespace SOFDCoreSignaturcentral.Service.DependencyResolution
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
            Policies.FillAllPropertiesOfType<BackendService>();
            Policies.FillAllPropertiesOfType<Dao>();
            Policies.Add<LoggingForClassPolicy>();
        }
    }
}
