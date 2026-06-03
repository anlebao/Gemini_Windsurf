namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Customer Information for Order CRM Integration
    /// Phase 2.5: Backend Consolidation
    /// </summary>
    public class CustomerInfo
    {
        public string FullName { get; init; } = string.Empty;
        public string PhoneNumber { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string? Address { get; init; }
        public string? Notes { get; init; }

        public CustomerInfo() { }

        public CustomerInfo(string fullName, string phoneNumber, string email, string? address = null, string? notes = null)
        {
            FullName = fullName;
            PhoneNumber = phoneNumber;
            Email = email;
            Address = address;
            Notes = notes;
        }
    }
}
