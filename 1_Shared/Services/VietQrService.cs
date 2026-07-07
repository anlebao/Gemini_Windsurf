using VanAn.Shared.Domain;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace VanAn.Shared.Services
{
    /// <summary>
    /// Represents a bank supported by the VietQR/Napas BIN registry.
    /// Extracted from VietQrController to provide a single source of truth
    /// for both the controller endpoint and ValidateBankConfigAsync.
    /// </summary>
    public sealed record BankInfo(string Id, string Name, string Logo);

    public interface IVietQrService
    {
        Task<VietQrResponse> GenerateQrCodeAsync(VietQrRequest request);
        Task<bool> ValidateBankConfigAsync(BankConfig config);
    }

    public partial class VietQrService(ILogger<VietQrService> logger, HttpClient httpClient) : IVietQrService
    {
        private readonly ILogger<VietQrService> _logger = logger;
        private readonly HttpClient _httpClient = httpClient;

        /// <summary>
        /// Supported banks registered with Napas/VietQR (BIN codes per TT 152/2025/TT-BTC intent).
        /// Single source of truth shared with VietQrController.GetSupportedBanks.
        /// </summary>
        public static readonly IReadOnlyList<BankInfo> SupportedBanks =
        [
            new BankInfo("970422", "Vietcombank", "https://img.vietqr.io/bank/970422.png"),
            new BankInfo("970436", "VietinBank", "https://img.vietqr.io/bank/970436.png"),
            new BankInfo("970418", "Agribank", "https://img.vietqr.io/bank/970418.png"),
            new BankInfo("970449", "MB Bank", "https://img.vietqr.io/bank/970449.png"),
            new BankInfo("970423", "Sacombank", "https://img.vietqr.io/bank/970423.png"),
            new BankInfo("970405", "Timo Digital Bank", "https://img.vietqr.io/bank/970405.png")
        ];

        // Account number must be numeric and 6-16 digits long (per business rule).
        private static readonly Regex AccountNoPattern = new(@"^\d{6,16}$", RegexOptions.Compiled);

        public async Task<VietQrResponse> GenerateQrCodeAsync(VietQrRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                LogVietQrProcessing(request.OrderDescription, request.Amount);

                // Validate bank config
                if (!await ValidateBankConfigAsync(request.BankConfig))
                {
                    throw new ArgumentException("Invalid bank configuration");
                }

                // Build VietQR URL
                string qrUrl = BuildVietQrUrl(request);

                // Generate payment URL (same as QR URL for VietQR)
                string paymentUrl = qrUrl;

                VietQrResponse response = new()
                {
                    QrImageUrl = new Uri(qrUrl),
                    PaymentUrl = new Uri(paymentUrl),
                    Amount = request.Amount,
                    OrderId = request.OrderDescription,
                    GeneratedAt = DateTime.UtcNow
                };

                LogVietQrGenerated(request.OrderDescription);

                return response;
            }
            catch (Exception ex)
            {
                LogVietQrError(ex, request.OrderDescription);
                throw;
            }
        }

        public async Task<bool> ValidateBankConfigAsync(BankConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            try
            {
                await Task.CompletedTask;

                // Rule 1: BankId must be non-empty and in supported banks list (Napas/VietQR BIN)
                if (string.IsNullOrWhiteSpace(config.BankId))
                    return false;
                bool bankIdSupported = SupportedBanks.Any(b => b.Id == config.BankId!.Trim());
                if (!bankIdSupported)
                    return false;

                // Rule 2: AccountNo must be non-empty and numeric (6-16 digits)
                if (string.IsNullOrWhiteSpace(config.AccountNo))
                    return false;
                if (!AccountNoPattern.IsMatch(config.AccountNo!.Trim()))
                    return false;

                // Rule 3: AccountName must be non-empty (after trim)
                if (string.IsNullOrWhiteSpace(config.AccountName))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                LogBankConfigValidationError(ex);
                throw;
            }
        }

        public static string BuildVietQrUrl(VietQrRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            // VietQR URL format: https://img.vietqr.io/image/<BANK_ID>-<ACCOUNT_NO>-template.jpg?amount=<AMOUNT>&addInfo=<DESCRIPTION>
            string baseUrl = "https://img.vietqr.io/image";
            string bankAccount = $"{request.BankConfig.BankId}-{request.BankConfig.AccountNo}";
            string template = "compact"; // or "compact2", "qronly"

            string url = $"{baseUrl}/{bankAccount}-{template}.jpg";

            List<string> parameters = new();
            if (request.Amount > 0)
            {
                parameters.Add($"amount={request.Amount.ToString(CultureInfo.InvariantCulture)}");
            }

            // Add description
            if (!string.IsNullOrWhiteSpace(request.OrderDescription))
            {
                string encodedDescription = Uri.EscapeDataString(request.OrderDescription);
                parameters.Add($"addInfo={encodedDescription}");
            }

            // Add account name
            if (!string.IsNullOrWhiteSpace(request.BankConfig.AccountName))
            {
                string encodedName = Uri.EscapeDataString(request.BankConfig.AccountName);
                parameters.Add($"accountName={encodedName}");
            }

            // PERFORMANCE: Use Count > 0 instead of Any() for better performance
            if (parameters.Count > 0)
            {
                url += "?" + string.Join("&", parameters);
            }

            return url;
        }

        // Shadow ID Generation for Anonymous Identity Protection
        private static string GenerateShadowAccountId(string bankId, string accountNo)
        {
            // Hash: bankId + "|" + accountNo + "|VanAnSalt"
            string input = $"{bankId}|{accountNo}|VanAnSalt";
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hashBytes)[..16]; // First 16 characters for ID
        }

        // High-Performance Logging Methods
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Generating VietQR for order: {OrderDescription}, amount: {Amount}")]
        private partial void LogVietQrProcessing(string orderDescription, decimal amount);

        [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "VietQR generated successfully for order: {OrderDescription}")]
        private partial void LogVietQrGenerated(string orderDescription);

        [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Error generating VietQR for order: {OrderDescription}")]
        private partial void LogVietQrError(Exception ex, string orderDescription);

        [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Error validating bank config")]
        private partial void LogBankConfigValidationError(Exception ex);
    }
}
