using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.GeneralLedger;

/// <summary>
/// P0.2 (2026-10-16 — master plan accounting-legal-compliance): Sổ Nhật ký chung + Sổ Cái cho DN
/// (TT 133/2016 · TT 99/2025 · TT 71/2024). Nguồn dữ liệu: JournalEntry (double-entry) — đọc thuần,
/// KHÔNG ghi, KHÔNG đụng Domain.cs. Multi-tenancy enforced ở mọi query.
/// </summary>
public interface IGeneralLedgerService
{
    /// <summary>Sổ Nhật ký chung — toàn bộ bút toán của kỳ theo thứ tự thời gian.</summary>
    Task<IReadOnlyList<JournalEntryDto>> GetJournalAsync(
        TenantId tenantId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    /// <summary>Sổ Cái TK — dư đầu kỳ + phát sinh Nợ/Có + dư cuối kỳ (cộng dồn theo thời gian).</summary>
    Task<LedgerReportDto> GetLedgerAsync(
        TenantId tenantId,
        int year,
        int month,
        string accountCode,
        CancellationToken cancellationToken = default);

    /// <summary>Số dư theo TK cuối kỳ (đối chiếu + nguồn cho P1.3 cảnh báo âm quỹ/âm kho).</summary>
    Task<IReadOnlyList<AccountBalanceDto>> GetBalancesAsync(
        TenantId tenantId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    /// <summary>Danh sách TK có phát sinh trong kỳ (dropdown Sổ Cái).</summary>
    Task<IReadOnlyList<AccountActivityDto>> GetAccountsAsync(
        TenantId tenantId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    /// <summary>Thông tin đơn vị (tên + địa chỉ) cho phần đầu Sổ Cái/Sổ Nhật ký xuất Excel (Mẫu S02c1-DN/S03b-DN).</summary>
    Task<TenantInfoDto?> GetTenantInfoAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Lũy kế phát sinh Nợ/Có của TK từ đầu quý đến hết kỳ (dòng "Cộng lũy kế từ đầu quý" — mẫu S02c1-DN).</summary>
    Task<QuarterTotalsDto> GetQuarterTotalsAsync(
        TenantId tenantId,
        int year,
        int month,
        string accountCode,
        CancellationToken cancellationToken = default);
}

/// <summary>Dòng chi tiết của bút toán (Nợ/Có).</summary>
public sealed record JournalEntryLineDto(
    string AccountNumber,
    decimal DebitAmount,
    decimal CreditAmount,
    string? Description);

/// <summary>Một bút toán trong Sổ Nhật ký chung.</summary>
public sealed record JournalEntryDto(
    Guid Id,
    string JournalNo,
    DateTime EntryDate,
    string Description,
    string? ReferenceType,
    bool IsReversal,
    Guid? ReversedJournalId,
    IReadOnlyList<JournalEntryLineDto> Lines)
{
    public decimal TotalDebit => Lines.Sum(l => l.DebitAmount);
    public decimal TotalCredit => Lines.Sum(l => l.CreditAmount);
}

/// <summary>Dòng Sổ Cái (theo từng TK). CounterpartAccount = TK đối ứng (phía còn lại của bút toán) — cho cột "Số hiệu TK đối ứng" trên mẫu sổ.</summary>
public sealed record LedgerRowDto(
    DateTime EntryDate,
    string JournalNo,
    string Description,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance,
    string? CounterpartAccount = null);

/// <summary>Sổ Cái của 1 TK trong kỳ.</summary>
public sealed record LedgerReportDto(
    string AccountCode,
    string? AccountName,
    decimal OpeningBalance,
    decimal PeriodDebitTotal,
    decimal PeriodCreditTotal,
    decimal ClosingBalance,
    IReadOnlyList<LedgerRowDto> Rows);

/// <summary>Số dư 1 TK: đầu kỳ · phát sinh · cuối kỳ.</summary>
public sealed record AccountBalanceDto(
    string AccountCode,
    string? AccountName,
    decimal OpeningBalance,
    decimal DebitTotal,
    decimal CreditTotal,
    decimal ClosingBalance);

/// <summary>TK có phát sinh trong kỳ (cho dropdown).</summary>
public sealed record AccountActivityDto(
    string AccountCode,
    string? AccountName,
    decimal DebitTotal,
    decimal CreditTotal);

/// <summary>Thông tin đơn vị cho phần đầu mẫu sổ (Đơn vị / Địa chỉ).</summary>
public sealed record TenantInfoDto(
    string? Name,
    string? Address);

/// <summary>Lũy kế phát sinh Nợ/Có của TK từ đầu quý đến hết kỳ.</summary>
public sealed record QuarterTotalsDto(
    decimal DebitTotal,
    decimal CreditTotal);