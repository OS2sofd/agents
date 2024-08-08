namespace sofd_core_ad_replicator.Services.Sofd.Model
{
    public class User
    {

        public long Id { get; set; }
        public string Uuid { get; set; }
        public string UserId { get; set; }
        public string UserType { get; set; }
        public string EmployeeId { get; set; }
        public bool Disabled { get; set; }
    }
}
