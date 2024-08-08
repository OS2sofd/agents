using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace sofd_core_ad_replicator.Services.PAM.Model
{
    public class CyberArk
    {
        [JsonPropertyName("Content")]
        public string? Password { get; init; }
    }
}
