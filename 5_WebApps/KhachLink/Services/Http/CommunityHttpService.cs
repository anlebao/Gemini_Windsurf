using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// CC-S1-T1/T2 (Sprint 1): HTTP client for Community Commerce endpoints.
/// KhachLink calls Gateway → CommunityController (Gateway-native, no YARP forward).
/// All methods require X-Customer-Token header (authenticated shipper).
/// </summary>
public class CommunityHttpService(IHttpClientFactory httpClientFactory, ILogger<CommunityHttpService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<CommunityHttpService> _logger = logger;

    /// <summary>
    /// GET /api/community/role — returns isShipper flag. Used by NavMenu to show/hide shipper tab.
    /// </summary>
    public async Task<bool> GetIsShipperAsync(string customerToken)
    {
        var role = await GetRoleAsync(customerToken);
        return role?.IsShipper ?? false;
    }

    /// <summary>
    /// CC-S4 (Sprint 4): GET /api/community/role — returns isShipper + isSalesman flags.
    /// </summary>
    public async Task<RoleResponse?> GetRoleAsync(string customerToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/community/role");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await _httpClient.SendAsync(request);
            if (!resp.IsSuccessStatusCode) return null;

            var body = await resp.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<RoleResponse>(body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetRoleAsync failed");
            return null;
        }
    }

    /// <summary>
    /// CC-S6 (Sprint 6): GET /api/community/my-roles — all community roles (active + inactive).
    /// Used by Profile.razor to display role badges.
    /// </summary>
    public async Task<List<CommunityRoleDto>> GetMyRolesAsync(string customerToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/community/my-roles");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await _httpClient.SendAsync(request);
            if (!resp.IsSuccessStatusCode) return new List<CommunityRoleDto>();

            var body = await resp.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<List<CommunityRoleDto>>(body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new List<CommunityRoleDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMyRolesAsync failed");
            return new List<CommunityRoleDto>();
        }
    }

    /// <summary>
    /// CC-S6 (Sprint 6 v1.2): GET /api/community/my-fraud-flags — salesman self-view fraud flags.
    /// Used by Profile.razor to display fraud flag status.
    /// </summary>
    public async Task<List<MyFraudFlagDto>> GetMyFraudFlagsAsync(string customerToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/community/my-fraud-flags");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await _httpClient.SendAsync(request);
            if (!resp.IsSuccessStatusCode) return new List<MyFraudFlagDto>();

            var body = await resp.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<List<MyFraudFlagDto>>(body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new List<MyFraudFlagDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMyFraudFlagsAsync failed");
            return new List<MyFraudFlagDto>();
        }
    }

    /// <summary>
    /// CC-S4 (Sprint 4): GET /api/community/nearby-products — nearby products with commission + bonus.
    /// </summary>
    public async Task<List<NearbyProductDto>> GetNearbyProductsAsync(string customerToken, double lat, double lng, int radiusKm)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"/api/community/nearby-products?lat={lat}&lng={lng}&radiusKm={radiusKm}");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await _httpClient.SendAsync(request);
            if (!resp.IsSuccessStatusCode) return new List<NearbyProductDto>();

            var body = await resp.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<List<NearbyProductDto>>(body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetNearbyProductsAsync failed");
            return new List<NearbyProductDto>();
        }
    }

    /// <summary>
    /// Sprint 7: GET /api/community/commerce-mode — customer-facing global commerce mode.
    /// Returns "Marketplace" or "Reseller" for UI badge + price display.
    /// </summary>
    public async Task<CommerceModeResult> GetCommerceModeAsync(string customerToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/community/commerce-mode");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await _httpClient.SendAsync(request);
            if (!resp.IsSuccessStatusCode)
                return new CommerceModeResult { Success = false };

            var body = await resp.Content.ReadAsStringAsync();
            var data = System.Text.Json.JsonSerializer.Deserialize<CommerceModeResponse>(body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return new CommerceModeResult
            {
                Success = true,
                GlobalMode = data?.GlobalMode ?? "Marketplace",
                IsReseller = data?.IsReseller ?? false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCommerceModeAsync failed");
            return new CommerceModeResult { Success = false };
        }
    }

    /// <summary>
    /// CC-S4 (Sprint 4): GET /api/community/salesman/qr?productId={id} — composite QR code.
    /// </summary>
    public async Task<CompositeSalesmanQrDto?> GetSalesmanQrAsync(string customerToken, Guid productId)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"/api/community/salesman/qr?productId={productId}");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await _httpClient.SendAsync(request);
            if (!resp.IsSuccessStatusCode) return null;

            var body = await resp.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<CompositeSalesmanQrDto>(body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSalesmanQrAsync failed for product {ProductId}", productId);
            return null;
        }
    }

    /// <summary>
    /// CC-S4 (Sprint 4): GET /api/community/salesman/commissions — commission summary.
    /// </summary>
    public async Task<CommissionSummaryDto?> GetCommissionsAsync(string customerToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/community/salesman/commissions");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await _httpClient.SendAsync(request);
            if (!resp.IsSuccessStatusCode) return null;

            var body = await resp.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<CommissionSummaryDto>(body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCommissionsAsync failed");
            return null;
        }
    }

    /// <summary>
    /// CC-S4 (Sprint 4): POST /api/community/app-install/attributed — attribute app install.
    /// </summary>
    public async Task<bool> AttributeInstallAsync(string customerToken, string referralCode)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/community/app-install/attributed");
            request.Headers.Add("X-Customer-Token", customerToken);
            request.Content = JsonContent.Create(new { ReferralCode = referralCode });

            var resp = await _httpClient.SendAsync(request);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AttributeInstallAsync failed for code {Code}", referralCode);
            return false;
        }
    }

    /// <summary>
    /// CC-S3 (Sprint 3): Get CustomerId from customerToken via ShopERP /api/customer-identity/me.
    /// Used by ChatPanel to identify the current user (shipper or customer).
    /// </summary>
    public async Task<Guid?> GetCustomerIdAsync(string customerToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("shoperp");
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/customer-identity/me");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await client.SendAsync(request);
            if (!resp.IsSuccessStatusCode) return null;

            var body = await resp.Content.ReadAsStringAsync();
            var data = System.Text.Json.JsonSerializer.Deserialize<CustomerIdResponse>(body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return data?.CustomerId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCustomerIdAsync failed");
            return null;
        }
    }

    /// <summary>
    /// GET /api/community/nearby-orders?lat={lat}&amp;lng={lng}&amp;radiusKm={radius}
    /// Returns DELIVERY orders within radius, sorted by distance.
    /// </summary>
    public async Task<NearbyOrdersResult> GetNearbyOrdersAsync(string customerToken, double lat, double lng, int radiusKm)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"/api/community/nearby-orders?lat={lat}&lng={lng}&radiusKm={radiusKm}");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var orders = System.Text.Json.JsonSerializer.Deserialize<List<NearbyOrderDto>>(body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new List<NearbyOrderDto>();
                return new NearbyOrdersResult { Success = true, Orders = orders };
            }

            var err = System.Text.Json.JsonSerializer.Deserialize<ErrorResponse>(body);
            return new NearbyOrdersResult
            {
                Success = false,
                ErrorCode = (int)resp.StatusCode,
                ErrorMessage = err?.Error ?? $"Lỗi {resp.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetNearbyOrdersAsync failed");
            return new NearbyOrdersResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    /// <summary>
    /// GET /api/community/my-deliveries
    /// Returns the shipper's active delivery tasks (Assigned/PickedUp/OutForDelivery).
    /// </summary>
    public async Task<MyDeliveriesResult> GetMyActiveDeliveriesAsync(string customerToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/community/my-deliveries");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var deliveries = System.Text.Json.JsonSerializer.Deserialize<List<ActiveDeliveryDto>>(body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new List<ActiveDeliveryDto>();
                return new MyDeliveriesResult { Success = true, Deliveries = deliveries };
            }

            var err = System.Text.Json.JsonSerializer.Deserialize<ErrorResponse>(body);
            return new MyDeliveriesResult
            {
                Success = false,
                ErrorCode = (int)resp.StatusCode,
                ErrorMessage = err?.Error ?? $"Lỗi {resp.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMyActiveDeliveriesAsync failed");
            return new MyDeliveriesResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    /// <summary>
    /// POST /api/community/orders/{orderId}/accept
    /// Accept an order for delivery. Returns 409 if already assigned.
    /// </summary>
    public async Task<AcceptOrderResult> AcceptOrderAsync(string customerToken, Guid orderId)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/community/orders/{orderId}/accept");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<AcceptSuccessResponse>(body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new AcceptOrderResult
                {
                    Success = true,
                    DeliveryTaskId = data?.DeliveryTaskId ?? Guid.Empty,
                    Status = data?.Status ?? "Assigned"
                };
            }

            var err = System.Text.Json.JsonSerializer.Deserialize<ErrorResponse>(body);
            return new AcceptOrderResult
            {
                Success = false,
                ErrorCode = (int)resp.StatusCode,
                ErrorMessage = err?.Error ?? $"Lỗi {resp.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AcceptOrderAsync failed for {OrderId}", orderId);
            return new AcceptOrderResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    /// <summary>
    /// CC-S2 (Sprint 2): POST /api/community/orders/{orderId}/pickup
    /// Mark the active DeliveryTask as PickedUp.
    /// </summary>
    public async Task<DeliveryTransitionResult> PickupOrderAsync(string customerToken, Guid orderId)
        => await PostDeliveryTransitionAsync(customerToken, orderId, "pickup");

    /// <summary>
    /// CC-S2 (Sprint 2): POST /api/community/orders/{orderId}/delivering
    /// Mark the active DeliveryTask as OutForDelivery.
    /// </summary>
    public async Task<DeliveryTransitionResult> StartDeliveringAsync(string customerToken, Guid orderId)
        => await PostDeliveryTransitionAsync(customerToken, orderId, "delivering");

    /// <summary>
    /// CC-S2 (Sprint 2): POST /api/community/orders/{orderId}/delivered
    /// Mark the active DeliveryTask as Delivered + Order → completed.
    /// </summary>
    public async Task<DeliveryTransitionResult> CompleteDeliveryAsync(string customerToken, Guid orderId)
        => await PostDeliveryTransitionAsync(customerToken, orderId, "delivered");

    /// <summary>
    /// CC-S2 (Sprint 2): POST /api/community/orders/{orderId}/failed
    /// Mark the active DeliveryTask as Failed with reason.
    /// </summary>
    public async Task<DeliveryTransitionResult> FailDeliveryAsync(string customerToken, Guid orderId, string reason)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/community/orders/{orderId}/failed");
            request.Headers.Add("X-Customer-Token", customerToken);
            request.Content = JsonContent.Create(new { Reason = reason });

            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<DeliveryTransitionResponse>(body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new DeliveryTransitionResult
                {
                    Success = true,
                    DeliveryTaskId = data?.DeliveryTaskId ?? Guid.Empty,
                    Status = data?.Status ?? "Unknown"
                };
            }

            var err = System.Text.Json.JsonSerializer.Deserialize<ErrorResponse>(body);
            return new DeliveryTransitionResult
            {
                Success = false,
                ErrorCode = (int)resp.StatusCode,
                ErrorMessage = err?.Error ?? $"Lỗi {resp.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FailDeliveryAsync failed for {OrderId}", orderId);
            return new DeliveryTransitionResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }

    /// <summary>
    /// CC-S2 (Sprint 2): POST /api/community/location/update
    /// Record a GPS location ping for the DeliveryTask.
    /// </summary>
    public async Task<bool> UpdateLocationAsync(string customerToken, string deliveryTaskId, double lat, double lng)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/community/location/update");
            request.Headers.Add("X-Customer-Token", customerToken);
            request.Content = JsonContent.Create(new LocationUpdateRequest
            {
                DeliveryTaskId = deliveryTaskId,
                Lat = lat,
                Lng = lng
            });

            var resp = await _httpClient.SendAsync(request);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateLocationAsync failed for task {TaskId}", deliveryTaskId);
            return false;
        }
    }

    private async Task<DeliveryTransitionResult> PostDeliveryTransitionAsync(string customerToken, Guid orderId, string action)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/community/orders/{orderId}/{action}");
            request.Headers.Add("X-Customer-Token", customerToken);

            var resp = await _httpClient.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<DeliveryTransitionResponse>(body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new DeliveryTransitionResult
                {
                    Success = true,
                    DeliveryTaskId = data?.DeliveryTaskId ?? Guid.Empty,
                    Status = data?.Status ?? "Unknown"
                };
            }

            var err = System.Text.Json.JsonSerializer.Deserialize<ErrorResponse>(body);
            return new DeliveryTransitionResult
            {
                Success = false,
                ErrorCode = (int)resp.StatusCode,
                ErrorMessage = err?.Error ?? $"Lỗi {resp.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PostDeliveryTransitionAsync failed for {OrderId} action {Action}", orderId, action);
            return new DeliveryTransitionResult { Success = false, ErrorMessage = "Lỗi kết nối." };
        }
    }
}

// === DTOs ===

public class NearbyOrderDto
{
    public Guid OrderId { get; set; }
    public Guid TenantId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public double ShopLat { get; set; }
    public double ShopLng { get; set; }
    public string? DeliveryAddress { get; set; }
    public double? DeliveryLat { get; set; }
    public double? DeliveryLng { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public double DistanceKm { get; set; }
}

public class NearbyOrdersResult
{
    public bool Success { get; set; }
    public List<NearbyOrderDto> Orders { get; set; } = new();
    public int ErrorCode { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class AcceptOrderResult
{
    public bool Success { get; set; }
    public Guid DeliveryTaskId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ErrorCode { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class MyDeliveriesResult
{
    public bool Success { get; set; }
    public List<ActiveDeliveryDto> Deliveries { get; set; } = new();
    public int ErrorCode { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class ActiveDeliveryDto
{
    public Guid OrderId { get; set; }
    public Guid DeliveryTaskId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? OutForDeliveryAt { get; set; }
}

public class AcceptSuccessResponse
{
    public Guid DeliveryTaskId { get; set; }
    public Guid OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ErrorResponse
{
    public string? Error { get; set; }
}

public class CommerceModeResult
{
    public bool Success { get; set; }
    public string GlobalMode { get; set; } = "Marketplace";
    public bool IsReseller { get; set; }
}

public class CommerceModeResponse
{
    public string GlobalMode { get; set; } = "Marketplace";
    public bool IsReseller { get; set; }
}

public class RoleResponse
{
    public bool IsShipper { get; set; }
    public bool IsSalesman { get; set; }
    public bool IsShopOwner { get; set; }
}

public class CommunityRoleDto
{
    public string RoleType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime ActivatedAt { get; set; }
    public DateTime? DeactivatedAt { get; set; }
    public string? SalesmanCode { get; set; }
}

public class MyFraudFlagDto
{
    public Guid Id { get; set; }
    public Guid? CustomerId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public int RiskScore { get; set; }
    public string RiskFactors { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CustomerIdResponse
{
    public Guid? CustomerId { get; set; }
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
}

public class DeliveryTransitionResult
{
    public bool Success { get; set; }
    public Guid DeliveryTaskId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ErrorCode { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class DeliveryTransitionResponse
{
    public Guid DeliveryTaskId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime? Timestamp { get; set; }
}

public class LocationUpdateRequest
{
    public string DeliveryTaskId { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
}

// === CC-S4 (Sprint 4) Salesman DTOs ===

public class NearbyProductDto
{
    public Guid ProductId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public double DistanceKm { get; set; }
    public decimal? CommissionRate { get; set; }
    public decimal? AppInstallBonus { get; set; }
    public string? ProductShortCode { get; set; }
    public bool HasReferralConfig { get; set; }
}

public class CompositeSalesmanQrDto
{
    public string SalesmanCode { get; set; } = string.Empty;
    public string ProductShortCode { get; set; } = string.Empty;
    public string CompositeCode { get; set; } = string.Empty;
    public string QrUrl { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
}

public class CommissionSummaryDto
{
    public decimal TotalSales { get; set; }
    public decimal TotalCommission { get; set; }
    public decimal PendingCommission { get; set; }
    public decimal PaidCommission { get; set; }
    public decimal HeldCommission { get; set; }
    public decimal RejectedCommission { get; set; }
    public decimal TotalAppInstallBonus { get; set; }
    public decimal PendingAppInstallBonus { get; set; }
    public decimal PaidAppInstallBonus { get; set; }
    public List<CommissionRecordDto> CommissionRecords { get; set; } = new();
    public List<AppInstallBonusRecordDto> AppInstallBonusRecords { get; set; } = new();
}

public class CommissionRecordDto
{
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }
    public Guid ProductId { get; set; }
    public decimal OrderTotal { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal CommissionAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AppInstallBonusRecordDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ProductId { get; set; }
    public decimal BonusAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public DateTime InstalledAt { get; set; }
}
