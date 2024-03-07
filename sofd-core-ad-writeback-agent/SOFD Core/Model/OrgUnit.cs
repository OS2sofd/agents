using System;
using System.Collections.Generic;

namespace SOFD_Core.Model
{
    public class OrgUnit
    {
        public string uuid { get; set; }
        public string master { get; set; }
        public string masterId { get; set; }
        public bool deleted { get; set; }
        public DateTime created { get; set; }
        public DateTime lastChanged { get; set; }
        public string shortname { get; set; }
        public string name { get; set; }
        public string displayName { get; set; }
        public string cvrName { get; set; }
        public long? cvr { get; set; }
        public long? ean { get; set; }
        public long? senr { get; set; }
        public long? pnr { get; set; }
        public Manager manager { get; set; }
        public string costBearer { get; set; }
        public string orgType { get; set; }
        public long? orgTypeId { get; set; }
        public List<Post> postAddresses { get; set; }
        public List<Phone> phones { get; set; }
        public string email { get; set; }
        public string parentUuid { get; set; }
        public OrgUnit parent { get; set; }
        public List<OrgUnitTag> tags { get; set; }       
        public string urlAddress { get; set; }
        public String GetDisplayName()
        {
            return !String.IsNullOrEmpty(displayName) ? displayName : name;
        }
    }
}
