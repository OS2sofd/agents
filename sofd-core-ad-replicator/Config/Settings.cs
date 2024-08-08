using sofd_core_ad_replicator.Jobs;
using sofd_core_ad_replicator.Services.ActiveDirectory;
using sofd_core_ad_replicator.Services.PAM;
using sofd_core_ad_replicator.Services.Sofd;

namespace sofd_core_ad_replicator.Config
{
    public class Settings
    {
        public JobSettings JobSettings { get; set; } = new JobSettings();
        public ActiveDirectorySettings ActiveDirectorySettings { get; set; }
        public SofdSettings SofdSettings { get; set; }

        public PAMSettings PAMSettings { get; set; }
    }
}
