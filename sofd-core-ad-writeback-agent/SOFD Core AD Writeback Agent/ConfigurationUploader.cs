using Ionic.Zip;
using SOFD_Core;
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

            byte[] output = new byte[0];
            using (ZipFile zip = new ZipFile())
            {
                using (MemoryStream outputStream = new MemoryStream())
                {
                    zip.AddDirectory(Path.Combine(currentDir, "AttributeWriteback"), "AttributeWriteback");
                    zip.AddFile(Path.Combine(currentDir, "SOFD Core AD Writeback Agent.exe.config"), "");
                    zip.Save(outputStream);

                    output = outputStream.ToArray();
                }
            }

            var organizationService = new SOFDOrganizationService(Properties.Settings.Default.SofdUrl, Properties.Settings.Default.SofdApiKey);
            organizationService.UploadConfig(output);
        }
    }
}
