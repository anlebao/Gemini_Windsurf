using VanAn.TenantAi.Domain;

namespace VanAn.TenantAi.Ai;

public enum ContextItemKind
{
    Product,
    Chunk
}

/// <summary>
/// One retrieval result flowing through the pipeline: structured product row or
/// knowledge chunk. SourceId is the grounding handle the LLM must cite back
/// (`product:{productId}` / `chunk:{chunkId}`) — the validator strips any ref
/// outside this set [FR-4].
/// </summary>
public sealed record ContextItem(
    ContextItemKind Kind,
    string SourceId,
    string Text,
    DocumentClassification Classification,
    ProductKnowledge? Product,
    KnowledgeChunk? Chunk);
