using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace VanAn.CoreHub.Infrastructure.ProjectMemory.Entities;

/// <summary>
/// Phase 6: Project Memory - Feature aggregate entity
/// Groups multiple tasks into a feature (e.g., UC1, Sprint 3)
/// </summary>
public class AiFeature
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    /// <summary>
    /// JSON array of related ADR IDs: ["ADR-001", "ADR-003"]
    /// </summary>
    public string? RelatedAdrIdsJson { get; set; }
    
    public AiFeatureStatus Status { get; set; } = AiFeatureStatus.InProgress;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? CompletedAt { get; set; }
    
    // Navigation properties
    public ICollection<AiFeatureTask> FeatureTasks { get; set; } = new List<AiFeatureTask>();
    
    /// <summary>
    /// Get related ADR IDs as string array
    /// </summary>
    public string[] GetRelatedAdrIds()
    {
        if (string.IsNullOrEmpty(RelatedAdrIdsJson)) return Array.Empty<string>();
        return JsonSerializer.Deserialize<string[]>(RelatedAdrIdsJson) ?? Array.Empty<string>();
    }
    
    /// <summary>
    /// Set related ADR IDs
    /// </summary>
    public void SetRelatedAdrIds(string[] adrIds)
    {
        RelatedAdrIdsJson = JsonSerializer.Serialize(adrIds);
    }
    
    /// <summary>
    /// Get all tasks in this feature
    /// </summary>
    public IEnumerable<AiTask> GetTasks()
    {
        return FeatureTasks?.Select(ft => ft.Task) ?? Enumerable.Empty<AiTask>();
    }
}

public enum AiFeatureStatus
{
    Planning,
    InProgress,
    Completed,
    Cancelled
}

/// <summary>
/// Junction table for Feature-Task many-to-many relationship
/// </summary>
public class AiFeatureTask
{
    public Guid FeatureId { get; set; }
    public AiFeature Feature { get; set; } = null!;
    
    public Guid TaskId { get; set; }
    public AiTask Task { get; set; } = null!;
}
