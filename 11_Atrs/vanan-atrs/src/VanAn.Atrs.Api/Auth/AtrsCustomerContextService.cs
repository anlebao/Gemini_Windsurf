using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Api.Auth;

/// <summary>
/// Giải quyết TrustSubject từ X-Customer-Token: validate qua ShopERP /me → customerId
/// → resolve/lazy-create TrustSubject (FR-002). 401 khi token invalid, 503 khi ShopERP down.
/// </summary>
public interface IAtrsCustomerContextService
{
    Task<AtrsCustomerAuthResult> AuthenticateAsync(HttpRequest request, CancellationToken ct = default);
}

public sealed class AtrsCustomerContextService(
    IHttpClientFactory httpClientFactory,
    ITrustProfileService trustProfileService) : IAtrsCustomerContextService
{
    public async Task<AtrsCustomerAuthResult> AuthenticateAsync(HttpRequest request, CancellationToken ct = default)
    {
        var token = request.Headers["X-Customer-Token"].FirstOrDefault();
        var (status, customerId) = await AtrsCustomerTokenHelper.ValidateTokenAsync(httpClientFactory, token, ct);
        if (status != AtrsCustomerTokenStatus.Valid || !customerId.HasValue)
            return new AtrsCustomerAuthResult(status, customerId, null);

        var subjectId = await trustProfileService.GetOrCreateForCustomerAsync(customerId.Value, ct);
        return new AtrsCustomerAuthResult(AtrsCustomerTokenStatus.Valid, customerId, subjectId);
    }
}