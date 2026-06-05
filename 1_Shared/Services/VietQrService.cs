using VanAn.Shared.Domain;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace VanAn.Shared.Services
{
    public interface IVietQrService
    {
        Task<VietQrResponse> GenerateQrCodeAsync(VietQrRequest request);
        Task<bool> ValidateBankConfigAsync(BankConfig config);
    }

    public partial class VietQrService(ILogger<VietQrService> logger, HttpClient httpClient) : IVietQrService
    {
        private readonly ILogger<VietQrService> _logger = logger;
        private readonly HttpClient _httpClient = httpClient;

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
                // Basic validation
                if (string.IsNullOrWhiteSpace(config.BankId) ||
                    string.IsNullOrWhiteSpace(config.AccountNo) ||
                    string.IsNullOrWhiteSpace(config.AccountName))
                {
                    return false;
                }

                // Additional validation logic would go here
                // For now, just return true if basic info is provided
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
