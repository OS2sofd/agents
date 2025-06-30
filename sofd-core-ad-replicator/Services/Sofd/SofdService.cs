using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using sofd_core_ad_replicator.Services.PAM;
using sofd_core_ad_replicator.Services.Sofd.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using RestSharp;
using System.Net;

namespace sofd_core_ad_replicator.Services.Sofd
{
    internal class SofdService : ServiceBase<SofdService>
    {
        private readonly Uri baseUri;
        private readonly string apiKey;
        private readonly long orgUnitPageSize;
        private readonly long personsPageSize;
        private readonly JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore };

        public SofdService(IServiceProvider sp) : base(sp)
        {
            baseUri = new Uri(settings.SofdSettings.BaseUrl);
            orgUnitPageSize = settings.SofdSettings.OrgUnitPageSize;
            personsPageSize = settings.SofdSettings.PersonsPageSize;

            var pamEnabled = settings.PAMSettings == null ? false : settings.PAMSettings.Enabled;
            if (pamEnabled)
            {
                PAMService pamService = sp.GetService<PAMService>();
                apiKey = pamService.GetApiKey();
            }
            else
            {
                apiKey = settings.SofdSettings.ApiKey;
            }
        }

        public List<OrgUnit> GetActiveOrgUnits()
        {
            logger.LogInformation($"Fetching orgunits from SOFD");
            using var httpClient = GetHttpClient();
            var response = httpClient.GetAsync(new Uri(baseUri, $"api/v2/orgUnits?size={orgUnitPageSize}"));
            response.Wait();
            response.Result.EnsureSuccessStatusCode();
            var responseString = response.Result.Content.ReadAsStringAsync();
            responseString.Wait();

            var getOrgUnitsDto = JsonConvert.DeserializeObject<GetOrgUnitsDto>(responseString.Result, jsonSerializerSettings);
            List<OrgUnit> orgUnits = getOrgUnitsDto.OrgUnits;

            // if we reached max entries on the last page there might be more data in SOFD!
            if (orgUnits.Count == orgUnitPageSize)
            {
                throw new Exception("Not all OrgUnits was fetched from SOFD. Increase PageSize");
            }

            logger.LogInformation($"Finished fetching orgunits from SOFD");
            return orgUnits.Where(o => !o.Deleted).ToList();
        }

        public List<Person> GetPersons()
        {
            logger.LogInformation($"Fetching persons from SOFD");

            List<Person> persons = new List<Person>();
            long page = 0;
            bool done = false;

            var options = new RestClientOptions(baseUri)
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            };
            var client = new RestClient(options);

            do
            {
                var request = new RestRequest("/api/v2/persons?size=" + personsPageSize + "&page=" + page, Method.Get);
                request.RequestFormat = DataFormat.Json;
                request.AddHeader("ApiKey", apiKey);

                RestResponse<GetPersonDto> response = client.Execute<GetPersonDto>(request);
                if (!response.StatusCode.Equals(HttpStatusCode.OK))
                {
                    throw new Exception("Failed to get persons: " + response.StatusCode);
                }

                var result = response.Data.Persons;
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

            logger.LogInformation($"Finished fetching persons from SOFD");

            return persons;
        }

        private HttpClient GetHttpClient()
        {
            var handler = new HttpClientHandler();
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.ServerCertificateCustomValidationCallback =
                (httpRequestMessage, cert, cetChain, policyErrors) =>
                {
                    return true;
                };

            var httpClient = new HttpClient(handler);
            httpClient.DefaultRequestHeaders.Add("ApiKey", apiKey);
            return httpClient;
        }

        private JsonSerializerSettings getSerializerSettings()
        {
            // api requires camel case
            return new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            };
        }
    }
}
