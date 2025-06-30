namespace sofd_core_ad_replicator.Services.Sofd.Model
{
    public class Manager
    {
        public string Uuid { get; set; }
        public string Name { get; set; }
        public bool Inherited { get; set; }

        // locally set
        public string UserId { get; set; }
    }
}