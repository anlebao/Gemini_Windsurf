using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Repositories;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Payment;

/// <summary>
/// P0.1 (2026-10-16 — master plan accounting-legal-compliance): Kiểm soát thanh toán ≥ 20 triệu
/// không dùng tiền mặt. Quyết định user Q1: CẢNH BÁO + bắt buộc ghi lý do (không chặn cứng).
/// Multi-tenancy enforced ở mọi query.
/// </summary>
public class PaymentComplianceService(
    IOrderRepository orderRepository,
    IAccountingDbContext db,
    ILogger<PaymentComplianceService> logger) : IPaymentComplianceService
{
    public decimal Threshold => 20_000_000m;

    public Task<PaymentComplianceResult> EvaluateAsync(
        TenantId tenantId,
        decimal amount,
        string? paymentMethod,
        CancellationToken cancellationToken = default)
    {
        bool isCashLike = IsCashLike(paymentMethod);
        return Task.FromResult(new PaymentComplianceResult(
            amount,
            Threshold,
            isCashLike,
            isCashLike && amount >= Threshold));
    }

    public async Task<CashPaymentReviewDto> GetCashPaymentReviewAsync(
        TenantId tenantId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        AccountingPeriod period = AccountingPeriod.FromDateTime(new DateTime(year, month, 1));

        try
        {
            var orders = await orderRepository.GetByDateRangeAsync(
                tenantId, period.StartDate, period.EndDate, cancellationToken);

            var items = orders
                .Where(o => IsCashLike(o.PaymentMethod)
                    && o.TotalAmount >= Threshold
                    && o.PaymentStatus == "Paid")
                .OrderByDescending(o => o.TotalAmount)
                .Select(o => new CashPaymentReviewItemDto(
                    o.Id,
                    o.TrackingCode ?? o.Id.ToString()[..8],
                    o.OrderDate,
                    o.TotalAmount,
                    o.PaymentMethod ?? "CASH"))
                .ToList();

            var entries = await db.AccountingEntries
                .Where(e => e.TenantId == tenantId
                    && e.TransactionDate >= period.StartDate
                    && e.TransactionDate <= period.EndDate
                    && e.Vendor != null)
                .ToListAsync(cancellationToken);

            var aggregates = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Vendor))
                .GroupBy(e => new { Vendor = e.Vendor!.Trim(), Date = e.TransactionDate.Date })
                .Select(g => new { g.Key.Vendor, g.Key.Date, Total = g.Sum(e => Math.Abs(e.Amount)), Count = g.Count() })
                .Where(g => g.Total >= Threshold)
                .OrderByDescending(g => g.Total)
                .Select(g => new SameDayAggregateDto(g.Vendor, g.Date, g.Total, g.Count))
                .ToList();

            return new CashPaymentReviewDto(year, month, Threshold, items, aggregates);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetCashPaymentReviewAsync failed for tenant {TenantId}, period {Year}-{Month}",
                tenantId.Value, year, month);
            throw;
        }
    }

    private static bool IsCashLike(string? paymentMethod)
    {
        string? method = paymentMethod?.Trim().ToUpperInvariant();
        return method == "CASH" || method == "COD";
    }
}