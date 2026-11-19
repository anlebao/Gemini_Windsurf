namespace VanAn.Metering.Domain;

/// <summary>
/// P11 (task_p11_package_catalog) — sysadmin-defined platform package.
/// Fixed PriceVnd + fixed Credits + ValidityDays. Buying a package creates a
/// credit lot expiring ValidityDays after confirm (P10). Custom top-ups
/// (PackageCode=null) stay non-expiring — pricing differentiation is the
/// incentive to buy packages.
/// </summary>
public sealed class CreditPackage
{
    public Guid Id { get; private set; }

    /// <summary>Stable code referenced by TopupRequest.PackageCode ("DAY","WEEK",...).</summary>
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public decimal PriceVnd { get; private set; }
    public decimal Credits { get; private set; }
    public int ValidityDays { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private CreditPackage() { } // EF Core

    public static CreditPackage Create(string code, string name, decimal priceVnd,
        decimal credits, int validityDays, bool isActive, int sortOrder = 0, DateTime? nowUtc = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("code required", nameof(code));
        if (priceVnd <= 0) throw new ArgumentException("priceVnd must be > 0", nameof(priceVnd));
        if (credits <= 0) throw new ArgumentException("credits must be > 0", nameof(credits));
        if (validityDays <= 0) throw new ArgumentException("validityDays must be > 0", nameof(validityDays));
        return new()
        {
            Id = Guid.NewGuid(),
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            PriceVnd = priceVnd,
            Credits = credits,
            ValidityDays = validityDays,
            SortOrder = sortOrder,
            IsActive = isActive,
            UpdatedAtUtc = nowUtc ?? DateTime.UtcNow
        };
    }

    public void Update(string name, decimal priceVnd, decimal credits, int validityDays,
        int sortOrder, bool isActive, DateTime nowUtc)
    {
        Name = name.Trim();
        PriceVnd = priceVnd;
        Credits = credits;
        ValidityDays = validityDays;
        SortOrder = sortOrder;
        IsActive = isActive;
        UpdatedAtUtc = nowUtc;
    }
}
