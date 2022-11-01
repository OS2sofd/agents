using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SOFD_Core.Model
{
    public class AccountOrderStatus
    {
        public long id { get; set; }
        public string status { get; set; }
        public string affectedUserId { get; set; }
        public string message { get; set; }
    }
}
