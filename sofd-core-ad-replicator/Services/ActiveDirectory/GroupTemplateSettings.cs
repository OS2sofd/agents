using System.Collections.Generic;

namespace sofd_core_ad_replicator.Services.ActiveDirectory
{
    public class GroupTemplateSettings
    {
        public bool Enabled { get; set; }
        public string Name { get; set; }
        public string SAMaccountName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
    }
}
