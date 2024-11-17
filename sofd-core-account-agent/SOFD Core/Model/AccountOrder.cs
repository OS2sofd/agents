using System;

namespace SOFD_Core.Model
{
    public class AccountOrder
    {
        public long id { get; set; }
        public PersonDetails person { get; set; }
        public string userType { get; set; }
        public string orderType { get; set; }
        public string userId { get; set; }
        public string linkedUserId { get; set; }
        public DateTime? endDate { get; set; }
        public string optionalJson { get; set; }
    }
}
