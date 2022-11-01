using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using Serilog;
using SOFDCoreSignaturcentral.Service.Signaturcentral;

namespace SOFDCoreSignaturcentral.Service.Backend
{
    public class BackendService
    {
        private readonly string url = "https://sc-integration.sofd.io/api/sync";

        public ILogger Logger { get; set; }

        public void FullSync(List<MOCES> moces)
        {
            var json = JsonConvert.SerializeObject(moces);
            Logger.Verbose("Invoking backend full sync: {0}", json);

            using (WebClient webClient = new WebClient())
            {
                webClient.Headers["Content-Type"] = "application/json";
                webClient.Headers["ApiKey"] = Settings.GetStringValue("Backend.Password");
                webClient.Encoding = System.Text.Encoding.UTF8;

                webClient.UploadString(url, json);
            }
        }
    }
}