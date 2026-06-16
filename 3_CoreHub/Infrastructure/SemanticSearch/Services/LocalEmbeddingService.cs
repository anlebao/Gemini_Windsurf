using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure.SemanticSearch.Models;

namespace VanAn.CoreHub.Infrastructure.SemanticSearch.Services;

/// <summary>
/// Phase 7: Local embedding service using hash-based embeddings
/// Fallback when OpenAI API is not available
/// NOTE: This is a simplified implementation for development/testing
/// Production should use OpenAI text-embedding-3-small
/// </summary>
public class LocalEmbeddingService : IEmbeddingService
{
    private readonly ILogger<LocalEmbeddingService> _logger;
    private readonly int _vectorSize;
    private readonly Random _random;

    public int VectorSize => _vectorSize;

    public LocalEmbeddingService(ILogger<LocalEmbeddingService> logger, int vectorSize = 1536)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _vectorSize = vectorSize;
        _random = new Random(42); // Seeded for reproducibility
    }

    public Task<float[]> GenerateEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(new float[_vectorSize]);
        }

        // Generate deterministic embedding from text hash
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
        
        var embedding = new float[_vectorSize];
        for (int i = 0; i < _vectorSize; i++)
        {
            // Use hash bytes + pseudo-random for remaining dimensions
            if (i < hash.Length)
            {
                embedding[i] = (hash[i] / 128.0f) - 1.0f; // Normalize to [-1, 1]
            }
            else
            {
                // Generate additional dimensions from seeded random
                embedding[i] = (float)(_random.NextDouble() * 2 - 1);
            }
        }

        // Normalize to unit vector
        var magnitude = MathF.Sqrt(embedding.Sum(x => x * x));
        if (magnitude > 0)
        {
            for (int i = 0; i < _vectorSize; i++)
            {
                embedding[i] /= magnitude;
            }
        }

        _logger.LogDebug("Generated {Size} dim embedding for text: {Preview}...", 
            _vectorSize, text.Length > 50 ? text[..50] : text);

        return Task.FromResult(embedding);
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts)
    {
        var results = new List<float[]>();
        foreach (var text in texts)
        {
            results.Add(await GenerateEmbeddingAsync(text));
        }
        return results;
    }

    public async Task<IReadOnlyList<DocumentChunk>> EmbedDocumentsAsync(IEnumerable<DocumentChunk> chunks)
    {
        var chunkList = chunks.ToList();
        var embeddings = await GenerateEmbeddingsAsync(chunkList.Select(c => c.Content));
        
        for (int i = 0; i < chunkList.Count; i++)
        {
            chunkList[i].Embedding = embeddings[i];
        }
        
        return chunkList;
    }
}
