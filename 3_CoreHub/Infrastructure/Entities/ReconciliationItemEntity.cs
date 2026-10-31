using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Infrastructure.Entities;

/// <summary>
/// SRS v1.2 W5/F (M10 — FR-RECNC-005, G-M3): workflow xử lý chênh lệch tra soát.
/// Emit từ ReconcileResult (Warning/Error checks) — dedup per (Tenant,CheckCode,Period).
/// Status: Open → Investigating → Resolved | Dismissed.
/// </summary>
public class ReconciliationItemEntity : BaseEntity
{
    /// <summary>Check code R1..R13 phát sinh item.</summary>
    public string CheckCode { get; private set; } = string.Empty;
    public int PeriodYear { get; private set; }
    public int PeriodMonth { get; private set; }
    /// <summary>"warning" | "error".</summary>
    public string Severity { get; private set; } = "warning";
    public string Description { get; private set; } = string.Empty;
    /// <summary>Open | Investigating | Resolved | Dismissed.</summary>
    public string Status { get; private set; } = "Open";
    public string? AssignedTo { get; private set; }
    public string? ResolutionNote { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public string? ResolvedBy { get; private set; }

    private ReconciliationItemEntity() { } // EF Core

    public ReconciliationItemEntity(TenantId tenantId, string checkCode, int periodYear, int periodMonth,
        string severity, string description) : base(tenantId)
    {
        CheckCode = checkCode;
        PeriodYear = periodYear;
        PeriodMonth = periodMonth;
        Severity = severity;
        Description = description;
    }

    public void Assign(string assignedTo)
    {
        AssignedTo = assignedTo;
        Status = "Investigating";
    }

    public void Resolve(string resolvedBy, string? note)
    {
        Status = "Resolved";
        ResolvedBy = resolvedBy;
        ResolutionNote = note;
        ResolvedAt = DateTime.UtcNow;
    }

    public void Dismiss(string resolvedBy, string? note)
    {
        Status = "Dismissed";
        ResolvedBy = resolvedBy;
        ResolutionNote = note;
        ResolvedAt = DateTime.UtcNow;
    }

    /// <summary>Check pass lần sau → auto-resolve (giữ audit: ResolvedBy="system").</summary>
    public void AutoResolve()
    {
        Status = "Resolved";
        ResolvedBy = "system";
        ResolvedAt = DateTime.UtcNow;
    }
}
