namespace VanAn.Shared.Domain.Aggregates.TenantAggregate
{
    /// <summary>
    /// Value object for tenant configuration settings — Wave 5
    /// Owned entity — stored in Tenants table (no separate table needed)
    /// </summary>
    public class TenantSettings
    {
        public string? ContactEmail { get; private set; }
        public string? ContactPhone { get; private set; }
        public string? Address { get; private set; }
        public string? LogoUrl { get; private set; }
        public string? TaxCode { get; private set; }

        // Store Finder — geographic coordinates (migrated from Shop entity, 2026-07-21)
        public double? Latitude { get; private set; }
        public double? Longitude { get; private set; }

        // EF Core requires parameterless constructor
        private TenantSettings() { }

        public TenantSettings(
            string? contactEmail,
            string? contactPhone,
            string? address,
            string? logoUrl = null,
            string? taxCode = null,
            double? latitude = null,
            double? longitude = null)
        {
            ContactEmail = contactEmail;
            ContactPhone = contactPhone;
            Address = address;
            LogoUrl = logoUrl;
            TaxCode = taxCode;
            Latitude = latitude;
            Longitude = longitude;
        }

        public TenantSettings WithContactEmail(string email)
            => new(email, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude);

        public TenantSettings WithContactPhone(string phone)
            => new(ContactEmail, phone, Address, LogoUrl, TaxCode, Latitude, Longitude);

        public TenantSettings WithAddress(string address)
            => new(ContactEmail, ContactPhone, address, LogoUrl, TaxCode, Latitude, Longitude);

        public TenantSettings WithTaxCode(string taxCode)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, taxCode, Latitude, Longitude);

        public TenantSettings WithCoordinates(double latitude, double longitude)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, latitude, longitude);

        public static TenantSettings Empty() => new(null, null, null);
    }
}
