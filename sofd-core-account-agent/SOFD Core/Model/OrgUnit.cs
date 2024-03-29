﻿using System;
using System.Collections.Generic;

namespace SOFD_Core.Model
{
    public class OrgUnit
    {
        public Guid uuid { get; set; }
        public string master { get; set; }
        public string masterId { get; set; }
        public bool deleted { get; set; }
        public DateTime created { get; set; }
        public DateTime lastChanged { get; set; }
        public string shortname { get; set; }
        public string name { get; set; }
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
        public List<Email> emails { get; set; }
        public string parentUuid { get; set; }
        public OrgUnit parent { get; set; }
        public List<OrgUnitTag> tags { get; set; }

    }
}
