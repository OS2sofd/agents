using RestSharp;
using SOFD_Core.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace SOFD_Core
{
    public class SOFDOrganizationService
    {
        private static string url = "";
        private static string apiKey = "";
        private static string clientVersion;

        static SOFDOrganizationService()
        {
            clientVersion = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location).ProductVersion;
            ServicePointManager.ServerCertificateValidationCallback = delegate (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
            {
                return true;
            };
        }

        public SOFDOrganizationService(string url, string apiKey)
        {
            SOFDOrganizationService.url = url;
            SOFDOrganizationService.apiKey = apiKey;
        }

        public void UploadConfig(byte[] content)
        {
            RestClient client = new RestClient(url);
            var request = new RestRequest("/api/config/upload", Method.POST);
            request.AddHeader("ApiKey", apiKey);
            request.AddHeader("ClientVersion", clientVersion);
            request.AddFileBytes("file", content, "config.zip");
            request.AlwaysMultipartFormData = true;

            IRestResponse response = client.Execute(request);
            if (!response.StatusCode.Equals(HttpStatusCode.OK))
            {
                throw new Exception("Failed to upload configuration: " + response.StatusCode.ToString());
            }
        }

        public Person GetPerson(string uuid)
        {
            RestClient client = new RestClient(url);
            var request = new RestRequest("/api/v2/persons/" + uuid, Method.GET);
            request.RequestFormat = DataFormat.Json;
            request.AddHeader("ApiKey", apiKey);
            request.AddHeader("ClientVersion", clientVersion);

            IRestResponse<Person> response = client.Execute<Person>(request);
            if (!response.StatusCode.Equals(HttpStatusCode.OK))
            {
                throw new Exception("Failed to get person: " + uuid);
            }

            return response.Data;
        }

        public AccountOrderResponse GetPendingOrders(string userType, string orderType)
        {
            return GetPendingOrders(userType, new List<string>() { orderType });
        }

        public AccountOrderResponse GetPendingOrders(string userType, List<string> orderTypes)
        {
            RestClient client = new RestClient(url);
            AccountOrderResponse result = null;

            foreach (var orderType in orderTypes)
            {
                var request = new RestRequest("/api/account/" + userType + "/pending?type=" + orderType, Method.GET);
                request.RequestFormat = DataFormat.Json;
                request.AddHeader("ApiKey", apiKey);
                request.AddHeader("ClientVersion", clientVersion);

                IRestResponse<AccountOrderResponse> response = client.Execute<AccountOrderResponse>(request);
                if (!response.StatusCode.Equals(HttpStatusCode.OK))
                {
                    throw new Exception("Failed to get pending orders!  status: " + response.StatusCode + "     \n" + response.ErrorMessage);
                }

                if (result == null)
                {
                    result = response.Data;
                }
                else
                {
                    result.pendingOrders.AddRange(response.Data.pendingOrders);
                }
            }

            return result;
        }

        public void SetOrderStatus(string userType, List<AccountOrderStatus> result)
        {
            RestClient client = new RestClient(url);
            var request = new RestRequest("/api/account/" + userType + "/setStatus", Method.POST);
            request.RequestFormat = DataFormat.Json;
            request.AddHeader("ApiKey", apiKey);
            request.AddHeader("ClientVersion", clientVersion);
            request.AddJsonBody(result);

            IRestResponse response = client.Execute(request);
            if (!response.StatusCode.Equals(HttpStatusCode.OK))
            {
                throw new Exception("Failed to set order status!");
            }
        }
    }
}
