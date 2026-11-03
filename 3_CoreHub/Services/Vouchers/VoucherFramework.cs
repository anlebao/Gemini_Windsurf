using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Vouchers;

/// <summary>Task PH/G1 — Voucher Framework (registry-driven): thêm phiếu mới = registry + strategy,
/// không sửa page/ApplyParse/SmartEntryBar. KHÔNG đụng Domain/DB.</summary>

public enum VoucherFieldType { Amount, Date, Party, Account, Select, Text, Item, Quantity }

/// <summary>PH2: thêm Options (select configurable — bỏ hardcode production/sale trong renderer).</summary>
public sealed record VoucherField(
    string Id, VoucherFieldType Type, string Label, bool Required = false, string? SourceParse = null,
    IReadOnlyList<string>? Options = null);

/// <summary>PH2: thêm HostPage — null = generic page /vouchers/{code}; khác null = trang host
/// (vd "/accounting/revenue") — VoucherEntry chặn submit sai đường.</summary>
public sealed record VoucherTypeDefinition(
    string Code, string Name, string Route, string? PrintTemplate,
    IReadOnlyList<VoucherField> Fields, string PostingStrategyKey, string? HostPage = null);

/// <summary>Dữ liệu nộp từ form generic (VoucherEntry) → strategy.</summary>
/// <summary>PH2: thêm LoanAccountCode cho phiếu vay (311/341 — strategy đọc field này).</summary>
public sealed record VoucherSubmission(
    TenantId TenantId, VoucherIntent Intent, decimal? Amount, DateTime Date,
    string? Party, string? CashAccount, Guid? AllocatedToEntryId,
    string? Description = null, string? PaymentMethod = null, string? Mode = null,
    decimal? OriginalCost = null, decimal? AccumulatedDepreciation = null, decimal? LiquidationPrice = null,
    string? LoanAccountCode = null);

public sealed record VoucherLine(string AccountNumber, decimal DebitAmount, decimal CreditAmount);

public sealed record VoucherEntryResult(string? CashAccountCode, IReadOnlyList<VoucherLine> Lines);

public sealed record VoucherPostingResult(VoucherEntryResult? Entry, IReadOnlyList<string> Errors)
{
    public static VoucherPostingResult Ok(VoucherEntryResult e) => new(e, Array.Empty<string>());
    public static VoucherPostingResult Fail(string error) => new(null, new[] { error });
}

public interface IVoucherPostingStrategy
{
    Task<VoucherPostingResult> PostAsync(VoucherSubmission s, CancellationToken ct);
}

/// <summary>L1 — registry: khai báo phiếu + map Smart Entry intent → phiếu.</summary>
public sealed class VoucherTypeRegistry
{
    private readonly Dictionary<string, VoucherTypeDefinition> _byCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<VoucherIntent, string> _byIntent = new();
    private readonly Dictionary<string, IVoucherPostingStrategy> _strategies = new(StringComparer.OrdinalIgnoreCase);
    // PH2: giữ thứ tự đăng ký (dropdown options giữ hành vi form cũ).
    private readonly List<VoucherTypeDefinition> _registered = new();

    /// <summary>PH2: strategy nullable — hosted types (trang host quản lý posting) không cần strategy.</summary>
    public void Register(VoucherTypeDefinition def, IVoucherPostingStrategy? strategy, params VoucherIntent[] intents)
    {
        _byCode[def.Code] = def;
        _registered.Add(def);
        if (strategy != null) _strategies[def.Code] = strategy;
        foreach (var i in intents) _byIntent[i] = def.Code;
    }

    public VoucherTypeDefinition? ResolveByCode(string code)
        => _byCode.TryGetValue(code, out var d) ? d : null;

    public VoucherTypeDefinition? ResolveByIntent(VoucherIntent intent)
        => _byIntent.TryGetValue(intent, out var c) ? _byCode.GetValueOrDefault(c) : null;

    public IVoucherPostingStrategy? ResolveStrategy(string code)
        => _strategies.TryGetValue(code, out var s) ? s : null;

    /// <summary>PH2: options dropdown của 1 trang host (thứ tự = thứ tự Register) — bỏ hardcode GetVoucherTypeOptions.</summary>
    public IReadOnlyList<VoucherTypeDefinition> GetOptionsForHost(string hostPage)
        => _registered.Where(d => string.Equals(d.HostPage, hostPage, StringComparison.OrdinalIgnoreCase)).ToList();
}

/// <summary>L3 — Phiếu Tạm Ứng / Hoàn Ứng: Nợ 141/Có {cash} · Nợ {cash}/Có 141.</summary>
public sealed class AdvancePostingStrategy : IVoucherPostingStrategy
{
    public Task<VoucherPostingResult> PostAsync(VoucherSubmission s, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(s.Party))
        {
            return Task.FromResult(VoucherPostingResult.Fail("Đối tượng (người nhận tạm ứng) là bắt buộc"));
        }
        var cash = s.CashAccount ?? "111";
        var amt = s.Amount ?? 0m;
        var lines = s.Intent == VoucherIntent.AdvanceSettle
            ? new[] { new VoucherLine("111", amt, 0), new VoucherLine("141", 0, amt) }
            : new[] { new VoucherLine("141", amt, 0), new VoucherLine(cash, 0, amt) };
        return Task.FromResult(VoucherPostingResult.Ok(new VoucherEntryResult(cash, lines)));
    }
}

/// <summary>L3 — Phiếu Xuất Kho: SX Nợ 154/Có 152 · bán Nợ 632/Có 152 (bình quân gia quyền).</summary>
public sealed class StockIssuePostingStrategy : IVoucherPostingStrategy
{
    public Task<VoucherPostingResult> PostAsync(VoucherSubmission s, CancellationToken ct)
    {
        var amt = s.Amount ?? 0m;
        var debit = s.Mode == "sale" ? "632" : "154";
        var lines = new[] { new VoucherLine(debit, amt, 0), new VoucherLine("152", 0, amt) };
        return Task.FromResult(VoucherPostingResult.Ok(new VoucherEntryResult(null, lines)));
    }
}

/// <summary>L3 — Thanh Lý TSCĐ: Nợ 214 + Nợ 811(lỗ)/Có 711(lãi) + Nợ {cash} / Có 211.</summary>
public sealed class AssetDisposalPostingStrategy : IVoucherPostingStrategy
{
    public Task<VoucherPostingResult> PostAsync(VoucherSubmission s, CancellationToken ct)
    {
        var cost = s.OriginalCost ?? 0m;
        var dep = s.AccumulatedDepreciation ?? 0m;
        var price = s.LiquidationPrice ?? s.Amount ?? 0m;
        var cash = s.CashAccount ?? "111";
        var diff = cost - dep - price; // >0 lỗ, <0 lãi
        var lines = new List<VoucherLine> { new("214", dep, 0), new(cash, price, 0), new("211", 0, cost) };
        if (diff > 0) lines.Add(new VoucherLine("811", diff, 0));
        else if (diff < 0) lines.Add(new VoucherLine("711", 0, -diff));
        return Task.FromResult(VoucherPostingResult.Ok(new VoucherEntryResult(cash, lines)));
    }
}

/// <summary>PH2 — Vay nhận tiền / Trả nợ vay: loan-in Nợ {cash}/Có {TK vay} · repayment Nợ {TK vay}/Có {cash}.
/// TK vay (311/341 + chi tiết) do user chọn trên form (LoanAccountCode).</summary>
public sealed class LoanPostingStrategy : IVoucherPostingStrategy
{
    public Task<VoucherPostingResult> PostAsync(VoucherSubmission s, CancellationToken ct)
    {
        var amt = s.Amount ?? 0m;
        if (amt <= 0) return Task.FromResult(VoucherPostingResult.Fail("Số tiền phải lớn hơn 0."));
        if (string.IsNullOrWhiteSpace(s.LoanAccountCode))
        {
            return Task.FromResult(VoucherPostingResult.Fail("Chọn tài khoản vay (311/341)."));
        }
        var cash = s.CashAccount ?? "111";
        var loan = s.LoanAccountCode.Trim();
        var lines = s.Intent == VoucherIntent.LoanRepayment
            ? new[] { new VoucherLine(loan, amt, 0), new VoucherLine(cash, 0, amt) }
            : new[] { new VoucherLine(cash, amt, 0), new VoucherLine(loan, 0, amt) };
        return Task.FromResult(VoucherPostingResult.Ok(new VoucherEntryResult(cash, lines)));
    }
}