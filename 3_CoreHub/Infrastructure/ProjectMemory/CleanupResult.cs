namespace VanAn.CoreHub.Infrastructure.ProjectMemory;

/// <summary>
/// Result of a Project Memory cleanup operation.
/// Distinct from VanAn.Shared.Domain.CleanupResult (which is for VoiceCommand audio files).
/// </summary>
public class CleanupResult
{
    public int SessionsDeleted { get; set; }
    public int TasksDeleted { get; set; }
    public int HistoryEntriesDeleted { get; set; }
    public int DecisionsArchived { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public string? Error { get; set; }
}
