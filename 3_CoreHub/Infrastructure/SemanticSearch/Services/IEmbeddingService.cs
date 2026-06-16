using System.Collections.Generic;
using System.Threading.Tasks;
using VanAn.CoreHub.Infrastructure.SemanticSearch.Models;

namespace VanAn.CoreHub.Infrastructure.SemanticSearch.Services;

/// <summary>
/// Phase 7: Embedding service for generating vector representations
/// Uses OpenAI text-embedding-3-small (1536 dimensions)
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generate embedding for a single text
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(string text);
    
    /// <summary>
    /// Generate embeddings for multiple texts (batch)
    /// </summary>
    Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts);
    
    /// <summary>
    /// Generate and attach embeddings to document chunks
    /// </summary>
    Task<IReadOnlyList<DocumentChunk>> EmbedDocumentsAsync(IEnumerable<DocumentChunk> chunks);
    
    /// <summary>
    /// Vector size (1536 for text-embedding-3-small)
    /// </summary>
    int VectorSize { get; }
}
