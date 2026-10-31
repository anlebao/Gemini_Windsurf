using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Common;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>SRS v1.2 W3/D (M9): quản lý TK tiền per tenant — quỹ 111 mặc định + ngân hàng 112x.</summary>
public interface IBankAccountService
{
    /// <summary>Danh sách TK tiền của tenant (lazy-seed quỹ 111 nếu chưa có).</summary>
    Task<IReadOnlyList<BankAccountEntity>> ListAsync(TenantId tenantId, bool includeInactive = false, CancellationToken ct = default);

    /// <summary>Tạo TK mới — code phải bắt đầu "11" (111 quỹ phụ / 112x ngân hàng), unique per tenant.</summary>
    Task<BankAccountEntity> CreateAsync(TenantId tenantId, string accountCode, string displayName,
        string? bankName = null, string? accountNumber = null, CancellationToken ct = default);

    Task<BankAccountEntity> UpdateAsync(Guid id, string displayName, string? bankName, string? accountNumber, CancellationToken ct = default);

    /// <summary>Deactivate thay vì xóa (giữ lịch sử) — KHÔNG cho deactivate quỹ 111 mặc định.</summary>
    Task DeactivateAsync(Guid id, CancellationToken ct = default);

    /// <summary>Seed idempotent: quỹ mặc định 111 "Tiền mặt" khi tenant chưa có.</summary>
    Task EnsureDefaultAccountsAsync(TenantId tenantId, CancellationToken ct = default);

    /// <summary>
    /// Map hình thức thanh toán → TK tiền: cash-like → "111"; bank-like → TK 112x active
    /// đầu tiên của tenant (không có → "112" generic). Null/unknown → "111" (safe default).
    /// </summary>
    Task<string> ResolveCashAccountCodeAsync(TenantId tenantId, string? paymentMethod, CancellationToken ct = default);
}

public sealed class BankAccountService(
    IAccountingDbContext dbContext,
    ILogger<BankAccountService>? logger = null) : IBankAccountService
{
    public const string DefaultCashCode = "111";
    public const string DefaultCashName = "Tiền mặt — Quỹ mặc định";

    public async Task<IReadOnlyList<BankAccountEntity>> ListAsync(TenantId tenantId, bool includeInactive = false, CancellationToken ct = default)
    {
        await EnsureDefaultAccountsAsync(tenantId, ct);
        return await dbContext.BankAccounts
            .Where(b => b.TenantId == tenantId && (includeInactive || b.IsActive))
            .OrderBy(b => b.AccountCode)
            .ToListAsync(ct);
    }

    public async Task<BankAccountEntity> CreateAsync(TenantId tenantId, string accountCode, string displayName,
        string? bankName = null, string? accountNumber = null, CancellationToken ct = default)
    {
        accountCode = (accountCode ?? string.Empty).Trim();
        if (!IsCashAccountCode(accountCode))
            throw new ArgumentException("Mã TK tiền phải bắt đầu bằng \"11\" (111 quỹ / 112x ngân hàng).", nameof(accountCode));
        if (accountCode == "113")
            throw new ArgumentException("TK 113 (Tiền đang chuyển) là TK hệ thống — dùng cho phiếu chuyển InTransit, không tạo tay.", nameof(accountCode));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Tên hiển thị là bắt buộc.", nameof(displayName));

        bool exists = await dbContext.BankAccounts
            .AnyAsync(b => b.TenantId == tenantId && b.AccountCode == accountCode, ct);
        if (exists)
            throw new InvalidOperationException($"TK {accountCode} đã tồn tại cho tenant này.");

        var account = new BankAccountEntity(tenantId, accountCode, displayName.Trim(), bankName, accountNumber);
        dbContext.BankAccounts.Add(account);
        await dbContext.SaveChangesAsync(ct);
        logger?.LogInformation("BankAccount created: tenant {TenantId} TK {Code} ({Name})", tenantId.Value, accountCode, displayName);
        return account;
    }

    public async Task<BankAccountEntity> UpdateAsync(Guid id, string displayName, string? bankName, string? accountNumber, CancellationToken ct = default)
    {
        BankAccountEntity account = await dbContext.BankAccounts
            .FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new KeyNotFoundException($"BankAccount {id} not found");
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Tên hiển thị là bắt buộc.", nameof(displayName));
        account.Update(displayName.Trim(), bankName, accountNumber);
        await dbContext.SaveChangesAsync(ct);
        return account;
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        BankAccountEntity account = await dbContext.BankAccounts
            .FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new KeyNotFoundException($"BankAccount {id} not found");
        if (account.AccountCode == DefaultCashCode)
            throw new InvalidOperationException("Không thể vô hiệu quỹ tiền mặt mặc định (TK 111).");
        account.Deactivate();
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task EnsureDefaultAccountsAsync(TenantId tenantId, CancellationToken ct = default)
    {
        bool hasCash = await dbContext.BankAccounts
            .AnyAsync(b => b.TenantId == tenantId && b.AccountCode == DefaultCashCode, ct);
        if (hasCash)
        {
            return;
        }
        dbContext.BankAccounts.Add(new BankAccountEntity(tenantId, DefaultCashCode, DefaultCashName));
        await dbContext.SaveChangesAsync(ct);
        logger?.LogInformation("BankAccount seeded default: tenant {TenantId} TK 111", tenantId.Value);
    }

    public async Task<string> ResolveCashAccountCodeAsync(TenantId tenantId, string? paymentMethod, CancellationToken ct = default)
    {
        string mapped = PaymentMethodConstants.MapCashAccount(paymentMethod);
        if (mapped == DefaultCashCode)
        {
            return DefaultCashCode;
        }
        // Bank-like → TK 112x active đầu tiên của tenant (theo code) — không có → "112" generic.
        string? code = await dbContext.BankAccounts
            .Where(b => b.TenantId == tenantId && b.IsActive && b.AccountCode.StartsWith("112"))
            .OrderBy(b => b.AccountCode)
            .Select(b => b.AccountCode)
            .FirstOrDefaultAsync(ct);
        return code ?? "112";
    }

    private static bool IsCashAccountCode(string code)
        => !string.IsNullOrWhiteSpace(code) && code.Length <= 20 && code.StartsWith("11");
}
