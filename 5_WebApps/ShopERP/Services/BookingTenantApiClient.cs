using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Services;

// ── DTOs (mirror Gateway /api/tenant/booking — P4.2 + P5 extensions) ─────────

public record BookingQueueItemDto(
    Guid BookingId, string PublicBookingCode, string Status, string SubState, string OfferingNameSnapshot,
    DateTime StartAt, DateTime EndAt, decimal EstimatedTotal, decimal? ActualTotal, Guid? StaffId,
    Guid? CustomerId, string? CustomerDeviceId, string? CustomerNote, bool DepositRequired,
    decimal? DepositAmount, string? DepositType, string PaymentStatus, string InvoiceStatus, int Version,
    DateTime CreatedAt, DateTime? CompletedAt, Guid? OrderId);

public record BookingCategoryDto(Guid Id, string Name, int DisplayOrder);

public record BookingOfferingDto(
    Guid Id, Guid? CategoryId, string OfferingType, string DisplayName, int DurationMinutes, decimal Price, string? Description);

public record BookingAddOnDto(Guid Id, string Name, decimal Price);

public record StaffDto(Guid Id, string DisplayName, string? Role, string? AvatarUrl, bool IsActive);

public record StaffServiceDto(Guid OfferingId);

public record WorkingScheduleDto(Guid Id, int Weekday, string StartTime, string EndTime, string? BreakStart, string? BreakEnd);

public record ScheduleOverrideDto(Guid Id, DateTime Date, string OverrideType, string? StartTime, string? EndTime, string? Note);

public record StaffScheduleDto(Guid StaffId, List<WorkingScheduleDto> Schedules, List<ScheduleOverrideDto> Overrides);

public record StaffAvailabilityDto(Guid StaffId, string StaffName, DateTime SlotStartAt, DateTime SlotEndAt, bool IsAvailable, string? UnavailableReason);

public record EligibleStaffDto(Guid StaffId, string StaffName, bool IsAvailable, string? UnavailableReason);

public record BookingTenantConfigDto(
    bool IsEnabled, string DepositPolicy, decimal? DepositFixedAmount, decimal? DepositPercentage,
    string? CancelReschedulePolicy, string? EinvoiceMode);

public record QrChannelDto(Guid Id, string QrTokenHash, Guid? SalesmanId, Guid? CampaignId, bool IsActive, DateTime? RevokedAt, DateTime CreatedAt);

/// <summary>Create response — RawToken chỉ hiện 1 lần tại creation (link đặt lịch; DB chỉ lưu hash §7.2).</summary>
public record QrChannelCreatedDto(
    Guid Id, string QrTokenHash, string RawToken, Guid? SalesmanId, Guid? CampaignId,
    bool IsActive, DateTime? RevokedAt, DateTime CreatedAt);

/// <summary>Q1 (2026-10-08 — issue #188 bug 2): chi tiết QR — link đặt lịch + QR PNG render lại sau khi tạo.</summary>
public record QrChannelDetailDto(
    Guid Id, string? BookingLink, string? QrCodePngBase64, bool IsActive, DateTime? RevokedAt);

public record SalesmanDto(Guid CustomerId, string Name, DateTime CreatedAt);

public record CommissionLedgerDto(
    Guid EntryId, string SourceType, Guid? BookingId, string? BookingCode, Guid SalesmanId,
    decimal BaseAmount, decimal GrossCommissionAmount, decimal TaxWithheldAmount, decimal NetCommissionAmount,
    string? TaxRuleVersion, string? WithholdingReasonCode, string State, Guid? WalletTransactionId,
    DateTime? FinalizedAt, DateTime? PaidAt);

public record DepositItemDto(
    Guid BookingId, string PublicBookingCode, string OfferingName, DateTime StartAt,
    decimal? DepositAmount, string? DepositType, string PaymentStatus,
    Guid? DepositTransactionId, string? DepositStatus, string? CustomerDeviceId, string BookingStatus);

public record FinancialStatusDto(
    Guid BookingId, bool DepositRequired, decimal? DepositAmount, string? DepositType, string? DepositStatus,
    Guid? DepositTransactionId, string PaymentStatus, string InvoiceStatus, string InvoiceTrigger,
    decimal EstimatedTotal, decimal? ActualTotal);

/// <summary>Kết quả call tenant API — Ok=false → Error chứa message thân thiện từ Gateway (§6.5).</summary>
public record ApiResponse<T>(bool Ok, T? Data, string? Error)
{
    public static ApiResponse<T> Success(T data) => new(true, data, null);
    public static ApiResponse<T> Failure(string error) => new(false, default, error);
}

public interface IBookingTenantApiClient
{
    // Queue / transitions
    Task<ApiResponse<List<BookingQueueItemDto>>> GetQueueAsync(BookingStatus? status = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default);
    Task<ApiResponse<BookingQueueItemDto>> GetBookingAsync(Guid bookingId, CancellationToken ct = default);
    // Feature 3 (2026-10-08): staff/owner tạo lịch hẹn thay khách (tenant-scoped, Idempotency-Key bắt buộc).
    Task<ApiResponse<BookingQueueItemDto>> CreateBookingAsync(
        Guid offeringId, IReadOnlyList<Guid> addOnIds, DateTime startAt, Guid? staffId,
        string? customerName, string? customerPhone, string? customerNote,
        string idempotencyKey, CancellationToken ct = default);
    Task<ApiResponse<BookingQueueItemDto>> ConfirmAsync(Guid bookingId, CancellationToken ct = default);
    Task<ApiResponse<BookingQueueItemDto>> RejectAsync(Guid bookingId, string? reason, CancellationToken ct = default);
    Task<ApiResponse<BookingQueueItemDto>> AssignStaffAsync(Guid bookingId, Guid staffId, CancellationToken ct = default);
    Task<ApiResponse<BookingQueueItemDto>> ChangeStaffAsync(Guid bookingId, Guid staffId, CancellationToken ct = default);
    Task<ApiResponse<BookingQueueItemDto>> CheckInAsync(Guid bookingId, CancellationToken ct = default);
    Task<ApiResponse<BookingQueueItemDto>> StartAsync(Guid bookingId, CancellationToken ct = default);
    Task<ApiResponse<BookingQueueItemDto>> CompleteAsync(Guid bookingId, decimal actualTotal, CancellationToken ct = default);
    Task<ApiResponse<BookingQueueItemDto>> NoShowAsync(Guid bookingId, string? reason, CancellationToken ct = default);
    Task<ApiResponse<BookingQueueItemDto>> CancelAsync(Guid bookingId, string? reason, CancellationToken ct = default);
    Task<ApiResponse<List<EligibleStaffDto>>> GetEligibleStaffAsync(Guid bookingId, CancellationToken ct = default);

    // Staff / catalog / schedules
    Task<ApiResponse<List<StaffDto>>> GetStaffAsync(CancellationToken ct = default);
    Task<ApiResponse<StaffDto>> CreateStaffAsync(string displayName, string? role, CancellationToken ct = default);
    Task<ApiResponse<StaffDto>> UpdateStaffAsync(Guid staffId, string displayName, string? role, string? avatarUrl, bool isActive, CancellationToken ct = default);
    Task<ApiResponse<List<StaffServiceDto>>> GetStaffServicesAsync(Guid staffId, CancellationToken ct = default);
    Task<ApiResponse<bool>> SetStaffServicesAsync(Guid staffId, List<Guid> offeringIds, CancellationToken ct = default);
    Task<ApiResponse<List<BookingCategoryDto>>> GetCategoriesAsync(CancellationToken ct = default);
    Task<ApiResponse<List<BookingOfferingDto>>> GetOfferingsAsync(bool activeOnly = false, CancellationToken ct = default);
    Task<ApiResponse<List<BookingAddOnDto>>> GetAddOnsAsync(CancellationToken ct = default);
    Task<ApiResponse<StaffScheduleDto>> GetSchedulesAsync(Guid staffId, CancellationToken ct = default);
    Task<ApiResponse<WorkingScheduleDto>> UpsertScheduleAsync(Guid staffId, int weekday, TimeSpan start, TimeSpan end, TimeSpan? breakStart, TimeSpan? breakEnd, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteScheduleAsync(Guid scheduleId, CancellationToken ct = default);
    Task<ApiResponse<ScheduleOverrideDto>> AddOverrideAsync(Guid staffId, DateOnly date, string overrideType, TimeSpan? start, TimeSpan? end, string? note, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteOverrideAsync(Guid overrideId, CancellationToken ct = default);

    // Availability / calendar
    Task<ApiResponse<List<StaffAvailabilityDto>>> GetAvailabilityMatrixAsync(DateOnly date, Guid? offeringId = null, Guid? staffId = null, CancellationToken ct = default);

    // Config / QR / commission / deposits
    Task<ApiResponse<BookingTenantConfigDto>> GetConfigAsync(CancellationToken ct = default);
    Task<ApiResponse<BookingTenantConfigDto>> UpdateConfigAsync(bool isEnabled, string depositPolicy, decimal? fixedAmount, decimal? percentage, string? cancelPolicy, string? einvoiceMode, CancellationToken ct = default);
    Task<ApiResponse<List<QrChannelDto>>> GetQrChannelsAsync(CancellationToken ct = default);
    Task<ApiResponse<QrChannelCreatedDto>> CreateQrChannelAsync(string qrToken, Guid? salesmanId, Guid? campaignId, DateTime? expiry, CancellationToken ct = default);
    Task<ApiResponse<QrChannelDetailDto>> GetQrChannelAsync(Guid qrId, CancellationToken ct = default);
    Task<ApiResponse<bool>> RevokeQrChannelAsync(Guid qrId, CancellationToken ct = default);
    Task<ApiResponse<List<SalesmanDto>>> GetSalesmenAsync(CancellationToken ct = default);
    Task<ApiResponse<List<CommissionLedgerDto>>> GetCommissionLedgerAsync(Guid? salesmanId = null, Guid? bookingId = null, CancellationToken ct = default);
    Task<ApiResponse<CommissionLedgerDto>> PayCommissionAsync(Guid entryId, CancellationToken ct = default);
    Task<ApiResponse<List<DepositItemDto>>> GetDepositsAsync(CancellationToken ct = default);
    Task<ApiResponse<FinancialStatusDto>> MarkDepositReceivedAsync(Guid bookingId, decimal? amount, CancellationToken ct = default);
    Task<ApiResponse<FinancialStatusDto>> RefundDepositAsync(Guid bookingId, CancellationToken ct = default);
    Task<ApiResponse<FinancialStatusDto>> GetFinancialStatusAsync(Guid bookingId, CancellationToken ct = default);
}

/// <summary>
/// Booking P5.8: ShopERP HTTP proxy cho Gateway tenant booking API (/api/tenant/booking).
/// Architecture: ShopERP KHÔNG query PG trực tiếp — mọi tenant ops qua Gateway (D1).
/// Auth: mint JWT (tenant_id claim từ user đang login) qua GatewayAdminApiClientBase.
/// Graceful degradation: Gateway lỗi → ApiResponse.Error message thân thiện (UI hiển thị §6.5).
/// </summary>
public sealed class BookingTenantApiClient : GatewayAdminApiClientBase, IBookingTenantApiClient
{
    private const string Base = "api/tenant/booking";
    private readonly ILogger<BookingTenantApiClient> _logger;

    public BookingTenantApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IJwtTokenService jwtTokenService,
        AuthenticationStateProvider authStateProvider,
        ILogger<BookingTenantApiClient> logger)
        : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger)
    {
        _logger = logger;
    }

    // ── Queue / transitions ─────────────────────────────────────────────────

    public async Task<ApiResponse<List<BookingQueueItemDto>>> GetQueueAsync(
        BookingStatus? status = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        string url = $"{Base}/bookings";
        var query = new List<string>();
        if (status.HasValue) query.Add($"status={status.Value}");
        if (from.HasValue) query.Add($"from={Uri.EscapeDataString(from.Value.ToString("o"))}");
        if (to.HasValue) query.Add($"to={Uri.EscapeDataString(to.Value.ToString("o"))}");
        if (query.Count > 0) url += "?" + string.Join("&", query);
        return await GetAsync<List<BookingQueueItemDto>>(url, ct);
    }

    public Task<ApiResponse<BookingQueueItemDto>> GetBookingAsync(Guid bookingId, CancellationToken ct = default)
        => GetAsync<BookingQueueItemDto>($"{Base}/bookings/{bookingId}", ct);

    public async Task<ApiResponse<BookingQueueItemDto>> CreateBookingAsync(
        Guid offeringId, IReadOnlyList<Guid> addOnIds, DateTime startAt, Guid? staffId,
        string? customerName, string? customerPhone, string? customerNote,
        string idempotencyKey, CancellationToken ct = default)
    {
        try
        {
            HttpRequestMessage request = await CreateRequestAsync(HttpMethod.Post, $"{Base}/bookings", new
            {
                offeringId,
                addOnIds,
                startAt,
                staffId,
                customerName,
                customerPhone,
                customerNote
            }, null);
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            }
            return await SendAsync<BookingQueueItemDto>(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BookingTenantApi POST {Url} failed", $"{Base}/bookings");
            return ApiResponse<BookingQueueItemDto>.Failure("Không kết nối được máy chủ đặt lịch.");
        }
    }

    public Task<ApiResponse<BookingQueueItemDto>> ConfirmAsync(Guid bookingId, CancellationToken ct = default)
        => PostAsync<BookingQueueItemDto>($"{Base}/bookings/{bookingId}/confirm", null, ct);

    public Task<ApiResponse<BookingQueueItemDto>> RejectAsync(Guid bookingId, string? reason, CancellationToken ct = default)
        => PostAsync<BookingQueueItemDto>($"{Base}/bookings/{bookingId}/reject", new { reason }, ct);

    public Task<ApiResponse<BookingQueueItemDto>> AssignStaffAsync(Guid bookingId, Guid staffId, CancellationToken ct = default)
        => PostAsync<BookingQueueItemDto>($"{Base}/bookings/{bookingId}/assign-staff", new { staffId }, ct);

    public Task<ApiResponse<BookingQueueItemDto>> ChangeStaffAsync(Guid bookingId, Guid staffId, CancellationToken ct = default)
        => PostAsync<BookingQueueItemDto>($"{Base}/bookings/{bookingId}/change-staff", new { staffId }, ct);

    public Task<ApiResponse<BookingQueueItemDto>> CheckInAsync(Guid bookingId, CancellationToken ct = default)
        => PostAsync<BookingQueueItemDto>($"{Base}/bookings/{bookingId}/check-in", null, ct);

    public Task<ApiResponse<BookingQueueItemDto>> StartAsync(Guid bookingId, CancellationToken ct = default)
        => PostAsync<BookingQueueItemDto>($"{Base}/bookings/{bookingId}/start", null, ct);

    public Task<ApiResponse<BookingQueueItemDto>> CompleteAsync(Guid bookingId, decimal actualTotal, CancellationToken ct = default)
        => PostAsync<BookingQueueItemDto>($"{Base}/bookings/{bookingId}/complete", new { actualTotal }, ct);

    public Task<ApiResponse<BookingQueueItemDto>> NoShowAsync(Guid bookingId, string? reason, CancellationToken ct = default)
        => PostAsync<BookingQueueItemDto>($"{Base}/bookings/{bookingId}/no-show", new { reason }, ct);

    public Task<ApiResponse<BookingQueueItemDto>> CancelAsync(Guid bookingId, string? reason, CancellationToken ct = default)
        => PostAsync<BookingQueueItemDto>($"{Base}/bookings/{bookingId}/cancel", new { reason }, ct);

    public Task<ApiResponse<List<EligibleStaffDto>>> GetEligibleStaffAsync(Guid bookingId, CancellationToken ct = default)
        => GetAsync<List<EligibleStaffDto>>($"{Base}/bookings/{bookingId}/eligible-staff", ct);

    // ── Staff / catalog / schedules ─────────────────────────────────────────

    public Task<ApiResponse<List<StaffDto>>> GetStaffAsync(CancellationToken ct = default)
        => GetAsync<List<StaffDto>>($"{Base}/staff", ct);

    public Task<ApiResponse<StaffDto>> CreateStaffAsync(string displayName, string? role, CancellationToken ct = default)
        => PostAsync<StaffDto>($"{Base}/staff", new { displayName, role }, ct);

    public Task<ApiResponse<StaffDto>> UpdateStaffAsync(Guid staffId, string displayName, string? role, string? avatarUrl, bool isActive, CancellationToken ct = default)
        => PostAsync<StaffDto>($"{Base}/staff/{staffId}", new { displayName, role, avatarUrl, isActive }, ct, HttpMethod.Put);

    public Task<ApiResponse<List<StaffServiceDto>>> GetStaffServicesAsync(Guid staffId, CancellationToken ct = default)
        => GetAsync<List<StaffServiceDto>>($"{Base}/staff/{staffId}/services", ct);

    public Task<ApiResponse<bool>> SetStaffServicesAsync(Guid staffId, List<Guid> offeringIds, CancellationToken ct = default)
        => PostAsync<bool>($"{Base}/staff/{staffId}/services", new { offeringIds }, ct);

    public Task<ApiResponse<List<BookingCategoryDto>>> GetCategoriesAsync(CancellationToken ct = default)
        => GetAsync<List<BookingCategoryDto>>($"{Base}/categories", ct);

    public Task<ApiResponse<List<BookingOfferingDto>>> GetOfferingsAsync(bool activeOnly = false, CancellationToken ct = default)
        => GetAsync<List<BookingOfferingDto>>($"{Base}/offerings?activeOnly={activeOnly}", ct);

    public Task<ApiResponse<List<BookingAddOnDto>>> GetAddOnsAsync(CancellationToken ct = default)
        => GetAsync<List<BookingAddOnDto>>($"{Base}/add-ons", ct);

    public Task<ApiResponse<StaffScheduleDto>> GetSchedulesAsync(Guid staffId, CancellationToken ct = default)
        => GetAsync<StaffScheduleDto>($"{Base}/staff/{staffId}/schedules", ct);

    public Task<ApiResponse<WorkingScheduleDto>> UpsertScheduleAsync(Guid staffId, int weekday, TimeSpan start, TimeSpan end, TimeSpan? breakStart, TimeSpan? breakEnd, CancellationToken ct = default)
        => PostAsync<WorkingScheduleDto>($"{Base}/staff/{staffId}/schedules",
            new { weekday, startTime = ToHm(start), endTime = ToHm(end), breakStart = ToHm(breakStart), breakEnd = ToHm(breakEnd) }, ct);

    public Task<ApiResponse<bool>> DeleteScheduleAsync(Guid scheduleId, CancellationToken ct = default)
        => SendDeleteAsync($"{Base}/schedules/{scheduleId}", ct);

    public Task<ApiResponse<ScheduleOverrideDto>> AddOverrideAsync(Guid staffId, DateOnly date, string overrideType, TimeSpan? start, TimeSpan? end, string? note, CancellationToken ct = default)
        => PostAsync<ScheduleOverrideDto>($"{Base}/staff/{staffId}/overrides",
            new { date = date.ToString("yyyy-MM-dd"), overrideType, startTime = ToHm(start), endTime = ToHm(end), note }, ct);

    public Task<ApiResponse<bool>> DeleteOverrideAsync(Guid overrideId, CancellationToken ct = default)
        => SendDeleteAsync($"{Base}/overrides/{overrideId}", ct);

    // ── Availability / calendar ─────────────────────────────────────────────

    public Task<ApiResponse<List<StaffAvailabilityDto>>> GetAvailabilityMatrixAsync(DateOnly date, Guid? offeringId = null, Guid? staffId = null, CancellationToken ct = default)
    {
        string url = $"{Base}/availability?date={date:yyyy-MM-dd}";
        if (offeringId.HasValue) url += $"&offeringId={offeringId}";
        if (staffId.HasValue) url += $"&staffId={staffId}";
        return GetAsync<List<StaffAvailabilityDto>>(url, ct);
    }

    // ── Config / QR / commission / deposits ─────────────────────────────────

    public Task<ApiResponse<BookingTenantConfigDto>> GetConfigAsync(CancellationToken ct = default)
        => GetAsync<BookingTenantConfigDto>($"{Base}/config", ct);

    public Task<ApiResponse<BookingTenantConfigDto>> UpdateConfigAsync(bool isEnabled, string depositPolicy, decimal? fixedAmount, decimal? percentage, string? cancelPolicy, string? einvoiceMode, CancellationToken ct = default)
        => PutAsync<BookingTenantConfigDto>($"{Base}/config",
            new { isEnabled, depositPolicy, depositFixedAmount = fixedAmount, depositPercentage = percentage, cancelReschedulePolicy = cancelPolicy, einvoiceMode }, ct);

    public Task<ApiResponse<List<QrChannelDto>>> GetQrChannelsAsync(CancellationToken ct = default)
        => GetAsync<List<QrChannelDto>>($"{Base}/qr-channels", ct);

    public Task<ApiResponse<QrChannelCreatedDto>> CreateQrChannelAsync(string qrToken, Guid? salesmanId, Guid? campaignId, DateTime? expiry, CancellationToken ct = default)
        => PostAsync<QrChannelCreatedDto>($"{Base}/qr-channels", new { qrToken, salesmanId, campaignId, attributionExpiryAt = expiry }, ct);

    public Task<ApiResponse<bool>> RevokeQrChannelAsync(Guid qrId, CancellationToken ct = default)
        => PostAsync<bool>($"{Base}/qr-channels/{qrId}/revoke", null, ct);

    public Task<ApiResponse<QrChannelDetailDto>> GetQrChannelAsync(Guid qrId, CancellationToken ct = default)
        => GetAsync<QrChannelDetailDto>($"{Base}/qr-channels/{qrId}", ct);

    public Task<ApiResponse<List<SalesmanDto>>> GetSalesmenAsync(CancellationToken ct = default)
        => GetAsync<List<SalesmanDto>>($"{Base}/salesmen", ct);

    public Task<ApiResponse<List<CommissionLedgerDto>>> GetCommissionLedgerAsync(Guid? salesmanId = null, Guid? bookingId = null, CancellationToken ct = default)
    {
        string url = $"{Base}/commission/ledger";
        var query = new List<string>();
        if (salesmanId.HasValue) query.Add($"salesmanId={salesmanId}");
        if (bookingId.HasValue) query.Add($"bookingId={bookingId}");
        if (query.Count > 0) url += "?" + string.Join("&", query);
        return GetAsync<List<CommissionLedgerDto>>(url, ct);
    }

    public Task<ApiResponse<CommissionLedgerDto>> PayCommissionAsync(Guid entryId, CancellationToken ct = default)
        => PostAsync<CommissionLedgerDto>($"{Base}/commission/ledger/{entryId}/pay", null, ct);

    public Task<ApiResponse<List<DepositItemDto>>> GetDepositsAsync(CancellationToken ct = default)
        => GetAsync<List<DepositItemDto>>($"{Base}/deposits", ct);

    public Task<ApiResponse<FinancialStatusDto>> MarkDepositReceivedAsync(Guid bookingId, decimal? amount, CancellationToken ct = default)
        => PostAsync<FinancialStatusDto>($"{Base}/bookings/{bookingId}/deposit/received", new { amount }, ct);

    public Task<ApiResponse<FinancialStatusDto>> RefundDepositAsync(Guid bookingId, CancellationToken ct = default)
        => PostAsync<FinancialStatusDto>($"{Base}/bookings/{bookingId}/deposit/refund", null, ct);

    public Task<ApiResponse<FinancialStatusDto>> GetFinancialStatusAsync(Guid bookingId, CancellationToken ct = default)
        => GetAsync<FinancialStatusDto>($"{Base}/bookings/{bookingId}/financial-status", ct);

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<ApiResponse<T>> GetAsync<T>(string url, CancellationToken ct)
    {
        try
        {
            HttpRequestMessage request = await CreateRequestAsync(HttpMethod.Get, url, null, null);
            return await SendAsync<T>(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BookingTenantApi GET {Url} failed", url);
            return ApiResponse<T>.Failure("Không kết nối được máy chủ đặt lịch.");
        }
    }

    private async Task<ApiResponse<T>> PostAsync<T>(string url, object? body, CancellationToken ct, HttpMethod? method = null)
    {
        try
        {
            HttpRequestMessage request = await CreateRequestAsync(method ?? HttpMethod.Post, url, body, null);
            return await SendAsync<T>(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BookingTenantApi POST {Url} failed", url);
            return ApiResponse<T>.Failure("Không kết nối được máy chủ đặt lịch.");
        }
    }

    private async Task<ApiResponse<T>> PutAsync<T>(string url, object? body, CancellationToken ct)
        => await PostAsync<T>(url, body, ct, HttpMethod.Put);

    private async Task<ApiResponse<bool>> SendDeleteAsync(string url, CancellationToken ct)
    {
        try
        {
            HttpRequestMessage request = await CreateRequestAsync(HttpMethod.Delete, url, null, null);
            HttpResponseMessage response = await HttpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return ApiResponse<bool>.Success(true);
            return ApiResponse<bool>.Failure(await ReadErrorAsync(response, ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BookingTenantApi DELETE {Url} failed", url);
            return ApiResponse<bool>.Failure("Không kết nối được máy chủ đặt lịch.");
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
        return ApiResponse<T>.Failure(await ReadErrorAsync(response, ct));
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<GatewayErrorDto>(ct);
            if (!string.IsNullOrWhiteSpace(error?.Message))
                return error.Message;
        }
        catch { /* fallthrough */ }
        return $"Máy chủ trả về lỗi ({(int)response.StatusCode}). Vui lòng thử lại.";
    }

    private static string? ToHm(TimeSpan? time) => time.HasValue ? time.Value.ToString(@"hh\:mm") : null;

    private class GatewayErrorDto
    {
        public string? Message { get; set; }
    }
}
