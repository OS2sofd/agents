using System;

namespace sofd_core_ad_replicator.Services.Sofd.Model
{
    public class Affiliation
    {
        public string Uuid { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? StopDate { get; set; }
        public String EmployeeId { get; set; }
        public String OrgUnitUuid { get; set; }
        public String AlternativeOrgunitUuid { get; set; }
        public Boolean Prime { get; set; }
        public string AffiliationType { get; set; }
    }
}
