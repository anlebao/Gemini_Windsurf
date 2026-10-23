using System.Globalization;
using VanAn.Metering.Domain;

namespace VanAn.Metering.Infra;

/// <summary>
/// P7 D7 — vietqr.io URL builder (pattern VietQrService/BankConfig đang production):
/// https://img.vietqr.io/image/{bank}-{acc}-compact.jpg?amount=&addInfo=&accountName=
/// Pure function — image rendered client-side, no server-side secret needed.
/// </summary>
public static class VietQrPayloadBuilder
{
    public static string Build(TopupConfig config, string referenceCode, decimal? amountVnd)
    {
        var url = $"https://img.vietqr.io/image/{config.BankCode}-{config.AccountNumber}-compact.jpg";
        var parameters = new List<string>();
        if (amountVnd is > 0)
            parameters.Add($"amount={amountVnd.Value.ToString("0", CultureInfo.InvariantCulture)}");
        parameters.Add($"addInfo={Uri.EscapeDataString(referenceCode)}");
        if (!string.IsNullOrWhiteSpace(config.AccountName))
            parameters.Add($"accountName={Uri.EscapeDataString(config.AccountName)}");
        return url + "?" + string.Join("&", parameters);
    }
}
