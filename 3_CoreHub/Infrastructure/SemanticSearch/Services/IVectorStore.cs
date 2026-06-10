using System.Collections.Generic;
using System.Threading.Tasks;
using VanAn.CoreHub.Infrastructure.SemanticSearch.Models;

namespace VanAn.CoreHub.Infrastructure.SemanticSearch.Services;

/// <summary>
/// Phase 7: Vector store interface (Qdrant abstraction)
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Create a collection with specified vector size
    /// </summary>
    Task<bool> CreateCollectionAsync(string name, int vectorSize);
    
    /// <summary>
    /// Delete a collection
    /// </summary>
    Task<bool> DeleteCollectionAsync(string name);
    
    /// <summary>
    /// List all collections
    /// </summary>
    Task<IReadOnlyList<string>> ListCollectionsAsync();
    
    /// <summary>
    /// Check if collection exists
    /// </summary>
    Task<bool> CollectionExistsAsync(string name);
    
    /// <summary>
    /// Upsert a document with embedding
    /// </summary>
    Task<bool> UpsertAsync(string collection, DocumentChunk document);
    
    /// <summary>
    /// Upsert multiple documents
    /// </summary>
    Task<bool> UpsertBatchAsync(string collection, IEnumerable<DocumentChunk> documents);
    
    /// <summary>
    /// Delete a document by ID
    /// </summary>
    Task<bool> DeleteAsync(string collection, string documentId);
    
    /// <summary>
    /// Search for similar vectors
    /// </summary>
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        string collection, 
        float[] queryVector, 
        int topK = 5,
        Dictionary<string, string>? filters = null);
}
