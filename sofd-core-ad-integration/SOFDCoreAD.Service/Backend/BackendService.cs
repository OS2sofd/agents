using Newtonsoft.Json;
using Serilog;
using SOFDCoreAD.Service.Model;
using SOFDCoreAD.Service.Service.PAM;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;

namespace SOFDCoreAD.Service.Backend
{
    public class BackendService
    {
        private readonly string baseUrl = "https://ad-integration.sofd.io/api/";
        private readonly string clientVersion;

        public BackendService()
        {
            clientVersion = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
        }
        public ILogger Logger { get; set; }

        public void FullSync(IEnumerable<ADUser> users)
        {
            var json = JsonConvert.SerializeObject(users);
            Logger.Verbose("Invoking backend full sync: {0}", json);

            using (WebClient webClient = new WebClient())
            {
                webClient.Headers["Content-Type"] = "application/json";
                webClient.Headers["ApiKey"] = PAMService.GetBackendApiKey();
                webClient.Headers["ClientVersion"] = clientVersion;
                webClient.Encoding = System.Text.Encoding.UTF8;

                webClient.UploadString(baseUrl + "fullsync", json);
            }
        }

        public void DeltaSync(IEnumerable<ADUser> users)
        {
            if (users.Count() > 0)
            {
                var json = JsonConvert.SerializeObject(users);
                Logger.Verbose("Invoking backend delta sync: {0}", json);

                using (WebClient webClient = new WebClient())
                {
                    webClient.Headers["Content-Type"] = "application/json";
                    webClient.Headers["ApiKey"] = PAMService.GetBackendApiKey();
                    webClient.Headers["ClientVersion"] = clientVersion;
                    webClient.Encoding = System.Text.Encoding.UTF8;

                    webClient.UploadString(baseUrl + "deltasync", json);
                }
            }
        }
    }
}