namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Regime-lock (2026-10-30, user-approved): chuẩn kế toán được phép áp dụng theo Tenant.Type.
    /// Pure static — dùng chung bởi ShopERP (dropdown UI) + CoreHub (validate write-path
    /// SetAccountingStandardAsync). Default-lock theo loại hình + bầu chọn hợp pháp:
    ///   HKD → TT 152 (sổ riêng, KHÔNG BCTC) hoặc tự nguyện TT 58 (Điều 2.2 TT 58/2026)
    ///   DN siêu nhỏ → TT 58 mặc định, được chọn TT 133 (áp dụng chế độ cao hơn)
    ///   DN vừa/nhỏ → TT 133 · DN lớn → TT 99 · HTX → TT 71 (khóa cứng)
    ///   Type=null (legacy, chưa phân loại) → không khóa (đủ 4 — giữ hành vi cũ)
    /// </summary>
    public static class AccountingStandardRules
    {
        /// <summary>Chuẩn mặc định theo Tenant.Type (null = không suy ra được — hiển thị TT133 như cũ).</summary>
        public static AccountingStandard DefaultStandard(TenantType? tenantType) => tenantType switch
        {
            TenantType.Enterprise_Large => AccountingStandard.TT99_2025,
            TenantType.Enterprise_SuperSmall => AccountingStandard.TT58_2026,
            TenantType.HTX => AccountingStandard.TT71_2024,
            _ => AccountingStandard.TT133_2016
        };

        /// <summary>
        /// Danh sách chuẩn được phép chọn cho tenant. Trống = BCTC doanh nghiệp không áp dụng
        /// (HKD chưa bầu chọn TT 58 → dùng sổ TT 152).
        /// Override hợp lệ = phải nằm trong danh sách này.
        /// </summary>
        public static IReadOnlyList<AccountingStandard> AllowedStandards(
            TenantType? tenantType, AccountingStandard? accountingStandardOverride) => tenantType switch
        {
            TenantType.Enterprise_Large => new[] { AccountingStandard.TT99_2025 },
            TenantType.Enterprise_SME => new[] { AccountingStandard.TT133_2016 },
            TenantType.HTX => new[] { AccountingStandard.TT71_2024 },
            TenantType.Enterprise_SuperSmall => new[] { AccountingStandard.TT58_2026, AccountingStandard.TT133_2016 },
            TenantType.HKD => accountingStandardOverride == AccountingStandard.TT58_2026
                ? new[] { AccountingStandard.TT58_2026 }
                : Array.Empty<AccountingStandard>(),
            _ => new[]
            {
                AccountingStandard.TT133_2016, AccountingStandard.TT99_2025,
                AccountingStandard.TT58_2026, AccountingStandard.TT71_2024
            }
        };

        /// <summary>
        /// Chuẩn hiệu lực = override (nếu thuộc allowed) → default (nếu thuộc allowed) →
        /// phần tử đầu allowed → null khi allowed rỗng (BCTC không áp dụng).
        /// </summary>
        public static AccountingStandard? ResolveEffective(TenantType? tenantType, AccountingStandard? accountingStandardOverride)
        {
            var allowed = AllowedStandards(tenantType, accountingStandardOverride);
            if (allowed.Count == 0) return null;
            if (accountingStandardOverride is { } ovr && allowed.Contains(ovr)) return ovr;
            var def = DefaultStandard(tenantType);
            return allowed.Contains(def) ? def : allowed[0];
        }

        /// <summary>
        /// Chuẩn có thể BẦU CHỌN (override) cho loại hình — dùng cho admin modal
        /// "Chế độ kế toán". Khác AllowedStandards: HKD chưa elected vẫn thấy TT 58.
        /// Rỗng = loại hình khóa cứng, chỉ dùng mặc định.
        /// </summary>
        public static IReadOnlyList<AccountingStandard> ElectableStandards(TenantType? tenantType) => tenantType switch
        {
            TenantType.HKD => new[] { AccountingStandard.TT58_2026 },                 // Điều 2.2 TT 58/2026
            TenantType.Enterprise_SuperSmall => new[] { AccountingStandard.TT133_2016 }, // tự nguyện lên TT 133
            null => new[] { AccountingStandard.TT99_2025, AccountingStandard.TT58_2026, AccountingStandard.TT71_2024 },
            _ => Array.Empty<AccountingStandard>()
        };

        /// <summary>Chuẩn đang dùng khác default của loại hình → badge "áp dụng tự nguyện".</summary>
        public static bool IsVoluntary(TenantType? tenantType, AccountingStandard standard)
            => standard != DefaultStandard(tenantType);
    }
}
