using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Services.GeneralLedger;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// W4 (P1.3 — TT 152/2025): cảnh báo âm quỹ TK 111 / âm kho TK 152·156 / âm tồn
    /// nguyên liệu. NON-BLOCKING: chỉ trả <see cref="BalanceAlertResult"/>, caller quyết
    /// định hiển thị — nghiệp vụ vẫn lưu được (ghi nhận rủi ro, không ngăn phiếu).
    ///
    /// Feature flag: <c>Balance_Alert_Enabled</c> — default ON; fail-safe ON khi flag
    /// service lỗi trong background scope (giống IsAuditEnabledAsync).
    /// </summary>
    public class BalanceAlertService(
        IGeneralLedgerService generalLedger,
        IFeatureFlagService featureFlag,
        ILogger<BalanceAlertService> logger) : IBalanceAlertService
    {
        private const string CashAccountCode = "111";
        private static readonly string[] InventoryAccountCodes = ["152", "156"];

        public async Task<BalanceAlertResult?> CheckCashBalanceAsync(
            TenantId tenantId,
            decimal projectedOutflow,
            CancellationToken cancellationToken = default)
        {
            if (projectedOutflow <= 0 || !await IsEnabledAsync(cancellationToken))
            {
                return null;
            }

            DateTime now = DateTime.UtcNow;
            IReadOnlyList<AccountBalanceDto> balances = await generalLedger.GetBalancesAsync(
                tenantId, now.Year, now.Month, cancellationToken);

            decimal current = balances
                .Where(b => b.AccountCode == CashAccountCode)
                .Sum(b => b.ClosingBalance);

            decimal projected = current - projectedOutflow;
            if (projected >= 0)
            {
                return null;
            }

            logger.LogWarning(
                "BALANCE ALERT: TK 111 âm quỹ — tenant {TenantId}, hiện tại {Current:N0}, chi {Outflow:N0}, dư kiến {Projected:N0}",
                tenantId.Value, current, projectedOutflow, projected);

            return new BalanceAlertResult(
                BalanceAlertType.CashNegative,
                "TK 111 — Tiền mặt",
                current,
                projectedOutflow,
                projected,
                IsNegative: true,
                $"⚠ Âm quỹ tiền mặt: TK 111 hiện có {current:N0}đ, khoản chi {projectedOutflow:N0}đ sẽ làm quỹ âm {projected:N0}đ. Kiểm tra lại trước khi lưu.");
        }

        public async Task<BalanceAlertResult?> CheckInventoryBalanceAsync(
            TenantId tenantId,
            decimal projectedOutflowValue,
            CancellationToken cancellationToken = default)
        {
            if (projectedOutflowValue <= 0 || !await IsEnabledAsync(cancellationToken))
            {
                return null;
            }

            DateTime now = DateTime.UtcNow;
            IReadOnlyList<AccountBalanceDto> balances = await generalLedger.GetBalancesAsync(
                tenantId, now.Year, now.Month, cancellationToken);

            decimal current = balances
                .Where(b => InventoryAccountCodes.Contains(b.AccountCode))
                .Sum(b => b.ClosingBalance);

            decimal projected = current - projectedOutflowValue;
            if (projected >= 0)
            {
                return null;
            }

            logger.LogWarning(
                "BALANCE ALERT: TK 152/156 âm kho — tenant {TenantId}, hiện tại {Current:N0}, xuất {Outflow:N0}, dư kiến {Projected:N0}",
                tenantId.Value, current, projectedOutflowValue, projected);

            return new BalanceAlertResult(
                BalanceAlertType.InventoryNegative,
                "TK 152/156 — Hàng tồn kho",
                current,
                projectedOutflowValue,
                projected,
                IsNegative: true,
                $"⚠ Âm kho trên sổ sách: TK 152/156 hiện có {current:N0}đ, xuất {projectedOutflowValue:N0}đ sẽ làm tồn kho âm {projected:N0}đ.");
        }

        public async Task<BalanceAlertResult?> CheckStockLevelAsync(
            string itemLabel,
            decimal currentStock,
            decimal projectedOutput,
            CancellationToken cancellationToken = default)
        {
            if (projectedOutput <= 0 || !await IsEnabledAsync(cancellationToken))
            {
                return null;
            }

            decimal projected = currentStock - projectedOutput;
            if (projected >= 0)
            {
                return null;
            }

            logger.LogWarning(
                "BALANCE ALERT: âm tồn nguyên liệu '{Item}' — hiện có {Current}, xuất {Outflow}, dư kiến {Projected}",
                itemLabel, currentStock, projectedOutput, projected);

            return new BalanceAlertResult(
                BalanceAlertType.IngredientStockNegative,
                itemLabel,
                currentStock,
                projectedOutput,
                projected,
                IsNegative: true,
                $"⚠ Âm tồn kho '{itemLabel}': hiện có {currentStock}, xuất {projectedOutput} → tồn âm {projected}.");
        }

        /// <summary>
        /// Balance_Alert_Enabled — default ON; fail-safe ON khi flag service throw
        /// trong background scope (NATS/hosted — không HttpContext).
        /// </summary>
        private async Task<bool> IsEnabledAsync(CancellationToken ct)
        {
            try
            {
                return await featureFlag.IsEnabledAsync("Balance_Alert_Enabled", defaultWhenMissing: true, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex,
                    "Balance_Alert_Enabled flag check failed (background scope?) — alerts default to ENABLED");
                return true;
            }
        }
    }
}
