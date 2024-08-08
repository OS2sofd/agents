using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace SOFDCoreAD.Service.Service.PAM.Model
{
    public class CyberArk
    {
        [JsonPropertyName("Content")]
        public string Password { get; set; }
    }
}
