namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Validator for Vietnamese Chart of Accounts (Hệ thống tài khoản kế toán Việt Nam)
    /// </summary>
    public static class AccountCodeValidator
    {
        /// <summary>
        /// Validate account code against VN Chart of Accounts
        /// </summary>
        public static bool IsValidVNAccountCode(string accountCode)
        {
            if (string.IsNullOrWhiteSpace(accountCode))
            {
                return false;
            }

            if (!int.TryParse(accountCode, out int code))
            {
                return false;
            }

            if (accountCode.Length != 3)
            {
                return false;
            }
            // Valid VN account code prefixes: 1xx,2xx,3xx,4xx,5xx,6xx,7xx,8xx (not 9xx)
            int prefix = code / 100;
            return prefix is >= 1 and <= 8;
        }

        /// <summary>
        /// Get account type from account code prefix
        /// </summary>
        public static AccountType GetAccountType(string accountCode)
        {
            if (string.IsNullOrWhiteSpace(accountCode) || !int.TryParse(accountCode, out int code))
            {
                throw new ArgumentException("Invalid account code", nameof(accountCode));
            }

            int prefix = code / 100;
            return prefix switch
            {
                1 => AccountType.Asset,
                2 => AccountType.Asset,
                3 => AccountType.Liability,
                4 => AccountType.Equity,
                5 => AccountType.Revenue,
                6 => AccountType.Expense,
                7 => AccountType.OtherIncome,
                8 => AccountType.OtherExpense,
                _ => throw new ArgumentException($"Unknown account type for code: {accountCode}")
            };
        }
    }

    /// <summary>
    /// Account type classification based on VN accounting standards
    /// </summary>
    public enum AccountType
    {
        Asset,       // 1xx - Tài sản
        Liability,   // 3xx - Nợ phải trả
        Equity,      // 4xx - Vốn chủ sở hữu
        Revenue,     // 5xx - Doanh thu
        Expense,     // 6xx - Chi phí
        OtherIncome, // 7xx - Thu nhập khác
        OtherExpense // 8xx - Chi phí khác
    }
}
