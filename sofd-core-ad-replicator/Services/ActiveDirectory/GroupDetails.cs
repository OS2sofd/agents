
using System.Collections.Generic;

namespace sofd_core_ad_replicator
{
    public class GroupDetails
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string DisplayName { get; set; }
        public string SamAccountName { get; set; }
        public string DN {  get; set; }
        public string Id { get; set; }
        public bool Found { get; set; }

        // members stuff
        public List<string> Members { get;set; } = new List<string>();
        public List<string> ToAdd { get; set; } = new List<string>();
        public List<string> ToRemove { get; set; } = new List<string>();
    }
}
