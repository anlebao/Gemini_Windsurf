using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VanAn.CoreHub.Infrastructure.ProjectMemory.Entities;

namespace VanAn.CoreHub.Infrastructure.ProjectMemory;

/// <summary>
/// Phase 6: Project Memory - Service implementation for AI work tracking
/// </summary>
public class ProjectMemoryService : IProjectMemoryService
{
    private readonly ProjectMemoryDbContext _context;

    public ProjectMemoryService(ProjectMemoryDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    #region Task Operations

    public async Task<AiTask> CreateTaskAsync(string title, string? description = null, string? agentName = null)
    {
        var task = new AiTask
        {
            Title = title,
            Description = description,
            AgentName = agentName,
            Status = AiTaskStatus.InProgress,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }

    public async Task<AiTask?> GetTaskAsync(Guid id)
    {
        return await _context.Tasks
            .Include(t => t.AgentHistories)
            .Include(t => t.FeatureTasks)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IReadOnlyList<AiTask>> GetTasksByAgentAsync(string agentName, int limit = 50)
    {
        return await _context.Tasks
            .Where(t => t.AgentName == agentName)
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<AiTask>> GetTasksByStatusAsync(AiTaskStatus status, int limit = 50)
    {
        return await _context.Tasks
            .Where(t => t.Status == status)
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<AiTask> UpdateTaskStatusAsync(Guid taskId, AiTaskStatus status)
    {
        var task = await _context.Tasks.FindAsync(taskId)
            ?? throw new InvalidOperationException($"Task {taskId} not found");
        
        task.Status = status;
        if (status == AiTaskStatus.Completed || status == AiTaskStatus.Failed)
        {
            task.CompletedAt = DateTime.UtcNow;
        }
        
        await _context.SaveChangesAsync();
        return task;
    }

    public async Task CompleteTaskAsync(Guid taskId, string? summary = null)
    {
        var task = await _context.Tasks.FindAsync(taskId)
            ?? throw new InvalidOperationException($"Task {taskId} not found");
        
        task.Status = AiTaskStatus.Completed;
        task.CompletedAt = DateTime.UtcNow;
        
        if (!string.IsNullOrEmpty(summary))
        {
            task.Description = string.IsNullOrEmpty(task.Description) 
                ? summary 
                : $"{task.Description}\n\n[Completed]: {summary}";
        }
        
        await _context.SaveChangesAsync();
    }

    #endregion

    #region Feature Operations

    public async Task<AiFeature> CreateFeatureAsync(string name, string? description = null, string[]? relatedAdrIds = null)
    {
        var feature = new AiFeature
        {
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            Status = AiFeatureStatus.InProgress
        };

        if (relatedAdrIds != null)
        {
            feature.SetRelatedAdrIds(relatedAdrIds);
        }

        _context.Features.Add(feature);
        await _context.SaveChangesAsync();
        return feature;
    }

    public async Task<AiFeature?> GetFeatureAsync(Guid id)
    {
        return await _context.Features
            .Include(f => f.FeatureTasks)
            .ThenInclude(ft => ft.Task)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<AiFeature?> GetFeatureByNameAsync(string name)
    {
        return await _context.Features
            .Include(f => f.FeatureTasks)
            .ThenInclude(ft => ft.Task)
            .FirstOrDefaultAsync(f => f.Name == name);
    }

    public async Task LinkTaskToFeatureAsync(Guid featureId, Guid taskId)
    {
        var link = new AiFeatureTask
        {
            FeatureId = featureId,
            TaskId = taskId
        };

        _context.FeatureTasks.Add(link);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<AiFeature>> GetFeaturesByStatusAsync(AiFeatureStatus status)
    {
        return await _context.Features
            .Where(f => f.Status == status)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();
    }

    #endregion

    #region Decision Operations

    public async Task<AiDecision> RecordDecisionAsync(
        string? adrId, 
        string context, 
        string decision, 
        DecisionConsequences consequences,
        string madeBy = "user")
    {
        var dec = new AiDecision
        {
            AdrId = adrId,
            Context = context,
            Decision = decision,
            MadeAt = DateTime.UtcNow,
            MadeBy = madeBy
        };
        dec.SetConsequences(consequences);

        _context.Decisions.Add(dec);
        await _context.SaveChangesAsync();
        return dec;
    }

    public async Task<IReadOnlyList<AiDecision>> GetDecisionsByAdrAsync(string adrId)
    {
        return await _context.Decisions
            .Where(d => d.AdrId == adrId)
            .OrderByDescending(d => d.MadeAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<AiDecision>> GetRecentDecisionsAsync(int limit = 20)
    {
        return await _context.Decisions
            .OrderByDescending(d => d.MadeAt)
            .Take(limit)
            .ToListAsync();
    }

    #endregion

    #region Agent History Operations

    public async Task<AiAgentHistory> LogAgentActionAsync(
        string agentName,
        string action,
        string inputSummary,
        string outputSummary,
        bool success,
        Guid? taskId = null,
        string[]? filesModified = null)
    {
        var history = new AiAgentHistory
        {
            AgentName = agentName,
            TaskId = taskId,
            Action = action,
            InputSummary = inputSummary,
            OutputSummary = outputSummary,
            Success = success,
            ExecutedAt = DateTime.UtcNow
        };

        if (filesModified != null)
        {
            history.SetFilesModified(filesModified);
        }

        _context.AgentHistories.Add(history);
        await _context.SaveChangesAsync();
        return history;
    }

    public async Task<IReadOnlyList<AiAgentHistory>> GetAgentHistoryAsync(string agentName, int limit = 50)
    {
        return await _context.AgentHistories
            .Where(h => h.AgentName == agentName)
            .OrderByDescending(h => h.ExecutedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<AiAgentHistory>> GetRecentActionsAsync(int limit = 50)
    {
        return await _context.AgentHistories
            .OrderByDescending(h => h.ExecutedAt)
            .Take(limit)
            .ToListAsync();
    }

    #endregion

    #region Session Operations

    public async Task<AiSession> StartSessionAsync(string sessionCode, string featureName, int testsTotal)
    {
        var session = new AiSession
        {
            SessionCode = sessionCode,
            FeatureName = featureName,
            StartTime = DateTime.UtcNow,
            TestsTotal = testsTotal,
            TestsPassed = 0,
            Status = AiSessionStatus.InProgress
        };

        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task<AiSession> CompleteSessionAsync(string sessionCode, int testsPassed, string? summary = null)
    {
        var session = await _context.Sessions
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode && s.Status == AiSessionStatus.InProgress)
            ?? throw new InvalidOperationException($"Active session {sessionCode} not found");

        session.EndTime = DateTime.UtcNow;
        session.TestsPassed = testsPassed;
        session.Status = AiSessionStatus.Completed;
        session.Summary = summary;

        await _context.SaveChangesAsync();
        return session;
    }

    public async Task<AiSession?> GetSessionAsync(string sessionCode)
    {
        return await _context.Sessions
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);
    }

    public async Task<IReadOnlyList<AiSession>> GetSessionsByFeatureAsync(string featureName)
    {
        return await _context.Sessions
            .Where(s => s.FeatureName == featureName)
            .OrderBy(s => s.StartTime)
            .ToListAsync();
    }

    #endregion

    #region Query Patterns

    public async Task<IReadOnlyList<AiTask>> GetTasksLastMonthAsync()
    {
        var lastMonth = DateTime.UtcNow.AddMonths(-1);
        return await _context.Tasks
            .Where(t => t.CreatedAt >= lastMonth)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<AiAgentHistory>> GetWhatWeDidLastMonthAsync()
    {
        var lastMonth = DateTime.UtcNow.AddMonths(-1);
        return await _context.AgentHistories
            .Where(h => h.ExecutedAt >= lastMonth && h.Success)
            .OrderByDescending(h => h.ExecutedAt)
            .ToListAsync();
    }

    public async Task<string> GenerateSprintRetrospectiveAsync(string featureName)
    {
        var sessions = await GetSessionsByFeatureAsync(featureName);
        var tasks = await _context.Tasks
            .Where(t => t.CreatedAt >= sessions.Min(s => s.StartTime).AddDays(-1))
            .ToListAsync();
        
        var decisions = await _context.Decisions
            .Where(d => d.MadeAt >= sessions.Min(s => s.StartTime).AddDays(-1))
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine($"# Sprint Retrospective: {featureName}");
        sb.AppendLine();
        sb.AppendLine($"**Period:** {sessions.Min(s => s.StartTime):yyyy-MM-dd} to {sessions.Max(s => s.EndTime ?? DateTime.UtcNow):yyyy-MM-dd}");
        sb.AppendLine();
        
        sb.AppendLine("## Sessions");
        foreach (var session in sessions)
        {
            var symbol = session.Status == AiSessionStatus.Completed && session.TestsPassed == session.TestsTotal ? "✅" : "⚠️";
            sb.AppendLine($"- {session.SessionCode} {symbol} | {session.TestsPassed}/{session.TestsTotal} tests");
        }
        sb.AppendLine();
        
        sb.AppendLine("## Key Decisions");
        foreach (var dec in decisions.Take(10))
        {
            sb.AppendLine($"- {dec.Decision} (ADR: {dec.AdrId ?? "N/A"})");
        }
        sb.AppendLine();
        
        sb.AppendLine("## Summary");
        var totalTests = sessions.Sum(s => s.TestsTotal);
        var passedTests = sessions.Sum(s => s.TestsPassed);
        sb.AppendLine($"- Total sessions: {sessions.Count}");
        sb.AppendLine($"- Test pass rate: {passedTests}/{totalTests} ({(totalTests > 0 ? passedTests * 100 / totalTests : 0)}%)");
        sb.AppendLine($"- Tasks completed: {tasks.Count(t => t.Status == AiTaskStatus.Completed)}");
        
        return sb.ToString();
    }

    public async Task<IReadOnlyList<string>> FindSimilarPatternsAsync(string pattern)
    {
        // Simple pattern matching - can be enhanced with semantic search in Phase 7
        var histories = await _context.AgentHistories
            .Where(h => h.InputSummary != null && h.InputSummary.Contains(pattern) ||
                       h.OutputSummary != null && h.OutputSummary.Contains(pattern))
            .Take(10)
            .ToListAsync();

        return histories
            .Select(h => $"[{h.AgentName}] {h.Action}: {h.InputSummary} -> {h.OutputSummary}")
            .ToList();
    }

    #endregion
}
