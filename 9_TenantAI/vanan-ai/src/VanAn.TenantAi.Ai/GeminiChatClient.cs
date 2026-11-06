using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VanAn.TenantAi.Ai;

/// <summary>
/// task_ai_p3 / SRS [G17] — Gemini `generateContent` impl of <see cref="ILlmChatClient"/>.
/// Model from `TenantAiLlm:ChatModel` (default gemini-3.5-flash-lite, NFR-2 — đổi model
/// = đổi config). Timeout via HttpClient (30s configured in Program.cs). One retry on
/// timeout/5xx per task card. ApiKey rỗng hoặc TenantAi:Enabled=false → Enabled=false.
/// </summary>
public sealed class GeminiChatClient : ILlmChatClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<GeminiChatClient> _logger;
    private readonly string _model;
    private readonly string? _apiKey;
    private readonly bool _enabled;

    public GeminiChatClient(HttpClient http, IConfiguration config, ILogger<GeminiChatClient> logger)
    {
        _http = http;
        _logger = logger;
        _model = config["TenantAiLlm:ChatModel"] ?? "gemini-3.5-flash-lite";
        _apiKey = config["TenantAiLlm:ApiKey"];
        _enabled = config.GetValue("TenantAi:Enabled", true) && !string.IsNullOrWhiteSpace(_apiKey);
    }

    public bool Enabled => _enabled;

    public async Task<LlmChatResult?> GenerateAsync(string prompt, string? schemaHint = null, CancellationToken ct = default)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(prompt)) return null;

        // 1 retry on timeout/5xx — task_ai_p3 budget.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, $"models/{_model}:generateContent");
                req.Headers.TryAddWithoutValidation("x-goog-api-key", _apiKey);
                req.Content = JsonContent.Create(new
                {
                    contents = new[] { new { parts = new[] { new { text = prompt } } } },
                    generationConfig = new
                    {
                        temperature = 0.2,
                        responseMimeType = schemaHint != null ? "application/json" : "text/plain"
                    }
                }, options: Json);
                using var resp = await _http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    if ((int)resp.StatusCode < 500 && resp.StatusCode != System.Net.HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogWarning("Gemini chat failed non-retryable HTTP {Status}", (int)resp.StatusCode);
                        return null;
                    }
                    continue; // 5xx/429 → retry once
                }

                var payload = await resp.Content.ReadFromJsonAsync<GenerateResponse>(Json, ct);
                var text = payload?.Candidates?.FirstOrDefault()?.Content?.Parts is { } parts
                    ? string.Concat(parts.Select(p => p.Text))
                    : null;
                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogWarning("Gemini chat returned empty candidates");
                    return null;
                }
                return new LlmChatResult(text,
                    payload?.UsageMetadata?.PromptTokenCount,
                    payload?.UsageMetadata?.CandidatesTokenCount);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                if (attempt == 1)
                    _logger.LogWarning(ex, "Gemini chat failed after retry");
            }
        }
        return null;
    }

    private sealed class GenerateResponse
    {
        public Candidate[]? Candidates { get; set; }
        public UsageMetadata? UsageMetadata { get; set; }
    }

    private sealed class Candidate
    {
        public CandidateContent? Content { get; set; }
    }

    private sealed class CandidateContent
    {
        public Part[]? Parts { get; set; }
    }

    private sealed class Part
    {
        public string? Text { get; set; }
    }

    private sealed class UsageMetadata
    {
        public int? PromptTokenCount { get; set; }
        public int? CandidatesTokenCount { get; set; }
    }
}
