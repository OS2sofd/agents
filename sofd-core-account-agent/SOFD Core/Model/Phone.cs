using SOFD_Core.Model.Enums;

namespace SOFD_Core.Model
{
    public class Phone
    {
        public long id { get; set; }
        public string master { get; set; }
        public string masterId { get; set; }
        public string phoneNumber { get; set; }
        public string phoneType { get; set; }
        public bool prime { get; set; }
    }
}