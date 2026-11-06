namespace VanAn.TenantAi.Ai;

/// <summary>LLM generation result — text plus token usage for cost analysis [NFR-6].</summary>
public sealed record LlmChatResult(string Text, int? PromptTokens, int? CompletionTokens);

/// <summary>
/// SRS v1.1 [G17]/FR-13 — chat provider abstraction. First impl = Gemini
/// (`generateContent`); tests use deterministic fakes. Enabled=false (missing
/// ApiKey or TenantAi:Enabled=false) → pipeline must degrade, never crash.
/// </summary>
public interface ILlmChatClient
{
    bool Enabled { get; }

    /// <summary>
    /// Generate a completion. <paramref name="schemaHint"/> tells the provider to
    /// emit JSON (response_mime_type). Null when the call fails after retries —
    /// the orchestrator decides the fallback, this interface never throws on
    /// provider errors.
    /// </summary>
    Task<LlmChatResult?> GenerateAsync(string prompt, string? schemaHint = null, CancellationToken ct = default);
}
