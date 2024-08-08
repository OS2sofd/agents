using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using SOFDCoreAD.Service.Service.PAM.Model;

namespace SOFDCoreAD.Service.Service.PAM
{
    internal class PAMService
    {
        private static readonly string cyberArkAppId;
        private static readonly string cyberArkSafe;
        private static readonly string cyberArkAPI;
        private static readonly bool cyberArkEnabled;

        static PAMService()
        {
            cyberArkAppId = Settings.GetStringValue("CyberArk.CyberArkAppId");
            cyberArkSafe = Settings.GetStringValue("CyberArk.CyberArkSafe");
            cyberArkAPI = Settings.GetStringValue("CyberArk.CyberArkAPI");
            cyberArkEnabled = Settings.GetBooleanValue("CyberArk.Enabled");
        }

        public static string GetSofdApiKey()
        {
            var cyberArkEnabled = Settings.GetBooleanValue("CyberArk.Enabled");
            var cyberArkObject = Settings.GetStringValues("CyberArk.CyberArkObject.")["SOFD"];
            if (cyberArkEnabled)
            {
                return GetApiKey(cyberArkObject);
            }
            else
            {
                return Settings.GetStringValue("UploadConfig.SofdCoreApiKey");
            }
        }

        public static string GetBackendApiKey()
        {
            var cyberArkEnabled = Settings.GetBooleanValue("CyberArk.Enabled");
            var cyberArkObject = Settings.GetStringValues("CyberArk.CyberArkObject.")["Backend"];
            if (cyberArkEnabled)
            {
                return GetApiKey(cyberArkObject);
            }
            else
            {
                return Settings.GetStringValue("Backend.Password");
            }
        }

        private static string GetApiKey(string cyberArkObject)
        {
            if (cyberArkEnabled)
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

            return null;
        }
        private static HttpClient GetHttpClient()
        {
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(cyberArkAPI);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return httpClient;
        }
    }
}
