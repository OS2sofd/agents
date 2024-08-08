using SOFD.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using SOFD.PAM.Model;
using System.Text.Json;

namespace SOFD.PAM
{
    internal class PAMService
    {
        private static readonly string cyberArkAppId;
        private static readonly string cyberArkSafe;
        private static readonly string cyberArkObject;
        private static readonly string cyberArkAPI;

        static PAMService()
        {
            cyberArkAppId = Properties.Settings.Default.CyberArkAppId;
            cyberArkSafe = Properties.Settings.Default.CyberArkSafe;
            cyberArkObject = Properties.Settings.Default.CyberArkObject;
            cyberArkAPI = Properties.Settings.Default.CyberArkAPI;
        }

        public static string GetApiKey()
        {
            if (Properties.Settings.Default.CyberArkEnabled)
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
            else
            {
                return Properties.Settings.Default.SofdApiKey;
            }
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
