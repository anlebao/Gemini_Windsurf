using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VanAn.CoreHub.Infrastructure.SemanticSearch.Models;

namespace VanAn.CoreHub.Infrastructure.SemanticSearch;

/// <summary>
/// Phase 7: Semantic Search - Interface for AI knowledge retrieval
/// Enables semantic search over ADRs, domains, workflows, skills, codebase
/// </summary>
public interface ISemanticSearchService
{
    // Indexing operations
    Task<IndexResult> IndexDocumentAsync(DocumentChunk document, string collection);
    Task<IndexResult> IndexBatchAsync(IEnumerable<DocumentChunk> documents, string collection);
    Task<bool> DeleteDocumentAsync(string documentId, string collection);
    
    // Search operations
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, string collection, int topK = 5);
    Task<IReadOnlyList<SearchResult>> SearchAcrossCollectionsAsync(string query, string[] collections, int topK = 5);
    
    // Collection management
    Task<bool> CreateCollectionAsync(string name, int vectorSize = 1536);
    Task<bool> DeleteCollectionAsync(string name);
    Task<IReadOnlyList<string>> ListCollectionsAsync();
    
    // Utility queries for agents
    Task<IReadOnlyList<SearchResult>> FindAdrsAsync(string topic, int topK = 3);
    Task<IReadOnlyList<SearchResult>> FindDomainDocsAsync(string entity, int topK = 3);
    Task<IReadOnlyList<SearchResult>> FindWorkflowsAsync(string taskType, int topK = 3);
    Task<IReadOnlyList<SearchResult>> FindSkillsAsync(string problemPattern, int topK = 3);
    Task<IReadOnlyList<SearchResult>> FindCodeSnippetsAsync(string concept, int topK = 3);
}

public record IndexResult
{
    public required string DocumentId { get; init; }
    public required bool Success { get; init; }
    public string? Error { get; init; }
}

public record SearchResult
{
    public required string DocumentId { get; init; }
    public required string Content { get; init; }
    public required float Score { get; init; }
    public required string Collection { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}
