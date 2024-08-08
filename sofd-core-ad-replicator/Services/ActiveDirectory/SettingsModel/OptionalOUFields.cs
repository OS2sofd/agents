namespace sofd_core_ad_replicator.Services.ActiveDirectory
{
    public class OptionalOUFields
    {
        public string EanField { get; set; }
        public string StreetAddressField { get; set; }
        public string CityField { get; set; }
        public string PostalCodeField { get; set; }
        public string LosIDField { get; set; }
        public bool EanFieldInherit { get; set; } = false;
    }
}