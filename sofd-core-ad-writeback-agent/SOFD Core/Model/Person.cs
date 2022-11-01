using System;
using System.Collections.Generic;

namespace SOFD_Core.Model
{
    public class Person
    {
        public string cpr { get; set; }
        public string master { get; set; }
        public string firstname { get; set; }
        public string surname { get; set; }
        public string chosenName { get; set; }
        public string name { get { return firstname + " " + surname; } set { } }
        public DateTime? firstEmploymentDate { get; set; }
        public DateTime? anniversaryDate { get; set; }
        public Post registeredPostAddress { get; set; }
        public Post residencePostAddress { get; set; }
        public List<Phone> phones { get; set; }
        public List<User> users { get; set; }
        public List<Affiliation> affiliations { get; set; }
        public Guid uuid { get; set; }
        public DateTime created { get; set; }
        public DateTime lastChanged { get; set; }
        public bool deleted { get; set; }
    }
}
