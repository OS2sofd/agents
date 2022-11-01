using SOFD_Core.Model.Enums;
using System;

namespace SOFD_Core.Model
{
    public class User
    {
        public Guid uuid { get; set; }
        public string master { get; set; }
        public string masterId { get; set; }
        public string userId { get; set; }
        public string userType { get; set; }
        public bool prime { get; set; }
    }
}
