using System;
using System.Collections.Generic;

namespace sofd_core_ad_replicator.Services.Sofd.Model
{
    public class Person
    {
        public String Uuid { get; set; }
        public String Cpr { get; set; }
        public List<User> Users { get; set; }
        public List<Affiliation> Affiliations { get; set; }
        public Boolean Deleted { get; set; }
    }
}
