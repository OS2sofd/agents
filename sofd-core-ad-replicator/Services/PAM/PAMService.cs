using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using sofd_core_ad_replicator.Services.PAM.Model;

namespace sofd_core_ad_replicator.Services.PAM
{
    public class PAMService : ServiceBase<PAMService>
    {
        private readonly string cyberArkAppId;
        private readonly string cyberArkSafe;
        private readonly string cyberArkObject;
        private readonly string cyberArkAPI;
        public PAMService(IServiceProvider sp) : base(sp)
        {
            cyberArkAppId = settings.PAMSettings == null ? null : settings.PAMSettings.CyberArkAppId;
            cyberArkSafe = settings.PAMSettings == null ? null : settings.PAMSettings.CyberArkSafe;
            cyberArkObject = settings.PAMSettings == null ? null : settings.PAMSettings.CyberArkObject;
            cyberArkAPI = settings.PAMSettings == null ? null : settings.PAMSettings.CyberArkAPI;
        }

        public string GetApiKey()
        {
            string apiKey = null;
            HttpClient httpClient = GetHttpClient();
            var response = httpClient.GetAsync($"/AIMWebService/api/Accounts?AppID={cyberArkAppId}&Safe={cyberArkSafe}&Object={cyberArkObject}");
            response.Wait();
            response.Result.EnsureSuccessStatusCode();
            var responseString = response.Result.Content.ReadAsStringAsync();
            responseString.Wait();
            CyberArk cyberArk = JsonSerializer.Deserialize<CyberArk>(responseString.Result);

            if (cyberArk != null && cyberArk.Password != null)
            {
                apiKey = cyberArk.Password;
            }

            return apiKey;
        }

        private HttpClient GetHttpClient()
        {
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(cyberArkAPI);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return httpClient;
        }
    }
}
