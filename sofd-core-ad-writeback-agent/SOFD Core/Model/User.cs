using SOFD_Core.Model.Enums;
using System;

namespace SOFD_Core.Model
{
    public class User
    {
        public string uuid { get; set; }
        public string master { get; set; }
        public string masterId { get; set; }
        public string userId { get; set; }
        public string userType { get; set; }
        public string employeeId { get; set; }
        public bool prime { get; set; }
        public string kombitUuid { get; set; }

        // internal fields only
        public string managerADAccountName { get; set; }
        public bool managerUpdateExcluded { get; set; } = false;

    }
}
