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
        private int pageSize = 500;
        private static int personOffset = 0;
        public static bool fullSync = true;
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

        public void MoveToHead()
        {
            RestClient client = new RestClient(url);
            var request = new RestRequest("/api/sync/head", Method.GET);
            request.AddHeader("ApiKey", apiKey);
            request.AddHeader("ClientVersion", clientVersion);

            IRestResponse<int> response = client.Execute<int>(request);
            if (!response.StatusCode.Equals(HttpStatusCode.OK))
            {
                throw new Exception("Failed to get head! " + response.StatusCode + " / " + response.Content);
            }

            personOffset = response.Data;
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

        public DeltaSync GetDeltaSyncPersons()
        {
            RestClient client = new RestClient(url);
            var request = new RestRequest("/api/sync/persons?offset=" + personOffset, Method.GET);
            request.RequestFormat = DataFormat.Json;
            request.AddHeader("ApiKey", apiKey);
            request.AddHeader("ClientVersion", clientVersion);

            IRestResponse<DeltaSync> response = client.Execute<DeltaSync>(request);
            if (!response.StatusCode.Equals(HttpStatusCode.OK))
            {
                throw new Exception("Failed to get deltasync!");
            }

            return response.Data;
        }

        public void setPersonOffset(int offset)
        {
            personOffset = offset;
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

        public OrgUnit GetOrgUnit(string uuid)
        {
            RestClient client = new RestClient(url);
            var request = new RestRequest("/api/v2/orgUnits/" + uuid, Method.GET);
            request.RequestFormat = DataFormat.Json;
            request.AddHeader("ApiKey", apiKey);
            request.AddHeader("ClientVersion", clientVersion);

            IRestResponse<OrgUnit> response = client.Execute<OrgUnit>(request);
            if (!response.StatusCode.Equals(HttpStatusCode.OK))
            {
                throw new Exception("Failed to get orgUnit: " + uuid);
            }

            return response.Data;
        }

        public List<OrgUnit> GetOrgUnits()
        {
            List<OrgUnit> orgUnits = new List<OrgUnit>();
            long page = 0;
            bool done = false;

            RestClient client = new RestClient(url);

            do
            {
                var request = new RestRequest("/api/v2/orgUnits?size=" + pageSize + "&page=" + page, Method.GET);
                request.RequestFormat = DataFormat.Json;
                request.AddHeader("ApiKey", apiKey);
                request.AddHeader("ClientVersion", clientVersion);

                IRestResponse<OrgUnitsEmbedded> response = client.Execute<OrgUnitsEmbedded>(request);
                if (!response.StatusCode.Equals(HttpStatusCode.OK))
                {
                    throw new Exception("Failed to get orgUnits: " + response.StatusCode);
                }

                var result = response.Data.orgUnits;
                if (result.Count > 0)
                {
                    // copy all
                    orgUnits.AddRange(result);

                    // skip to next page
                    page = page + 1;
                }
                else
                {
                    done = true;
                }

            } while (!done);

            return orgUnits;
        }

        public List<Person> GetPersons()
        {
            List<Person> persons = new List<Person>();
            long page = 0;
            bool done = false;

            RestClient client = new RestClient(url);

            do
            {
                var request = new RestRequest("/api/v2/persons?size=" + pageSize + "&page=" + page, Method.GET);
                request.RequestFormat = DataFormat.Json;
                request.AddHeader("ApiKey", apiKey);
                request.AddHeader("ClientVersion", clientVersion);

                IRestResponse<PersonsEmbedded> response = client.Execute<PersonsEmbedded>(request);
                if (!response.StatusCode.Equals(HttpStatusCode.OK))
                {
                    throw new Exception("Failed to get persons: " + response.StatusCode);
                }

                var result = response.Data.persons;
                if (result.Count > 0)
                {
                    // copy all
                    persons.AddRange(result);

                    // skip to next page
                    page = page + 1;
                }
                else
                {
                    done = true;
                }

            } while (!done);

            return persons;
        }
    }
}