using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SOFD.PAM.Model
{
    public class CyberArk
    {
        [JsonPropertyName("Content")]
        public string Password { get; set; }
    }
}
