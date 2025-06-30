using System.Collections.Generic;

namespace sofd_core_ad_replicator.Services.ActiveDirectory
{
    public class ActiveDirectorySettings
    {
        public string RootOU { get; set; }
        public string RootDeletedOusOu { get; set; }
        public OptionalOUFields OptionalOUFields { get; set; }
        public RequiredOUFields RequiredOUFields { get; set; }
        public bool MoveUsersEnabled { get; set; }
        public bool DryRunMoveUsers { get; set; }
        public bool TestOURun { get; set; }
        public List<string> DontMoveUserRegularExpressions { get; set; }
        public List<string> DontMoveUserFromTheseOUs { get; set; }
        public bool ExcludeExternalUsers { get; set; } = false;
        public string OURunScriptOnCreate { get; set; }
        public string OURunScriptOnDelete { get; set; }
        public string OURunScriptOnMove { get; set; }
        public string UserRunScriptOnMove { get; set; }
        public GroupSettings GroupSettings { get; set; } = new GroupSettings();
    }
}
