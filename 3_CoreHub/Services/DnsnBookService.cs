using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Template;
using VanAn.Shared.Domain;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.CoreHub.Services;

/// <summary>
/// TT 58/2026/TT-BTC — DNSN Book Service (P2).
/// Sinh danh mục sổ bắt buộc theo DnsnTaxMethod (Điều 5-8) + generate từng sổ
/// từ JournalEntry (tái dùng TemplateFactory + formula engine của HKD books).
/// DnsnTaxMethod đọc từ Tenant.Settings.DnsnTaxMethod — null = VatPercent_TndnPercent (phổ biến nhất).
/// </summary>
public class DnsnBookService(
    IVanAnDbContext context,
    IAccountingDbContext accountingContext,
    VanAn.CoreHub.Services.Template.TemplateFactory templateFactory,
    ILogger<DnsnBookService> logger) : IDnsnBookService
{
    /// <inheritdoc />
    public async Task<List<DnsnBookInfo>> GetDnsnBookListAsync(TenantId tenantId, CancellationToken ct = default)
    {
        DnsnTaxMethod method = await ResolveTaxMethodAsync(tenantId, ct);
        return VanAn.CoreHub.Services.Template.TemplateFactory.GetDnsnBookCodes(method)
            .Select(code => new DnsnBookInfo(code, DnsnBookNames.TryGetValue(code, out var n) ? n : code, method))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<DnsnTaxMethod> ResolveTaxMethodAsync(TenantId tenantId, CancellationToken ct = default)
    {
        Tenant? tenant = await context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new ArgumentException($"Tenant {tenantId.Value} not found");
        return tenant.Settings?.DnsnTaxMethod ?? DnsnTaxMethod.VatPercent_TndnPercent;
    }

    /// <inheritdoc />
    public async Task<GenericHKDBook> GenerateBookAsync(
        TenantId tenantId, AccountingPeriod period, string templateCode, CancellationToken ct = default)
    {
        logger.LogInformation("Generating DNSN book {TemplateCode} for tenant {TenantId}, period {Period}",
            templateCode, tenantId.Value, period);

        List<JournalEntry> entries = await GetJournalEntriesAsync(tenantId, period, ct);
        HKDBookTemplate template = templateFactory.CreateDnsnTemplate(templateCode);
        return await template.CreateBookAsync(tenantId, period, entries);
    }

    /// <inheritdoc />
    public async Task<List<GenericHKDBook>> GenerateAllBooksAsync(
        TenantId tenantId, AccountingPeriod period, CancellationToken ct = default)
    {
        DnsnTaxMethod method = await ResolveTaxMethodAsync(tenantId, ct);
        List<JournalEntry> entries = await GetJournalEntriesAsync(tenantId, period, ct);

        var books = new List<GenericHKDBook>();
        foreach (string code in VanAn.CoreHub.Services.Template.TemplateFactory.GetDnsnBookCodes(method))
        {
            try
            {
                books.Add(await templateFactory.CreateDnsnTemplate(code)
                    .CreateBookAsync(tenantId, period, entries));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error generating DNSN book {TemplateCode} for tenant {TenantId}",
                    code, tenantId.Value);
            }
        }
        return books;
    }

    private async Task<List<JournalEntry>> GetJournalEntriesAsync(
        TenantId tenantId, AccountingPeriod period, CancellationToken ct)
    {
        DateTime periodStart = new(period.Year, period.Month, 1);
        DateTime periodEnd = periodStart.AddMonths(1);

        return await accountingContext.JournalEntries
            .Where(e => e.TenantId == tenantId &&
                       e.EntryDate >= periodStart &&
                       e.EntryDate < periodEnd)
            .OrderBy(e => e.EntryDate)
            .ToListAsync(ct);
    }

    private static readonly Dictionary<string, string> DnsnBookNames = new()
    {
        ["S1_DNSN"] = "Sổ S1-DNSN — Sổ doanh thu (Điều 5)",
        ["S2a_DNSN"] = "Sổ S2a-DNSN — Sổ doanh thu (Điều 6)",
        ["S2b_DNSN"] = "Sổ S2b-DNSN — Sổ chi tiết doanh thu, chi phí",
        ["S2c_DNSN"] = "Sổ S2c-DNSN — Sổ chi tiết VL, DC, SP, HH",
        ["S2d_DNSN"] = "Sổ S2d-DNSN — Sổ chi tiết tiền",
        ["S3a_DNSN"] = "Sổ S3a-DNSN — Sổ doanh thu (Điều 7)",
        ["S3b_DNSN"] = "Sổ S3b-DNSN — Sổ theo dõi nghĩa vụ thuế GTGT",
        ["S4a_DNSN"] = "Sổ S4a-DNSN — Sổ chi tiết thanh toán (Điều 9)",
    };
}

/// <summary>DNSN book service interface.</summary>
public interface IDnsnBookService
{
    Task<List<DnsnBookInfo>> GetDnsnBookListAsync(TenantId tenantId, CancellationToken ct = default);
    Task<DnsnTaxMethod> ResolveTaxMethodAsync(TenantId tenantId, CancellationToken ct = default);
    Task<GenericHKDBook> GenerateBookAsync(TenantId tenantId, AccountingPeriod period, string templateCode, CancellationToken ct = default);
    Task<List<GenericHKDBook>> GenerateAllBooksAsync(TenantId tenantId, AccountingPeriod period, CancellationToken ct = default);
}

/// <summary>Thông tin sổ DNSN áp dụng cho tenant.</summary>
public record DnsnBookInfo(string TemplateCode, string DisplayName, DnsnTaxMethod Method);
