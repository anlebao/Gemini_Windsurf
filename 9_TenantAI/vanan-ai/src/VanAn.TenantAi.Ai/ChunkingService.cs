namespace VanAn.TenantAi.Ai;

/// <summary>
/// task_ai_p2 — word-window chunker for vi-VN MVP. ~400 words ≈ ≤500 tokens heuristic,
/// 40-word overlap stride so facts near a boundary survive in a neighbor chunk.
/// </summary>
public sealed class ChunkingService
{
    public const int MaxWordsPerChunk = 400;
    public const int OverlapWords = 40;

    public IReadOnlyList<string> Chunk(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= MaxWordsPerChunk)
            return [string.Join(' ', words)];

        var chunks = new List<string>();
        var stride = MaxWordsPerChunk - OverlapWords;
        for (var start = 0; start < words.Length; start += stride)
        {
            var take = Math.Min(MaxWordsPerChunk, words.Length - start);
            chunks.Add(string.Join(' ', words, start, take));
            if (start + take >= words.Length) break;
        }
        return chunks;
    }

    /// <summary>Rough token estimate for token_count (1 word ≈ 1.3 tokens vi-VN heuristic).</summary>
    public static int EstimateTokens(string chunk)
        => (int)Math.Ceiling(chunk.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length * 1.3);
}
