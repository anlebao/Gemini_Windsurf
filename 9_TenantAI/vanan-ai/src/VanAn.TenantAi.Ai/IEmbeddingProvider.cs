namespace VanAn.TenantAi.Ai;

/// <summary>
/// SRS v1.1 [G17] — provider abstraction. Impl đầu tiên = Gemini; tests dùng fake
/// deterministic. Enabled=false (missing config) → pipeline degrades gracefully:
/// docs still approve, chunks get Embedding=null and stay "embedding_pending".
/// </summary>
public interface IEmbeddingProvider
{
    bool Enabled { get; }

    /// <summary>Embedding dimensions the provider emits (must match schema — 768 default).</summary>
    int Dimensions { get; }

    /// <summary>Null when provider disabled or the call fails — caller decides the fallback.</summary>
    Task<float[]?> EmbedAsync(string text, CancellationToken ct = default);
}
