namespace VanAn.Shared.Domain.Common
{
    public interface ITenantProvider
    {
        Guid TenantId { get; }
        string? CurrentUser { get; }
        bool HasTenant { get; }
        void SetTenant(Guid tenantId);
    }
}
