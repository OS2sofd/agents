using Ionic.Zip;
using RestSharp;
using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace SOFDCoreAD.Service
{
    class ConfigurationUploader
    {
        private static bool enabled = false;
        private static string url = "";
        private static string apiKey = "";

        static ConfigurationUploader()
        {
            ServicePointManager.ServerCertificateValidationCallback = delegate (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
            {
                return true;
            };

            enabled = Settings.GetBooleanValue("UploadConfig.Enabled");
            url = Settings.GetStringValue("UploadConfig.SofdCoreUrl");
            apiKey = Settings.GetStringValue("UploadConfig.SofdCoreApiKey");
        }

        public static void UploadConfiguration()
        {
            if (!enabled)
            {
                return;
            }

            string currentDir = Directory.GetCurrentDirectory();

            byte[] output = new byte[0];
            using (ZipFile zip = new ZipFile())
            {
                using (MemoryStream outputStream = new MemoryStream())
                {
                    zip.AddFile(Path.Combine(currentDir, "SOFDCoreAD.Service.exe.config"), "");
                    zip.Save(outputStream);

                    output = outputStream.ToArray();
                }
            }

            Upload(output);
        }

        private static void Upload(byte[] content)
        {
            RestClient client = new RestClient(url);
            var request = new RestRequest("/api/config/upload", Method.POST);
            request.AddHeader("ApiKey", apiKey);
            request.AddFileBytes("file", content, "config.zip");
            request.AlwaysMultipartFormData = true;

            IRestResponse response = client.Execute(request);
            if (!response.StatusCode.Equals(HttpStatusCode.OK))
            {
                throw new Exception("Failed to upload configuration: " + response.StatusCode.ToString());
            }
        }
    }
}
