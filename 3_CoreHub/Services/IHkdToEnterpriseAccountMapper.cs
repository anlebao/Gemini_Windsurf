using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// W3 (D9): Maps HKD internal synthetic account keys (single-entry, TT 88/2021)
/// to DN double-entry account codes per accounting standard.
/// Used by W8 conversion service for opening-balance migration (HKD → Enterprise tenant).
///
/// Mapping is "best-effort" — HKD single-entry has no formal double-entry structure.
/// This mapper provides account-code translation only; balance translation requires
/// manual review at W8 (per D9 decision).
/// </summary>
public interface IHkdToEnterpriseAccountMapper
{
    /// <summary>Map an HKD internal account key (e.g., "Revenue") to a DN account code (e.g., "511").</summary>
    string MapToEnterpriseAccount(string hkdAccountKey, AccountingStandard standard);

    /// <summary>Get all HKD→DN mappings for a standard.</summary>
    IReadOnlyDictionary<string, string> GetMappings(AccountingStandard standard);
}
