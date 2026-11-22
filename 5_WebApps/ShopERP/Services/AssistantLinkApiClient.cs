using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services;

// ── DTOs (mirror Gateway /api/v1/assistant-link — UNIVERSAL AI ASSISTANT W1) ──

public record AssistantLinkDto(
    Guid Id, string Slug, string? Label, bool IsActive,
    DateTime CreatedAt, string PublicUrl, string EmbedUrl);

/// <summary>
/// UNIVERSAL AI ASSISTANT W1 (2026-11-22): ShopERP HTTP proxy cho Gateway
/// assistant-link management API (/api/v1/assistant-link). Architecture: ShopERP
/// KHÔNG query PG trực tiếp — mọi tenant ops qua Gateway (D1).
/// Auth: mint JWT (tenant_id claim từ user đang login) qua GatewayAdminApiClientBase.
/// QR PNG: endpoint yêu cầu Bearer → fetch bytes → base64 data URI (img src không
/// mang token được).
/// </summary>
public interface IAssistantLinkApiClient
{
    Task<ApiResponse<List<AssistantLinkDto>>> ListAsync(CancellationToken ct = default);
    Task<ApiResponse<AssistantLinkDto>> CreateAsync(string? label, CancellationToken ct = default);
    Task<ApiResponse<bool>> RevokeAsync(Guid linkId, CancellationToken ct = default);
    Task<ApiResponse<AssistantLinkDto>> RegenerateAsync(Guid linkId, CancellationToken ct = default);
    Task<ApiResponse<string>> GetQrPngBase64Async(Guid linkId, CancellationToken ct = default);
}

public sealed class AssistantLinkApiClient : GatewayAdminApiClientBase, IAssistantLinkApiClient
{
    private const string Base = "api/v1/assistant-link";
    private readonly ILogger<AssistantLinkApiClient> _logger;

    public AssistantLinkApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IJwtTokenService jwtTokenService,
        AuthenticationStateProvider authStateProvider,
        ILogger<AssistantLinkApiClient> logger)
        : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger)
    {
        _logger = logger;
    }

    public async Task<ApiResponse<List<AssistantLinkDto>>> ListAsync(CancellationToken ct = default)
    {
        try
        {
            HttpRequestMessage request = await CreateRequestAsync(HttpMethod.Get, Base, null, null);
            return await SendAsync<List<AssistantLinkDto>>(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AssistantLinkApi GET list failed");
            return ApiResponse<List<AssistantLinkDto>>.Failure("Không kết nối được máy chủ.");
        }
    }

    public async Task<ApiResponse<AssistantLinkDto>> CreateAsync(string? label, CancellationToken ct = default)
    {
        try
        {
            HttpRequestMessage request = await CreateRequestAsync(HttpMethod.Post, Base,
                new { label }, null);
            return await SendAsync<AssistantLinkDto>(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AssistantLinkApi POST create failed");
            return ApiResponse<AssistantLinkDto>.Failure("Không kết nối được máy chủ.");
        }
    }

    public async Task<ApiResponse<bool>> RevokeAsync(Guid linkId, CancellationToken ct = default)
    {
        try
        {
            HttpRequestMessage request = await CreateRequestAsync(
                HttpMethod.Post, $"{Base}/{linkId}/revoke", null, null);
            return await SendAsync<bool>(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AssistantLinkApi POST revoke {LinkId} failed", linkId);
            return ApiResponse<bool>.Failure("Không kết nối được máy chủ.");
        }
    }

    public async Task<ApiResponse<AssistantLinkDto>> RegenerateAsync(Guid linkId, CancellationToken ct = default)
    {
        try
        {
            HttpRequestMessage request = await CreateRequestAsync(
                HttpMethod.Post, $"{Base}/{linkId}/regenerate", null, null);
            return await SendAsync<AssistantLinkDto>(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AssistantLinkApi POST regenerate {LinkId} failed", linkId);
            return ApiResponse<AssistantLinkDto>.Failure("Không kết nối được máy chủ.");
        }
    }

    /// <summary>QR PNG → base64 data URI (Bearer không đi qua &lt;img src&gt; được).</summary>
    public async Task<ApiResponse<string>> GetQrPngBase64Async(Guid linkId, CancellationToken ct = default)
    {
        try
        {
            HttpRequestMessage request = await CreateRequestAsync(
                HttpMethod.Get, $"{Base}/{linkId}/qr.png", null, null);
            HttpResponseMessage response = await HttpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return ApiResponse<string>.Failure($"Không tải được QR ({(int)response.StatusCode}).");
            byte[] png = await response.Content.ReadAsByteArrayAsync(ct);
            return ApiResponse<string>.Success($"data:image/png;base64,{Convert.ToBase64String(png)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AssistantLinkApi GET qr {LinkId} failed", linkId);
            return ApiResponse<string>.Failure("Không kết nối được máy chủ.");
        }
    }

    private async Task<ApiResponse<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage response = await HttpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            if (typeof(T) == typeof(bool))
                return ApiResponse<T>.Success((T)(object)true);
            T? data = await response.Content.ReadFromJsonAsync<T>(GatewayJsonOptions, ct);
            return data is null
                ? ApiResponse<T>.Failure("Máy chủ trả về dữ liệu rỗng.")
                : ApiResponse<T>.Success(data);
        }

        string error = await ReadErrorAsync(response, ct);
        return ApiResponse<T>.Failure(error);
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = await System.Text.Json.JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == System.Text.Json.JsonValueKind.String)
                return msg.GetString()!;
            if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == System.Text.Json.JsonValueKind.String)
                return err.GetString()!;
        }
        catch { /* fallthrough */ }
        return $"Máy chủ trả về lỗi ({(int)response.StatusCode}). Vui lòng thử lại.";
    }
}
