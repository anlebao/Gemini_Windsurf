using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace VanAn.CoreHub.Infrastructure.ProjectMemory.Entities;

/// <summary>
/// Phase 6: Project Memory - Detailed agent action log
/// Records what each agent did, inputs, outputs, and results
/// </summary>
public class AiAgentHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Agent that performed the action
    /// </summary>
    public string? AgentName { get; set; }
    
    /// <summary>
    /// Related task (optional)
    /// </summary>
    public Guid? TaskId { get; set; }
    public AiTask? Task { get; set; }
    
    /// <summary>
    /// Action performed: e.g., 'fixed_build_error', 'implemented_feature', 'created_tests'
    /// </summary>
    public string? Action { get; set; }
    
    /// <summary>
    /// Summary of input/problem
    /// </summary>
    public string? InputSummary { get; set; }
    
    /// <summary>
    /// Summary of output/solution
    /// </summary>
    public string? OutputSummary { get; set; }
    
    /// <summary>
    /// JSON array of modified files: ["file1.cs", "file2.cs"]
    /// </summary>
    public string? FilesModifiedJson { get; set; }
    
    /// <summary>
    /// Whether the action was successful
    /// </summary>
    public bool Success { get; set; }
    
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Get list of modified files
    /// </summary>
    public string[] GetFilesModified()
    {
        if (string.IsNullOrEmpty(FilesModifiedJson)) return Array.Empty<string>();
        return JsonSerializer.Deserialize<string[]>(FilesModifiedJson) ?? Array.Empty<string>();
    }
    
    /// <summary>
    /// Set modified files
    /// </summary>
    public void SetFilesModified(string[] files)
    {
        FilesModifiedJson = JsonSerializer.Serialize(files);
    }
    
    /// <summary>
    /// Set modified files from IEnumerable
    /// </summary>
    public void SetFilesModified(IEnumerable<string> files)
    {
        FilesModifiedJson = JsonSerializer.Serialize(files.ToArray());
    }
}
