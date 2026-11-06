using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VanAn.TenantAi.Ai;

/// <summary>
/// task_ai_p2 / SRS [D3][G17] — Gemini `embedContent` impl of <see cref="IEmbeddingProvider"/>.
/// Config `TenantAiLlm:*` — ApiKey rỗng = pipeline off (Enabled=false → EmbedAsync null).
/// Timeout 30s, retry ×2 exponential backoff on transient failures. Production model
/// verify happens at P7 per SRS NFR-2 — here the impl is config-driven only.
/// </summary>
public sealed class GeminiEmbeddingService : IEmbeddingProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<GeminiEmbeddingService> _logger;
    private readonly string _model;
    private readonly string? _apiKey;

    public GeminiEmbeddingService(HttpClient http, IConfiguration config, ILogger<GeminiEmbeddingService> logger)
    {
        _http = http;
        _logger = logger;
        _model = config["TenantAiLlm:EmbeddingModel"] ?? "text-embedding-004";
        _apiKey = config["TenantAiLlm:ApiKey"];
        Dimensions = config.GetValue("TenantAiLlm:EmbeddingDimensions", 768);
    }

    public bool Enabled => !string.IsNullOrWhiteSpace(_apiKey);

    public int Dimensions { get; }

    public async Task<float[]?> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(text)) return null;

        // Transient retry ×2 (500ms, 2s) — SRS timeout/budget rules.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(attempt == 1 ? 500 : 2000, ct);
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, $"models/{_model}:embedContent");
                req.Headers.TryAddWithoutValidation("x-goog-api-key", _apiKey);
                req.Content = JsonContent.Create(new
                {
                    model = $"models/{_model}",
                    content = new { parts = new[] { new { text } } }
                }, options: Json);
                using var resp = await _http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    if ((int)resp.StatusCode < 500 && resp.StatusCode != System.Net.HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogWarning("Gemini embed failed non-retryable HTTP {Status}", (int)resp.StatusCode);
                        return null;
                    }
                    continue; // 5xx/429 → retry
                }

                var payload = await resp.Content.ReadFromJsonAsync<EmbedResponse>(Json, ct);
                var values = payload?.Embedding?.Values;
                if (values is { Length: > 0 }) return values;
                _logger.LogWarning("Gemini embed returned empty values");
                return null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                if (attempt == 2)
                    _logger.LogWarning(ex, "Gemini embed failed after retries");
            }
        }
        return null;
    }

    private sealed class EmbedResponse
    {
        public EmbeddingPayload? Embedding { get; set; }
    }

    private sealed class EmbeddingPayload
    {
        public float[]? Values { get; set; }
    }
}
