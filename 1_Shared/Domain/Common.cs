namespace VanAn.Shared.Domain.Common;

/// <summary>
/// Contract cho Multi-tenancy - Bắt buộc mọi Entity phải có TenantId
/// </summary>
public interface IMustHaveTenant
{
    /// <summary>
    /// Tenant ID để cách ly dữ liệu giữa các quán
    /// </summary>
    TenantId TenantId { get; set; }
}

/// <summary>
/// Base Entity với TenantId tích hợp sẵn
/// </summary>
public abstract class BaseEntity : IMustHaveTenant
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Multi-tenancy field
    public TenantId TenantId { get; set; } = new TenantId(Guid.Empty);
    
    // Audit Trail Compliance (Rule 48)
    public bool IsDeleted { get; set; } = false;
    
    // Sync Infrastructure Properties
    public long RowVersion { get; set; } // For Concurrency & Sync
    public DateTime LastSyncedAt { get; set; }
}
