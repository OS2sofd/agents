using System.Text.Json.Serialization;

namespace SOFD.PAM.Model
{
    public class CyberArk
    {
        [JsonPropertyName("Content")]
        public string Password { get; set; }
    }
}
