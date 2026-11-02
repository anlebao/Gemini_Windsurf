using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Vouchers;

/// <summary>Task PH/G1 — Voucher Framework (registry-driven): thêm phiếu mới = registry + strategy,
/// không sửa page/ApplyParse/SmartEntryBar. KHÔNG đụng Domain/DB.</summary>

public enum VoucherFieldType { Amount, Date, Party, Account, Select, Text, Item, Quantity }

public sealed record VoucherField(string Id, VoucherFieldType Type, string Label, bool Required = false, string? SourceParse = null);

public sealed record VoucherTypeDefinition(
    string Code, string Name, string Route, string? PrintTemplate,
    IReadOnlyList<VoucherField> Fields, string PostingStrategyKey);

/// <summary>Dữ liệu nộp từ form generic (VoucherEntry) → strategy.</summary>
public sealed record VoucherSubmission(
    TenantId TenantId, VoucherIntent Intent, decimal? Amount, DateTime Date,
    string? Party, string? CashAccount, Guid? AllocatedToEntryId,
    string? Description = null, string? PaymentMethod = null);

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

    public void Register(VoucherTypeDefinition def, params VoucherIntent[] intents)
    {
        _byCode[def.Code] = def;
        foreach (var i in intents) _byIntent[i] = def.Code;
    }

    public VoucherTypeDefinition? ResolveByCode(string code)
        => _byCode.TryGetValue(code, out var d) ? d : null;

    public VoucherTypeDefinition? ResolveByIntent(VoucherIntent intent)
        => _byIntent.TryGetValue(intent, out var c) ? _byCode.GetValueOrDefault(c) : null;
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