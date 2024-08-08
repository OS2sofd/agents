using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sofd_core_ad_replicator.Services.PAM
{
    public class PAMSettings
    {
        public bool Enabled { get; set; }
        public string CyberArkAppId { get; set; }
        public string CyberArkSafe { get; set; }
        public string CyberArkObject { get; set; }
        public string CyberArkAPI { get; set; }
    }
}
