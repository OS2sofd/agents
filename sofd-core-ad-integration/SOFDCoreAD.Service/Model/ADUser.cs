using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace SOFDCoreAD.Service.Model
{
    public class ADUser
    {
        public string Cpr { get; set; }
        public string Affiliation { get; set; }
        public string Title { get; set; }
        public string ChosenName { get; set; }
        public string Firstname { get; set; }
        public string Surname { get; set; }
        public string Email { get; set; }
        public string Mobile { get; set; }
        public string SecretMobile { get; set; }
        public string Phone { get; set; }
        public string DepartmentNumber { get; set; }
        public string FaxNumber { get; set; }
        public string UserId { get; set; }
        public string EmployeeId { get; set; }
        public string ObjectGuid { get; set; }
        public long DaysToPwdChange { get; set; }
        public string AccountExpireDate { get; set; }
        public string WhenCreated { get; set; }
        public bool Deleted { get; set; }
        public bool Disabled { get; set; }
        public bool PasswordLocked { get; set; }
        public string UPN { get; set; }
        public string MitIDUUID { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public byte[] Photo { get; set; }
        public Dictionary<string, string> LocalExtensions { get; set; } = new Dictionary<string, string>();

        private bool IsActive()
        {
            return !Deleted && !Disabled;
        }
        internal bool IsHigherPriority(ADUser anotherADUser)
        {
            // priorty is based on user active status
            if (IsActive() && !anotherADUser.IsActive())
            {
                return true;
            }
            if (!IsActive() && anotherADUser.IsActive())
            {
                return false;
            }
            // if both users have the same active status, priority is based on an arbitrary guid comparison to ensure the same user is always prioritized.
            return ObjectGuid.CompareTo(anotherADUser.ObjectGuid) > 0;
        }
    }
}
