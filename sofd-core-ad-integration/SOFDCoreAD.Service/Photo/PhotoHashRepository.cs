using SOFDCoreAD.Service.Model;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.IO;
using Newtonsoft.Json;

namespace SOFDCoreAD.Service.Photo
{

    public class PhotoHashRepository
    {
        public bool PhotosEnabled { get; private set; }
        private Dictionary<string, string> hashes;        
        private readonly MD5 md5 = new MD5CryptoServiceProvider();
        private readonly string repoFilePath;
        // flag to prevent unnecessary file reloads
        private bool shouldLoad = true;

        public PhotoHashRepository()
        {
            var appDirectory = Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Digital Identity", "SOFDCoreAD.Service"));
            repoFilePath = Path.Combine(appDirectory.FullName, "PhotoHashRepository.json");
            PhotosEnabled = !string.IsNullOrEmpty(Settings.GetStringValue("ActiveDirectory.Property.Photo"));
        }

        private string GetHash(byte[] bytes)
        {
            return bytes == null ? "" : BitConverter.ToString(md5.ComputeHash(bytes)).Replace("-", string.Empty);
        }

        public bool InsertPhoto(ADUser adUser)
        {
            var hash = GetHash(adUser.Photo);
            if (hashes.ContainsKey(adUser.UserId) && hashes[adUser.UserId] == hash)
            {
                // photo is not changed - return false
                return false;
            }
            hashes[adUser.UserId] = hash;
            shouldLoad = true;
            // return true if photo is updated
            return true;
        }

        public void Load()
        {
            if(shouldLoad)
            {
                if (System.IO.File.Exists(repoFilePath))
                {
                    var hashFile = System.IO.File.ReadAllText(repoFilePath);
                    hashes = JsonConvert.DeserializeObject<Dictionary<string, string>>(hashFile);
                }
                else
                {
                    hashes = new Dictionary<string, string>();
                }
            }
        }
        public void Save()
        {            
            System.IO.File.WriteAllText(repoFilePath, JsonConvert.SerializeObject(hashes));
            shouldLoad = false;
        }

    }
}
