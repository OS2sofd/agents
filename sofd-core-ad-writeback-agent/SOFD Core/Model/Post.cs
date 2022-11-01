namespace SOFD_Core.Model
{
    public class Post
    {
        public long id { get; set; }
        public string master { get; set; }
        public string masterId { get; set; }
        public string street { get; set; }
        public string localname { get; set; }
        public string postalCode { get; set; }
        public string city { get; set; }
        public string country { get; set; }
        public bool addressProtected { get; set; }
        public bool prime { get; set; }
    }
}