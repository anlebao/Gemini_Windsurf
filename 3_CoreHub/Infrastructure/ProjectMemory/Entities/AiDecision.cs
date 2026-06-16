using System;
using System.Collections.Generic;
using System.Text.Json;

namespace VanAn.CoreHub.Infrastructure.ProjectMemory.Entities;

/// <summary>
/// Phase 6: Project Memory - ADR and decision tracking
/// Records architectural decisions and their context
/// </summary>
public class AiDecision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// ADR ID reference (e.g., "ADR-001", "ADR-003")
    /// </summary>
    public string? AdrId { get; set; }
    
    /// <summary>
    /// Context that led to the decision
    /// </summary>
    public string? Context { get; set; }
    
    /// <summary>
    /// The actual decision made
    /// </summary>
    public string? Decision { get; set; }
    
    /// <summary>
    /// JSON object containing consequences (pros/cons)
    /// </summary>
    public string? ConsequencesJson { get; set; }
    
    public DateTime MadeAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Who made the decision: 'user' or agent_name
    /// </summary>
    public string? MadeBy { get; set; }
    
    /// <summary>
    /// Get consequences as typed object
    /// </summary>
    public DecisionConsequences? GetConsequences()
    {
        if (string.IsNullOrEmpty(ConsequencesJson)) return null;
        return JsonSerializer.Deserialize<DecisionConsequences>(ConsequencesJson);
    }
    
    /// <summary>
    /// Set consequences
    /// </summary>
    public void SetConsequences(DecisionConsequences consequences)
    {
        ConsequencesJson = JsonSerializer.Serialize(consequences);
    }
}

/// <summary>
/// Consequences of an architectural decision
/// </summary>
public class DecisionConsequences
{
    public List<string> Pros { get; set; } = new();
    public List<string> Cons { get; set; } = new();
    public List<string> Risks { get; set; } = new();
    public string? Mitigation { get; set; }
}
