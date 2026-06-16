namespace VanAn.Shared.Domain.Common
{
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
        public bool IsDeleted { get; protected set; }

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
            if (updatedBy != null)
            {
                UpdatedBy = updatedBy;
            }
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
}

namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Invoice recipient type for e-invoice generation
    /// </summary>
    public enum InvoiceRecipientType
    {
        /// <summary>
        /// B2B invoice
        /// </summary>
        B2B,

        /// <summary>
        /// Retail member invoice
        /// </summary>
        RetailMember,

        /// <summary>
        /// Retail anonymous invoice
        /// </summary>
        RetailAnonymous,

        /// <summary>
        /// HKD Retail invoice
        /// </summary>
        HKDRetail,

        /// <summary>
        /// HKD Wholesale invoice
        /// </summary>
        HKDWholesale,

        /// <summary>
        /// E-invoice (electronic invoice)
        /// </summary>
        EInvoice
    }

    /// <summary>
    /// Pending invoice status for batch processing
    /// </summary>
    public enum PendingInvoiceStatus
    {
        /// <summary>
        /// Invoice is pending batch processing
        /// </summary>
        PendingInvoice = 0,

        /// <summary>
        /// Invoice is being processed
        /// </summary>
        Processing = 1,

        /// <summary>
        /// Invoice has been issued
        /// </summary>
        Invoiced = 2,

        /// <summary>
        /// Invoice processing failed
        /// </summary>
        Failed = 3
    }
}
