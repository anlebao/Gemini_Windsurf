namespace VanAn.Shared.Domain.Common;

/// <summary>
/// Contract cho Multi-tenancy - Bắt buộc mọi Entity phải có TenantId
/// </summary>
public interface IMustHaveTenant
{
    /// <summary>
    /// Tenant ID để cách ly dữ liệu giữa các quán
    /// </summary>
    TenantId TenantId { get; }
}


/// <summary>
/// Audit interface - Clear contract
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; }
    DateTime UpdatedAt { get; }
    string? CreatedBy { get; }
    string? UpdatedBy { get; }
}

public abstract class BaseEntity : IMustHaveTenant, IAuditableEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public TenantId TenantId { get; protected set; } = null!;

    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; protected set; } = DateTime.UtcNow;
    public string? CreatedBy { get; protected set; }
    public string? UpdatedBy { get; protected set; }
    public bool IsDeleted { get; protected set; } = false;

    protected BaseEntity() { }

    protected BaseEntity(TenantId tenantId)
    {
        TenantId = tenantId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    protected void UpdateAudit(string? updatedBy = null)
    {
        UpdatedAt = DateTime.UtcNow;
        if (updatedBy != null) UpdatedBy = updatedBy;
    }

    protected void MarkAsDeleted(string? updatedBy = null)
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    // Protected internal: Accessible from derived classes and from 3_CoreHub assembly
    protected internal void SetTenantId(TenantId tenantId)
    {
        TenantId = tenantId;
    }
}
