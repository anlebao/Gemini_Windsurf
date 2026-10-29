using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace VanAn.ShopERP.Services;

/// <summary>
/// P6 (task_p6_admin_ui): SystemAdmin CRUD client cho vanan-metering /v1/rate-cards.
/// Typed HttpClient riêng với ADMIN caller credentials (Metering:AdminCallerId/AdminKey —
/// caller "sysadmin" có CallerKey.IsAdmin). Khác MeteringApiClient (caller "shoperp-a",
/// fail-safe trả null): client này THROW lỗi thật (status + body) để trang admin hiển thị
/// đúng message 400/404/409 — admin cần biết tại sao thao tác thất bại.
/// Actor người dùng: server ghi ChangedBy = callerId ("sysadmin"); email user truyền qua
/// AuditNote prefix "actor:{email} | ..." (task card D2 — không cần sửa API).
/// </summary>
public sealed class MeteringRateCardApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<MeteringRateCardApiClient> _logger;
    private readonly bool _enabled;

    public bool IsConfigured => _enabled;

    public MeteringRateCardApiClient(HttpClient http, IConfiguration config, ILogger<MeteringRateCardApiClient> logger)
    {
        _http = http;
        // BaseAddress must end with "/" or relative "v1/..." paths drop the last segment.
        if (http.BaseAddress is { } b && !b.OriginalString.EndsWith('/'))
            http.BaseAddress = new Uri(b.OriginalString + "/");
        _logger = logger;
        _enabled = http.BaseAddress is not null
            && !string.IsNullOrWhiteSpace(config["Metering:AdminCallerId"])
            && !string.IsNullOrWhiteSpace(config["Metering:AdminKey"]);
        if (!_enabled)
            logger.LogWarning("Metering rate-card admin client disabled — Metering:BaseUrl/AdminCallerId/AdminKey not configured.");
    }

    /// <summary>GET /v1/rate-cards?includeInactive= — heads only khi false, full version chain khi true.</summary>
    public async Task<List<MeteringRateCardDto>> GetRateCardsAsync(bool includeInactive, CancellationToken ct = default)
    {
        var resp = await SendAsync(HttpMethod.Get, $"v1/rate-cards?includeInactive={includeInactive}", ct);
        return await ReadAsync<List<MeteringRateCardDto>>(resp, ct) ?? new List<MeteringRateCardDto>();
    }

    /// <summary>POST /v1/rate-cards — tạo chain mới cho serviceCode+scope (409 nếu đã active).</summary>
    public async Task<MeteringRateCardDto> CreateRateCardAsync(MeteringRateCardUpsertRequest req, string actorEmail, CancellationToken ct = default)
    {
        var resp = await SendAsync(HttpMethod.Post, "v1/rate-cards", ct, JsonContent.Create(new
        {
            serviceCode = req.ServiceCode,
            meterUnit = req.MeterUnit,
            creditRate = req.CreditRate,
            enforcement = req.Enforcement,
            effectiveFrom = req.EffectiveFrom,
            label = req.Label,
            auditNote = AuditWithActor(actorEmail, req.AuditNote),
            scope = req.Scope,
            scopeValue = req.ScopeValue
        }, options: Json));
        return await ReadAsync<MeteringRateCardDto>(resp, ct)
            ?? throw new InvalidOperationException("Metering API returned empty response for create rate-card");
    }

    /// <summary>PATCH /v1/rate-cards/{id} — tạo version mới (append-only), chỉ patch head hiện tại.</summary>
    public async Task<MeteringRateCardDto> PatchRateCardAsync(Guid versionId, MeteringRateCardUpsertRequest req, string actorEmail, CancellationToken ct = default)
    {
        var resp = await SendAsync(HttpMethod.Patch, $"v1/rate-cards/{versionId}", ct, JsonContent.Create(new
        {
            creditRate = req.CreditRate,
            meterUnit = req.MeterUnit,
            enforcement = req.Enforcement,
            effectiveFrom = req.EffectiveFrom,
            label = req.Label,
            auditNote = AuditWithActor(actorEmail, req.AuditNote)
        }, options: Json));
        return await ReadAsync<MeteringRateCardDto>(resp, ct)
            ?? throw new InvalidOperationException("Metering API returned empty response for patch rate-card");
    }

    /// <summary>DELETE /v1/rate-cards/{id} — deactivate chain (204; idempotent 404→success).</summary>
    public async Task DeactivateRateCardAsync(Guid versionId, CancellationToken ct = default)
    {
        var resp = await SendAsync(HttpMethod.Delete, $"v1/rate-cards/{versionId}", ct);
        if (resp.StatusCode == HttpStatusCode.NoContent || resp.StatusCode == HttpStatusCode.NotFound) return;
        throw await ApiErrorAsync(resp, ct);
    }

    // ---------- P7: admin top-up ops (/v1/topups*, /v1/topup-config) ----------

    /// <summary>GET /v1/topups?status=&tenantId= — admin list (lazy-expired server-side).</summary>
    public async Task<List<MeteringTopupDto>> GetTopupsAsync(string? status = null, Guid? tenantId = null, CancellationToken ct = default)
    {
        var query = "v1/topups?";
        if (!string.IsNullOrWhiteSpace(status)) query += $"status={Uri.EscapeDataString(status)}&";
        if (tenantId.HasValue) query += $"tenantId={tenantId.Value}&";
        var resp = await SendAsync(HttpMethod.Get, query.TrimEnd('&', '?'), ct);
        var list = await ReadAsync<MeteringTopupList>(resp, ct);
        return list?.Items ?? new List<MeteringTopupDto>();
    }

    /// <summary>POST /v1/topups/{id}/confirm — credits do admin quyết; idempotent replay.</summary>
    public async Task ConfirmTopupAsync(Guid id, decimal credits, string? note, CancellationToken ct = default)
    {
        var resp = await SendAsync(HttpMethod.Post, $"v1/topups/{id}/confirm", ct,
            JsonContent.Create(new { credits, note }, options: Json));
        if (!resp.IsSuccessStatusCode) throw await ApiErrorAsync(resp, ct);
    }

    /// <summary>POST /v1/topups/{id}/reject — reason required by policy.</summary>
    public async Task RejectTopupAsync(Guid id, string? reason, CancellationToken ct = default)
    {
        var resp = await SendAsync(HttpMethod.Post, $"v1/topups/{id}/reject", ct,
            JsonContent.Create(new { reason }, options: Json));
        if (!resp.IsSuccessStatusCode) throw await ApiErrorAsync(resp, ct);
    }

    /// <summary>GET /v1/topup-config — null khi chưa cấu hình (404).</summary>
    public async Task<MeteringTopupConfigDto?> GetTopupConfigAsync(CancellationToken ct = default)
    {
        var resp = await SendAsync(HttpMethod.Get, "v1/topup-config", ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        return await ReadAsync<MeteringTopupConfigDto>(resp, ct);
    }

    /// <summary>PUT /v1/topup-config — singleton upsert (audited server-side).</summary>
    public async Task<MeteringTopupConfigDto> PutTopupConfigAsync(
        string bankCode, string bankName, string accountNumber, string accountName, CancellationToken ct = default)
    {
        var resp = await SendAsync(HttpMethod.Put, "v1/topup-config", ct, JsonContent.Create(
            new { bankCode, bankName, accountNumber, accountName }, options: Json));
        return await ReadAsync<MeteringTopupConfigDto>(resp, ct)
            ?? throw new InvalidOperationException("Metering API returned empty response for topup-config");
    }

    // ---------- Issue #197: master switch toàn hệ thống ----------

    /// <summary>GET /v1/config/enforcement — default enabled=true khi chưa từng đổi.</summary>
    public async Task<MeteringEnforcementConfigDto> GetEnforcementAsync(CancellationToken ct = default)
    {
        var resp = await SendAsync(HttpMethod.Get, "v1/config/enforcement", ct);
        return await ReadAsync<MeteringEnforcementConfigDto>(resp, ct)
            ?? throw new InvalidOperationException("Metering API returned empty response for enforcement config");
    }

    /// <summary>PUT /v1/config/enforcement — bật/tắt metering toàn hệ thống (audited).</summary>
    public async Task<MeteringEnforcementConfigDto> PutEnforcementAsync(bool enabled, CancellationToken ct = default)
    {
        var resp = await SendAsync(HttpMethod.Put, "v1/config/enforcement", ct,
            JsonContent.Create(new { enabled }, options: Json));
        return await ReadAsync<MeteringEnforcementConfigDto>(resp, ct)
            ?? throw new InvalidOperationException("Metering API returned empty response for enforcement config");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, CancellationToken ct, HttpContent? content = null)
    {
        if (!_enabled)
            throw new InvalidOperationException("Metering admin chưa được cấu hình (Metering:AdminCallerId/AdminKey).");
        using var req = new HttpRequestMessage(method, uri);
        if (content != null) req.Content = content;
        try
        {
            return await _http.SendAsync(req, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Metering rate-card admin call failed: {Method} {Uri}", method, uri);
            throw new InvalidOperationException($"Không kết nối được metering service: {ex.Message}");
        }
    }

    private async Task<T?> ReadAsync<T>(HttpResponseMessage resp, CancellationToken ct)
    {
        if (!resp.IsSuccessStatusCode) throw await ApiErrorAsync(resp, ct);
        return await resp.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    private static async Task<InvalidOperationException> ApiErrorAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        var body = await resp.Content.ReadAsStringAsync(ct);
        string? code = null, message = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                code = err.TryGetProperty("code", out var c) ? c.GetString() : null;
                message = err.TryGetProperty("message", out var m) ? m.GetString() : null;
            }
        }
        catch (JsonException) { /* body không phải error JSON — giữ raw */ }
        return new InvalidOperationException(
            $"Metering API {(int)resp.StatusCode}{(code != null ? $" [{code}]" : "")}: {message ?? body}");
    }

    private static string AuditWithActor(string actorEmail, string? note)
        => $"actor:{actorEmail}{(string.IsNullOrWhiteSpace(note) ? "" : $" | {note}")}";
}

/// <summary>Response DTO — camelCase fields của metering RateCardDto.</summary>
public sealed class MeteringRateCardDto
{
    public Guid Id { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public string MeterUnit { get; set; } = string.Empty;
    public decimal CreditRate { get; set; }
    public string Enforcement { get; set; } = "MeterOnly";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
    public string? Label { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
    public string? AuditNote { get; set; }
    /// <summary>Issue #197: "Global" | "TenantType" | "TenantStatus" | "Tenant".</summary>
    public string Scope { get; set; } = "Global";
    public string? ScopeValue { get; set; }
}

/// <summary>Form input — cùng shape cho create (POST) và patch (PATCH creates new version).</summary>
public sealed class MeteringRateCardUpsertRequest
{
    public string ServiceCode { get; set; } = string.Empty;
    public string MeterUnit { get; set; } = string.Empty;
    public decimal CreditRate { get; set; }
    public string Enforcement { get; set; } = "MeterOnly";
    public DateTime? EffectiveFrom { get; set; }
    public string? Label { get; set; }
    public string? AuditNote { get; set; }
    /// <summary>Issue #197: "Global" | "TenantType" | "TenantStatus" | "Tenant" (default Global).</summary>
    public string Scope { get; set; } = "Global";
    public string? ScopeValue { get; set; }
}

/// <summary>Issue #197 — master switch DTO (GET/PUT /v1/config/enforcement).</summary>
public sealed class MeteringEnforcementConfigDto
{
    public bool Enabled { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}
