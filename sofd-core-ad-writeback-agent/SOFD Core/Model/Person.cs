using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
        public string uuid { get; set; }
        public DateTime created { get; set; }
        public DateTime lastChanged { get; set; }
        public bool deleted { get; set; }
        public string keyWords { get; set; }
        public string personCalculatedName { get { return !string.IsNullOrWhiteSpace(chosenName) ? chosenName : ((firstname??"") + " " + (surname??"")).Trim(); } }
        
        private Regex nameRegex = new Regex("^(?<firstname>.*?)\\s?(?<surname>(\\b((da)|(von)|(van)|(le)|(al)|(el)|(mc))\\s)?\\S+)$",RegexOptions.IgnoreCase);
        public string calculatedFirstname { get
            {
                if (!string.IsNullOrWhiteSpace(chosenName))
                {
                    var match = nameRegex.Match(chosenName.Trim());
                    if (match.Success)
                    {
                        return match.Groups["firstname"].Value;
                    }
                }
                return firstname ?? "";
            }
        }
        public string calculatedSurname
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(chosenName))
                {
                    var match = nameRegex.Match(chosenName.Trim());
                    if (match.Success)
                    {
                        return match.Groups["surname"].Value;
                    }
                }
                return surname ?? "";
            }
        }

        public List<AuthorizationCode> authorizationCodes { get; set; }
    }
}
