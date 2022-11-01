using System.DirectoryServices;

namespace Active_Directory
{
    public class DirectoryEntryWrapper
    {
        public DirectoryEntry Entry { get; set; }
        public string DC { get; set; }
    }
}
