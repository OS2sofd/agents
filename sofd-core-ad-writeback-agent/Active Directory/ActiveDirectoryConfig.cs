using System.Collections.Generic;

namespace Active_Directory
{
    public class ActiveDirectoryConfig
    {
        public Dictionary<string, string> map { get; set; }
        public List<string> ActiveDirectoryWritebackExcludeOUs { get; set; }
        public List<string> ActiveDirectoryWritebackIncludeOUs { get; set; }
        public bool EnablePowershell { get; set; } = false;
        public bool EnableFallbackToPrimeAffiliation { get; set; }
        public bool DryRun { get; set; }
    }
}
