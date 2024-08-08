using System;
using System.Collections.Generic;

namespace sofd_core_ad_replicator.Services.Sofd
{
    public class SofdSettings
    {
        public string ApiKey { get; set; }
        public string BaseUrl { get; set; }
        public long OrgUnitPageSize { get; set; }
        public string ExcludeFromSyncTagName { get; set; }
        public long PersonsPageCount { get; set; }
        public long PersonsPageSize { get; set; }
        public string RootOrgUnitUuid { get; set; }
        public Dictionary<string, string> SOFDToADOrgUnitMap { get; set; }
    }
}
