using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SOFD_Core.Model
{
    public class AccountOrderResponse
    {
        public bool singleAccount { get; set; }
        public List<AccountOrder> pendingOrders { get; set; }
    }
}
