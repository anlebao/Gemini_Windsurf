namespace VanAn.CoreHub.Common
{
    /// <summary>
    /// Payment method constants + cash/bank account mapping (VAS Wave 0 / R9).
    /// Maps Order.PaymentMethod → Vietnamese chart of accounts cash account.
    /// 111 = Tiền mặt (Cash); 112 = Tiền gửi ngân hàng (Bank deposit).
    /// </summary>
    public static class PaymentMethodConstants
    {
        public const string Cash = "CASH";
        public const string VietQR = "VIETQR";
        public const string CreditCard = "CREDIT_CARD";

        /// <summary>
        /// Map PaymentMethod → cash account code (111 cash, 112 bank).
        /// Unknown/null falls back to 111 (cash) — safe default for HKD retail.
        /// </summary>
        public static string MapCashAccount(string? paymentMethod) => paymentMethod switch
        {
            Cash => "111",
            VietQR or CreditCard => "112",
            _ => "111" // safe fallback
        };
    }
}
