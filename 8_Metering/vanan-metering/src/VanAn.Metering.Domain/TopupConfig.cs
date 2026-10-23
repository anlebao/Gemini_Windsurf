namespace VanAn.Metering.Domain;

/// <summary>
/// P7 D7 — singleton bank-account config for manual VietQR top-ups (admin-managed
/// via PUT /v1/topup-config). Payload QR is a vietqr.io URL built locally; the
/// image is rendered client-side (no secrets needed — bank info is not secret).
/// </summary>
public sealed class TopupConfig
{
    /// <summary>Fixed id so the row stays a true singleton (upsert by key).</summary>
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    public Guid Id { get; private set; }
    public string BankCode { get; private set; } = string.Empty;   // e.g. "970436" (VietinBank) — vietqr.io bank id
    public string BankName { get; private set; } = string.Empty;
    public string AccountNumber { get; private set; } = string.Empty;
    public string AccountName { get; private set; } = string.Empty;
    public string UpdatedBy { get; private set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; private set; }

    private TopupConfig() { } // EF Core

    public static TopupConfig Create(
        string bankCode, string bankName, string accountNumber, string accountName,
        string updatedBy, DateTime nowUtc)
        => new()
        {
            Id = SingletonId,
            BankCode = bankCode,
            BankName = bankName,
            AccountNumber = accountNumber,
            AccountName = accountName,
            UpdatedBy = updatedBy,
            UpdatedAtUtc = nowUtc
        };

    public void Update(
        string bankCode, string bankName, string accountNumber, string accountName,
        string updatedBy, DateTime nowUtc)
    {
        BankCode = bankCode;
        BankName = bankName;
        AccountNumber = accountNumber;
        AccountName = accountName;
        UpdatedBy = updatedBy;
        UpdatedAtUtc = nowUtc;
    }
}
