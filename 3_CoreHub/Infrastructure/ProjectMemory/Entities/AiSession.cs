using System;

namespace VanAn.CoreHub.Infrastructure.ProjectMemory.Entities;

/// <summary>
/// Phase 6: Project Memory - Vạn An specific session tracking
/// Maps to AI sessions like S1, S2, F0, F1, etc.
/// </summary>
public class AiSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Session code: e.g., "S1", "S2", "S3", "S4", "F0", "F1"
    /// </summary>
    public string? SessionCode { get; set; }
    
    /// <summary>
    /// Feature name: e.g., "UC1 QR Checkout", "Sprint 3 E-Invoice"
    /// </summary>
    public string? FeatureName { get; set; }
    
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    
    public DateTime? EndTime { get; set; }
    
    public int TestsTotal { get; set; }
    
    public int TestsPassed { get; set; }
    
    public AiSessionStatus Status { get; set; } = AiSessionStatus.InProgress;
    
    /// <summary>
    /// Session summary/notes
    /// </summary>
    public string? Summary { get; set; }
    
    /// <summary>
    /// Calculate pass rate
    /// </summary>
    public double GetPassRate()
    {
        if (TestsTotal == 0) return 0;
        return (double)TestsPassed / TestsTotal * 100;
    }
    
    /// <summary>
    /// Get session duration
    /// </summary>
    public TimeSpan? GetDuration()
    {
        if (!EndTime.HasValue) return null;
        return EndTime.Value - StartTime;
    }
    
    /// <summary>
    /// Format for Rx report: R1 ✅ | coverage X/20 | next: Session N
    /// </summary>
    public string FormatRxReport()
    {
        var symbol = Status == AiSessionStatus.Completed && TestsPassed == TestsTotal ? "✅" : "⚠️";
        return $"{SessionCode} {symbol} | coverage {TestsPassed}/{TestsTotal} | next: ?";
    }
}

public enum AiSessionStatus
{
    InProgress,
    Completed,
    Failed,
    Cancelled
}
