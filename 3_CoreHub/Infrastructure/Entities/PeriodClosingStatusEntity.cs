using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Infrastructure.Entities;

/// <summary>
/// W5: EF persistence entity for the PeriodClosingStatuses table.
/// Replaces the previous in-memory <c>static Dictionary</c> in <see cref="Services.PeriodClosingService"/>
/// so that period close/reopen state survives application restarts.
///
/// Inherits <see cref="BaseEntity"/> → gets <c>Id</c>, <c>TenantId</c>, audit fields, and
/// <c>IMustHaveTenant</c> (multi-tenancy query filter applies automatically in VanAnDbContext).
///
/// Business invariants enforced via factory + state-transition methods:
/// <list type="bullet">
/// <item>Default status on creation = <see cref="PeriodClosingStatus.Open"/>.</item>
/// <item><see cref="MarkClosed"/> requires current status = Open (or Reopening completed → Open).</item>
/// <item><see cref="MarkReopening"/> requires current status = Closed.</item>
/// <item><see cref="MarkReopened"/> requires current status = Reopening; clears ReopenReason and returns to Open.</item>
/// </list>
/// State machine: Open → Closed → Reopening → Open (cycle allowed for audit-trail reopen).
/// </summary>
public class PeriodClosingStatusEntity : BaseEntity
{
    /// <summary>Year of the accounting period (e.g. 2026).</summary>
    public int PeriodYear { get; private set; }

    /// <summary>Month of the accounting period (1-12).</summary>
    public int PeriodMonth { get; private set; }

    /// <summary>Current workflow status. Stored as int (enum conversion in EF configuration).</summary>
    public PeriodClosingStatus Status { get; private set; } = PeriodClosingStatus.Open;

    /// <summary>UTC timestamp when the period was last closed. Null if never closed.</summary>
    public DateTime? ClosedAt { get; private set; }

    /// <summary>Identifier (user Id or email) of the user who closed the period. Null if never closed.</summary>
    public string? ClosedBy { get; private set; }

    /// <summary>Reason supplied when the period was last reopened. Null if never reopened.</summary>
    public string? ReopenReason { get; private set; }

    private PeriodClosingStatusEntity() { } // EF Core materialization

    /// <summary>Factory: create a new period status record in <see cref="PeriodClosingStatus.Open"/> state.</summary>
    public PeriodClosingStatusEntity(TenantId tenantId, int periodYear, int periodMonth)
        : base(tenantId)
    {
        if (periodYear < 2000 || periodYear > 2100)
            throw new ArgumentOutOfRangeException(nameof(periodYear), "PeriodYear must be between 2000 and 2100.");
        if (periodMonth < 1 || periodMonth > 12)
            throw new ArgumentOutOfRangeException(nameof(periodMonth), "PeriodMonth must be between 1 and 12.");

        PeriodYear = periodYear;
        PeriodMonth = periodMonth;
        Status = PeriodClosingStatus.Open;
        ClosedAt = null;
        ClosedBy = null;
        ReopenReason = null;
    }

    /// <summary>Transition Open → Closed. Throws if current status is not Open.</summary>
    public void MarkClosed(string closedBy)
    {
        if (Status != PeriodClosingStatus.Open)
            throw new InvalidOperationException(
                $"Cannot close period {PeriodYear}-{PeriodMonth:D2}: current status is {Status}, expected Open.");

        Status = PeriodClosingStatus.Closed;
        ClosedAt = DateTime.UtcNow;
        ClosedBy = closedBy ?? throw new ArgumentNullException(nameof(closedBy));
        ReopenReason = null; // clear any prior reopen reason on fresh close
        UpdateAudit(closedBy);
    }

    /// <summary>Transition Closed → Reopening. Throws if current status is not Closed.</summary>
    public void MarkReopening(string reason)
    {
        if (Status != PeriodClosingStatus.Closed)
            throw new InvalidOperationException(
                $"Cannot reopen period {PeriodYear}-{PeriodMonth:D2}: current status is {Status}, expected Closed.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reopen reason is required.", nameof(reason));

        Status = PeriodClosingStatus.Reopening;
        ReopenReason = reason;
        UpdateAudit();
    }

    /// <summary>Transition Reopening → Open. Throws if current status is not Reopening.</summary>
    public void MarkReopened()
    {
        if (Status != PeriodClosingStatus.Reopening)
            throw new InvalidOperationException(
                $"Cannot complete reopen of period {PeriodYear}-{PeriodMonth:D2}: current status is {Status}, expected Reopening.");

        Status = PeriodClosingStatus.Open;
        // Keep ReopenReason + ClosedAt for audit trail (last reopen context). They will be cleared on next MarkClosed.
        UpdateAudit();
    }
}
