using Microsoft.AspNetCore.Mvc.ApplicationParts;
using System.Collections.Generic;

namespace sofd_core_ad_replicator.Services.Sofd.Model
{
    public class OrgUnit
    {
        public string MasterId { get; set; }
        public string Uuid { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string ParentUuid { get; set; }
        public long Ean { get; set; }
        public bool Deleted { get; set; }
        public List<OrgUnitPost> PostAddresses { get; set; }
        public List<OrgUnitTag> Tags { get; set; }
        public Manager Manager { get; set; }

        // locally generated data
        public bool ShouldBeExcluded { get; set; }
        public List<OrgUnit> Children { get; set; } = new();
        public OrgUnit Parent { get; set; }
        public List<string> Users { get; set; } = new List<string> ();
        public HashSet<string> UsersExtended { get; set; } = new HashSet<string>();
    }
}