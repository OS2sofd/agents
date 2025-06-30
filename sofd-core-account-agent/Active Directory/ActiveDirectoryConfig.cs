using System.Collections.Generic;

namespace Active_Directory
{
    public class ActiveDirectoryConfig
    {
        public Dictionary<string, string> map { get; set; }
        public string attributeCpr { get; set; }
        public string attributeEmployeeId { get; set; }
        public string userOU { get; set; }
        public bool allowEnablingWithoutEmployeeIdMatch { get; set; }
        public List<string> ActiveDirectoryWritebackExcludeOUs { get; set; }
        public string initialPassword { get; set; }
        public string uPNChoice { get; set; }
        public string defaultUPNDomain { get; set; }
        public string alternativeUPNDomains { get; set; }
        public string ignoredDcPrefix { get; set; }
        public string activeDirectoryUserIdGroupings { get; set; }
        public bool failReactivateOnMultipleDisabled { get; set; }


        public List<string> existingAccountExcludeOUs;
    }
}
