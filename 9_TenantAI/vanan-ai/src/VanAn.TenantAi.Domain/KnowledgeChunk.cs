namespace VanAn.TenantAi.Domain;

/// <summary>
/// SRS v1.1 §7 — embeddable slice of a KnowledgeDocument. Embedding kept as
/// float[] so Domain stays dependency-free; Infra maps it to pgvector vector(768)
/// via a value converter. Null until the embedding pipeline fills it.
/// </summary>
public sealed class KnowledgeChunk
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid DocumentId { get; private set; }
    public int ChunkIndex { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public float[]? Embedding { get; private set; }
    public int TokenCount { get; private set; }

    private KnowledgeChunk() { } // EF Core

    public static KnowledgeChunk Create(
        Guid tenantId,
        Guid documentId,
        int chunkIndex,
        string content,
        float[]? embedding = null,
        int tokenCount = 0)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
            Content = content,
            Embedding = embedding,
            TokenCount = tokenCount
        };

    public void SetEmbedding(float[] embedding, int tokenCount)
    {
        Embedding = embedding;
        TokenCount = tokenCount;
    }
}
