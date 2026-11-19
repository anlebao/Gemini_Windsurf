using Microsoft.EntityFrameworkCore;
using VanAn.Metering.Contracts;
using VanAn.Metering.Domain;

namespace VanAn.Metering.Infra;

/// <summary>
/// P11 — package catalog service. Sysadmin-defined global packages;
/// tenants only read the active list. Upsert is keyed by Code (stable id).
/// </summary>
public sealed class PackageService(MeteringDbContext db)
{
    public async Task<IReadOnlyList<PackageDto>> ListActiveAsync(CancellationToken ct = default)
        => await db.CreditPackages.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Code)
            .Select(p => new PackageDto(p.Code, p.Name, p.PriceVnd, p.Credits, p.ValidityDays, p.SortOrder))
            .ToListAsync(ct);

    public async Task<CreditPackage?> GetByCodeAsync(string code, bool activeOnly, CancellationToken ct = default)
    {
        var q = db.CreditPackages.AsNoTracking().Where(p => p.Code == code.ToUpperInvariant());
        if (activeOnly) q = q.Where(p => p.IsActive);
        return await q.FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<CreditPackage>> ListAllAsync(CancellationToken ct = default)
        => await db.CreditPackages.AsNoTracking()
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Code).ToListAsync(ct);

    /// <summary>Upsert by Code — creates or updates the package definition.</summary>
    public async Task<CreditPackage> UpsertAsync(string code, string name, decimal priceVnd,
        decimal credits, int validityDays, int sortOrder, bool isActive,
        string actor, DateTime nowUtc, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var pkg = await db.CreditPackages.FirstOrDefaultAsync(p => p.Code == normalized, ct);
        if (pkg == null)
        {
            pkg = CreditPackage.Create(normalized, name, priceVnd, credits, validityDays, isActive, sortOrder, nowUtc);
            db.CreditPackages.Add(pkg);
        }
        else
        {
            pkg.Update(name, priceVnd, credits, validityDays, sortOrder, isActive, nowUtc);
        }
        db.AuditLogs.Add(AuditLog.Create(actor, pkg.Id == default ? "create" : "update",
            nameof(CreditPackage), pkg.Id, oldJson: null,
            newJson: $"{{\"code\":\"{pkg.Code}\",\"priceVnd\":{priceVnd},\"credits\":{credits},\"validityDays\":{validityDays}}}",
            atUtc: nowUtc));
        await db.SaveChangesAsync(ct);
        return pkg;
    }

    public async Task<bool> DeactivateAsync(string code, string actor, DateTime nowUtc, CancellationToken ct = default)
    {
        var pkg = await db.CreditPackages.FirstOrDefaultAsync(p => p.Code == code.ToUpperInvariant(), ct);
        if (pkg == null) return false;
        pkg.Update(pkg.Name, pkg.PriceVnd, pkg.Credits, pkg.ValidityDays, pkg.SortOrder, false, nowUtc);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
