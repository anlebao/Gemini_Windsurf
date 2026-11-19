using Microsoft.Extensions.Configuration;

namespace VanAn.Metering.Infra;

/// <summary>
/// P8 (task_p8_tingee_topup) — config section "Tingee". All values come from env
/// (Tingee__*) — never hard-code secrets. Missing ClientId/ClientSecret disables the
/// feature cleanly (IsEnabled=false → pay-link fails, webhook 401s).
/// </summary>
public sealed class TingeeOptions
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    /// <summary>UAT: https://uat-open-api.tingee.vn · PROD: https://open-api.tingee.vn</summary>
    public string BaseUrl { get; set; } = "https://uat-open-api.tingee.vn";
    /// <summary>Secret of the "Xác thực webhook" credentials — used ONLY for the
    /// url-validation encryptedToken (HMAC-SHA256). Falls back to ClientSecret.</summary>
    public string? WebhookSecret { get; set; }
    /// <summary>Customer return URL after checkout (ShopERP wallet page).</summary>
    public string? ReturnUrl { get; set; }
    /// <summary>Tingee expireInMinute allowed range is 5–30; default 30.</summary>
    public int ExpireInMinute { get; set; } = 30;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    /// <summary>Secret used to verify inbound x-signature headers on webhooks.</summary>
    public string VerifySecret => WebhookSecret ?? ClientSecret ?? string.Empty;

    /// <summary>Manual bind — avoids a Configuration.Binder package dependency in Infra.</summary>
    public static TingeeOptions From(IConfiguration config)
    {
        var s = config.GetSection("Tingee");
        return new TingeeOptions
        {
            ClientId = s["ClientId"],
            ClientSecret = s["ClientSecret"],
            BaseUrl = s["BaseUrl"] ?? "https://uat-open-api.tingee.vn",
            WebhookSecret = s["WebhookSecret"],
            ReturnUrl = s["ReturnUrl"],
            ExpireInMinute = int.TryParse(s["ExpireInMinute"], out var m) ? m : 30
        };
    }
}
