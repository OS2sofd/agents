using System.Collections.Generic;

namespace sofd_core_ad_replicator.Services.ActiveDirectory
{
    public class GroupSettings
    {
        public bool Enabled { get; set; }
        public bool UseFastMethod { get; set; } = false;
        public bool DryRun { get; set; }
        public int DaysBeforeFirstWorkday { get; set; } = 14;
        public string GroupOUDN { get; set; }
        public string GroupIdField { get; set; }
        public GroupTemplateSettings ManagerGroup { get; set; } = new GroupTemplateSettings();
        public GroupTemplateSettings DirectMemberGroup { get; set; } = new GroupTemplateSettings();
        public GroupTemplateSettings MemberGroup { get; set; } = new GroupTemplateSettings();
    }
}
