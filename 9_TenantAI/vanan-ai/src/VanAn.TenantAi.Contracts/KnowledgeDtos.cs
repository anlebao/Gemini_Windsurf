namespace VanAn.TenantAi.Contracts;

/// <summary>SRS v1.1 §6.2 — wire DTOs for knowledge ingest + admin approval gate.</summary>
public sealed record ProductKnowledgeItem(
    Guid ProductId,
    string Name,
    string? Description,
    decimal Price,
    int? Calories,
    IReadOnlyList<string>? Ingredients,
    IReadOnlyList<string>? Allergens,
    bool Available,
    string? CategoryName);

/// <summary>fullSync=true → rows absent from Items are removed (replace-by-source [G9]).</summary>
public sealed record ProductsBatchRequest(
    bool FullSync,
    string SourceVersion,
    IReadOnlyList<ProductKnowledgeItem> Items);

public sealed record ProductsBatchResponse(
    int Upserted,
    int Removed,
    string IdempotencyKey,
    bool Replayed);

public sealed record DocumentIngestRequest(
    string Type,
    string Title,
    string Content,
    string Classification,
    /// <summary>AI-P11 — provenance/dedupe key (vd "chat:{conversationId}:{messageId}"). Optional.</summary>
    string? SourceRef = null);

public sealed record DocumentSummaryDto(
    Guid Id,
    string Type,
    string Title,
    string Classification,
    string ApprovalStatus,
    string? ApprovedBy,
    DateTime? ApprovedAtUtc,
    string? RejectionReason,
    DateTime CreatedAtUtc);

public sealed record DocumentIngestResponse(Guid Id, string ApprovalStatus, bool Sanitized);

/// <summary>task_ai_p10 — admin docs list item: summary + content (approval review needs the text).</summary>
public sealed record DocumentListItemDto(
    Guid Id,
    string Type,
    string Title,
    string Classification,
    string ApprovalStatus,
    string? ApprovedBy,
    DateTime? ApprovedAtUtc,
    string? RejectionReason,
    DateTime CreatedAtUtc,
    bool Sanitized,
    string Content);

public sealed record DocumentListResponse(
    int Page,
    int Size,
    int Total,
    IReadOnlyList<DocumentListItemDto> Items);

public sealed record RejectDocumentRequest(string Reason);

/// <summary>AI-P11 — approve body: owner chọn classification lúc duyệt. Null/absent = giữ nguyên.</summary>
public sealed record ApproveDocumentRequest(string? Classification);

/// <summary>W3a — owner-triggered extraction: raw text (menu/bảng giá/FAQ) →
/// structured candidates for review. NEVER auto-ingested; ShopERP shows preview,
/// owner confirms, then pushes via /v1/knowledge/documents (approval gate intact).</summary>
public sealed record KnowledgeExtractRequest(string Text, string? Hint = null);

public sealed record KnowledgeExtractItem(string Kind, string Title, string Content);

public sealed record KnowledgeExtractResponse(
    string Summary,
    IReadOnlyList<KnowledgeExtractItem> Items);

public sealed record KnowledgeStatusResponse(
    int PendingDocs,
    int ApprovedDocs,
    int RejectedDocs,
    int Chunks,
    int ChunksEmbedded,
    int ChunksPendingEmbedding,
    int Products,
    string? LastProductSourceVersion,
    DateTime? LastProductSyncedAtUtc,
    bool ProductsStale,
    int StaleHours);
