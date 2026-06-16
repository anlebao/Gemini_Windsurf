using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure.SemanticSearch.Models;
using VanAn.CoreHub.Infrastructure.SemanticSearch.Services;

namespace VanAn.CoreHub.Infrastructure.SemanticSearch;

/// <summary>
/// Phase 7: Semantic Search - Implementation using embedding + vector store
/// </summary>
public class SemanticSearchService : ISemanticSearchService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<SemanticSearchService> _logger;

    public SemanticSearchService(
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        ILogger<SemanticSearchService> logger)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IndexResult> IndexDocumentAsync(DocumentChunk document, string collection)
    {
        try
        {
            // Ensure collection exists
            if (!await _vectorStore.CollectionExistsAsync(collection))
            {
                await _vectorStore.CreateCollectionAsync(collection, _embeddingService.VectorSize);
            }

            // Generate embedding if not already present
            if (document.Embedding == null || document.Embedding.Length == 0)
            {
                document.Embedding = await _embeddingService.GenerateEmbeddingAsync(document.Content);
            }

            // Index to vector store
            var success = await _vectorStore.UpsertAsync(collection, document);

            _logger.LogInformation("Indexed document: {Id} to collection: {Collection} (success: {Success})", 
                document.Id, collection, success);

            return new IndexResult
            {
                DocumentId = document.Id,
                Success = success,
                Error = success ? null : "Failed to upsert to vector store"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index document to collection: {Collection}", collection);
            return new IndexResult
            {
                DocumentId = document.Id,
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<IndexResult> IndexBatchAsync(IEnumerable<DocumentChunk> documents, string collection)
    {
        try
        {
            // Ensure collection exists
            if (!await _vectorStore.CollectionExistsAsync(collection))
            {
                await _vectorStore.CreateCollectionAsync(collection, _embeddingService.VectorSize);
            }

            var documentList = documents.ToList();
            
            // Generate embeddings
            await _embeddingService.EmbedDocumentsAsync(documentList);

            // Batch upsert
            var success = await _vectorStore.UpsertBatchAsync(collection, documentList);

            _logger.LogInformation("Indexed {Count} documents to collection: {Collection} (success: {Success})", 
                documentList.Count, collection, success);

            return new IndexResult
            {
                DocumentId = $"batch_{Guid.NewGuid():N}",
                Success = success,
                Error = success ? null : "Failed to upsert batch to vector store"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index batch to collection: {Collection}", collection);
            return new IndexResult
            {
                DocumentId = $"batch_{Guid.NewGuid():N}",
                Success = false,
                Error = ex.Message
            };
        }
    }

    public Task<bool> DeleteDocumentAsync(string documentId, string collection)
    {
        return _vectorStore.DeleteAsync(collection, documentId);
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, string collection, int topK = 5)
    {
        try
        {
            if (!await _vectorStore.CollectionExistsAsync(collection))
            {
                _logger.LogWarning("Collection not found: {Collection}", collection);
                return new List<SearchResult>();
            }

            // Generate query embedding
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

            // Search
            var results = await _vectorStore.SearchAsync(collection, queryEmbedding, topK);

            _logger.LogDebug("Search in {Collection} for '{Query}' returned {Count} results", 
                collection, query, results.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed in collection: {Collection}", collection);
            return new List<SearchResult>();
        }
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAcrossCollectionsAsync(string query, string[] collections, int topK = 5)
    {
        var allResults = new List<SearchResult>();
        var perCollectionTopK = Math.Max(1, topK / collections.Length + 1);

        foreach (var collection in collections)
        {
            var results = await SearchAsync(query, collection, perCollectionTopK);
            allResults.AddRange(results);
        }

        // Merge and re-rank by score
        return allResults
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
    }

    public Task<bool> CreateCollectionAsync(string name, int vectorSize = 1536)
    {
        return _vectorStore.CreateCollectionAsync(name, vectorSize);
    }

    public Task<bool> DeleteCollectionAsync(string name)
    {
        return _vectorStore.DeleteCollectionAsync(name);
    }

    public Task<IReadOnlyList<string>> ListCollectionsAsync()
    {
        return _vectorStore.ListCollectionsAsync();
    }

    // Convenience methods for agents
    public Task<IReadOnlyList<SearchResult>> FindAdrsAsync(string topic, int topK = 3)
    {
        return SearchAsync(topic, Collections.Adrs, topK);
    }

    public Task<IReadOnlyList<SearchResult>> FindDomainDocsAsync(string entity, int topK = 3)
    {
        return SearchAsync(entity, Collections.Domains, topK);
    }

    public Task<IReadOnlyList<SearchResult>> FindWorkflowsAsync(string taskType, int topK = 3)
    {
        return SearchAsync(taskType, Collections.Workflows, topK);
    }

    public Task<IReadOnlyList<SearchResult>> FindSkillsAsync(string problemPattern, int topK = 3)
    {
        return SearchAsync(problemPattern, Collections.Skills, topK);
    }

    public Task<IReadOnlyList<SearchResult>> FindCodeSnippetsAsync(string concept, int topK = 3)
    {
        return SearchAsync(concept, Collections.Codebase, topK);
    }
}
