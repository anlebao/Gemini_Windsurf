using System;
using System.Collections.Generic;

namespace VanAn.CoreHub.Infrastructure.SemanticSearch.Models;

/// <summary>
/// Phase 7: A chunk of document ready for vector embedding and indexing
/// </summary>
public class DocumentChunk
{
    /// <summary>
    /// Unique identifier for this chunk
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Original document source (file path, URL, etc.)
    /// </summary>
    public string Source { get; set; } = string.Empty;
    
    /// <summary>
    /// The text content to be embedded
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Document type: adr, domain, workflow, skill, code, task
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;
    
    /// <summary>
    /// Chunk position in original document (for ordering)
    /// </summary>
    public int ChunkIndex { get; set; }
    
    /// <summary>
    /// Total chunks in document
    /// </summary>
    public int TotalChunks { get; set; }
    
    /// <summary>
    /// Metadata about the document
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
    
    /// <summary>
    /// Vector embedding (populated after embedding generation)
    /// </summary>
    public float[]? Embedding { get; set; }
    
    /// <summary>
    /// When this chunk was indexed
    /// </summary>
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Collection names for semantic search
/// </summary>
public static class Collections
{
    public const string Adrs = "adrs";
    public const string Domains = "domains";
    public const string Workflows = "workflows";
    public const string Skills = "skills";
    public const string Codebase = "codebase";
    public const string Tasks = "tasks";
    
    public static readonly IReadOnlyList<string> All = new[]
    {
        Adrs, Domains, Workflows, Skills, Codebase, Tasks
    };
}

/// <summary>
/// Document types for chunking strategy
/// </summary>
public static class DocumentTypes
{
    public const string Adr = "adr";
    public const string Domain = "domain";
    public const string Workflow = "workflow";
    public const string Skill = "skill";
    public const string Code = "code";
    public const string Task = "task";
}
