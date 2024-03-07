using SOFD_Core.Model.Enums;
using System;
using System.Collections.Generic;

namespace SOFD_Core.Model
{
    public class Affiliation
    {
        public string uuid { get; set; }
        public string master { get; set; }
        public string masterId { get; set; }
        public bool prime { get; set; }
        public string startDate { get; set; }
        public string stopDate { get; set; }
        public bool deleted { get; set; }
        public string orgUnitUuid { get; set; }
        public string alternativeOrgUnitUuid { get; set; }
        public string employeeId { get; set; }
        public string opusEmployeeId { get{ return this.master == "OPUS" ? employeeId : null; } }
        public string sdEmployeeId { get { return this.master.StartsWith("SD-") ? employeeId : null; } }
        public string employmentTerms { get; set; }
        public string employmentTermsText { get; set; }
        public string payGrade { get; set; }
        public double? workingHoursDenominator { get; set; }
        public double? workingHoursNumerator { get; set; }
        public string affiliationType { get; set; }
        public string positionId { get; set; }
        public string positionName { get; set; }
        public string positionDisplayName { get; set; }
        public string positionCalculatedName { get { return !string.IsNullOrWhiteSpace(positionDisplayName) ? positionDisplayName : positionName; } }
        public string calculatedOrgUnitUuid { get { return !string.IsNullOrWhiteSpace(alternativeOrgUnitUuid) ? alternativeOrgUnitUuid : orgUnitUuid; } }
        public List<string> functions { get; set; }
    }
}