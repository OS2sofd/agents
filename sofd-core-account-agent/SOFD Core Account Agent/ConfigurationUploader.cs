using Ionic.Zip;
using SOFD.PAM;
using SOFD_Core;
using System;
using System.IO;

namespace SOFD
{
    class ConfigurationUploader
    {
        public static void UploadConfiguration()
        {
            if (!Properties.Settings.Default.UploadConfiguration)
            {
                return;
            }

            string currentDir = Directory.GetCurrentDirectory();
            Console.WriteLine(currentDir);

            byte[] output = new byte[0];
            using (ZipFile zip = new ZipFile())
            {
                using (MemoryStream outputStream = new MemoryStream())
                {
                    zip.AddDirectory(Path.Combine(currentDir, "ActiveDirectory"), "ActiveDirectory");
                    zip.AddDirectory(Path.Combine(currentDir, "Exchange"), "Exchange");
                    zip.AddFile(Path.Combine(currentDir, "SOFD Core User Agent.exe.config"), "");
                    zip.Save(outputStream);

                    output = outputStream.ToArray();
                }
            }

            var organizationService = new SOFDOrganizationService(Properties.Settings.Default.SofdUrl, PAMService.GetApiKey());
            organizationService.UploadConfig(output);
        }
    }
}
