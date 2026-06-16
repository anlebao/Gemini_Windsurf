using System;
using System.Collections.Generic;
using System.Text.Json;

namespace VanAn.CoreHub.Infrastructure.ProjectMemory.Entities;

/// <summary>
/// Phase 6: Project Memory - AI Task tracking entity
/// Maps to ai_tasks table in SQLite/PostgreSQL
/// </summary>
public class AiTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public string Title { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    /// <summary>
    /// Agent name: 'Build Fixer', 'Feature Developer', 'Playwright Guardian', etc.
    /// </summary>
    public string? AgentName { get; set; }
    
    /// <summary>
    /// Status: pending, in_progress, completed, failed
    /// </summary>
    public AiTaskStatus Status { get; set; } = AiTaskStatus.Pending;
    
    public string? GitBranch { get; set; }
    
    public string? GitCommitHash { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// JSON metadata for extensibility
    /// </summary>
    public string? MetadataJson { get; set; }
    
    // Navigation properties
    public ICollection<AiFeatureTask> FeatureTasks { get; set; } = new List<AiFeatureTask>();
    public ICollection<AiAgentHistory> AgentHistories { get; set; } = new List<AiAgentHistory>();
    
    /// <summary>
    /// Get typed metadata from JSON string
    /// </summary>
    public T? GetMetadata<T>() where T : class
    {
        if (string.IsNullOrEmpty(MetadataJson)) return null;
        return JsonSerializer.Deserialize<T>(MetadataJson);
    }
    
    /// <summary>
    /// Set metadata as JSON string
    /// </summary>
    public void SetMetadata<T>(T metadata) where T : class
    {
        MetadataJson = JsonSerializer.Serialize(metadata);
    }
}

public enum AiTaskStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}
