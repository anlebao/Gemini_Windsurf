using System.Text.RegularExpressions;
using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api.Auth;

/// <summary>
/// Giải quyết TrustSubject từ X-Customer-Token: validate qua ShopERP /me → customerId
/// → resolve/lazy-create TrustSubject (FR-002). 401 khi token invalid, 503 khi ShopERP down.
/// ATRS-16: khi KHÔNG có token hợp lệ nhưng có X-Customer-Device-Id → guest path
/// (DeviceCluster lazy-create) — buyer không email/SĐT vẫn định danh được ở tầng thiết bị.
/// Device id chỉ là risk signal, không phải nhân thân pháp lý (FR-004/005).
/// </summary>
public interface IAtrsCustomerContextService
{
    Task<AtrsCustomerAuthResult> AuthenticateAsync(HttpRequest request, CancellationToken ct = default);
}

public sealed class AtrsCustomerContextService(
    IHttpClientFactory httpClientFactory,
    ITrustProfileService trustProfileService) : IAtrsCustomerContextService
{
    private static readonly Regex DeviceIdPattern = new("^[A-Za-z0-9._-]{8,128}$", RegexOptions.Compiled);

    public async Task<AtrsCustomerAuthResult> AuthenticateAsync(HttpRequest request, CancellationToken ct = default)
    {
        var token = request.Headers["X-Customer-Token"].FirstOrDefault();
        var (status, customerId) = await AtrsCustomerTokenHelper.ValidateTokenAsync(httpClientFactory, token, ct);
        if (status != AtrsCustomerTokenStatus.Valid || !customerId.HasValue)
        {
            // ATRS-16: fallback guest device (chỉ khi token path thất bại rõ ràng — invalid/thiếu).
            var deviceId = request.Headers["X-Customer-Device-Id"].FirstOrDefault();
            if (status == AtrsCustomerTokenStatus.InvalidToken && IsValidDeviceId(deviceId))
            {
                var guestSubjectId = await trustProfileService.GetOrCreateForDeviceAsync(deviceId!, ct: ct);
                return new AtrsCustomerAuthResult(AtrsCustomerTokenStatus.GuestDevice, customerId: null, guestSubjectId, IsGuest: true);
            }
            return new AtrsCustomerAuthResult(status, customerId, null);
        }

        var subjectId = await trustProfileService.GetOrCreateForCustomerAsync(customerId.Value, ct);
        return new AtrsCustomerAuthResult(AtrsCustomerTokenStatus.Valid, customerId, subjectId);
    }

    private static bool IsValidDeviceId(string? deviceId)
        => !string.IsNullOrWhiteSpace(deviceId) && DeviceIdPattern.IsMatch(deviceId);
}