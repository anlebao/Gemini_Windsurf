using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VanAn.CoreHub.Infrastructure.ProjectMemory.Entities;

namespace VanAn.CoreHub.Infrastructure.ProjectMemory;

/// <summary>
/// Phase 6: Project Memory - Service interface for AI work tracking
/// </summary>
public interface IProjectMemoryService
{
    // Task operations
    Task<AiTask> CreateTaskAsync(string title, string? description = null, string? agentName = null);
    Task<AiTask?> GetTaskAsync(Guid id);
    Task<IReadOnlyList<AiTask>> GetTasksByAgentAsync(string agentName, int limit = 50);
    Task<IReadOnlyList<AiTask>> GetTasksByStatusAsync(AiTaskStatus status, int limit = 50);
    Task<AiTask> UpdateTaskStatusAsync(Guid taskId, AiTaskStatus status);
    Task CompleteTaskAsync(Guid taskId, string? summary = null);
    
    // Feature operations
    Task<AiFeature> CreateFeatureAsync(string name, string? description = null, string[]? relatedAdrIds = null);
    Task<AiFeature?> GetFeatureAsync(Guid id);
    Task<AiFeature?> GetFeatureByNameAsync(string name);
    Task LinkTaskToFeatureAsync(Guid featureId, Guid taskId);
    Task<IReadOnlyList<AiFeature>> GetFeaturesByStatusAsync(AiFeatureStatus status);
    
    // Decision operations
    Task<AiDecision> RecordDecisionAsync(
        string? adrId, 
        string context, 
        string decision, 
        DecisionConsequences consequences,
        string madeBy = "user");
    Task<IReadOnlyList<AiDecision>> GetDecisionsByAdrAsync(string adrId);
    Task<IReadOnlyList<AiDecision>> GetRecentDecisionsAsync(int limit = 20);
    
    // Agent History operations
    Task<AiAgentHistory> LogAgentActionAsync(
        string agentName,
        string action,
        string inputSummary,
        string outputSummary,
        bool success,
        Guid? taskId = null,
        string[]? filesModified = null);
    Task<IReadOnlyList<AiAgentHistory>> GetAgentHistoryAsync(string agentName, int limit = 50);
    Task<IReadOnlyList<AiAgentHistory>> GetRecentActionsAsync(int limit = 50);
    
    // Session operations (Vạn An specific)
    Task<AiSession> StartSessionAsync(string sessionCode, string featureName, int testsTotal);
    Task<AiSession> CompleteSessionAsync(string sessionCode, int testsPassed, string? summary = null);
    Task<AiSession?> GetSessionAsync(string sessionCode);
    Task<IReadOnlyList<AiSession>> GetSessionsByFeatureAsync(string featureName);
    
    // Query patterns
    Task<IReadOnlyList<AiTask>> GetTasksLastMonthAsync();
    Task<IReadOnlyList<AiAgentHistory>> GetWhatWeDidLastMonthAsync();
    Task<string> GenerateSprintRetrospectiveAsync(string featureName);
    Task<IReadOnlyList<string>> FindSimilarPatternsAsync(string pattern);
    
    // Cleanup operations
    Task<CleanupResult> CleanupOldDataAsync(TimeSpan retentionPeriod);
}
