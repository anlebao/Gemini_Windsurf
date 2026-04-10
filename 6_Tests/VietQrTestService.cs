using Microsoft.Extensions.Logging;

namespace VanAn.Test.Services;

// DTOs for VietQR service
public class VietQrRequest
{
    public string BankId { get; set; } = string.Empty;
    public string AccountNo { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string OrderId { get; set; } = string.Empty;
}

public interface IMockVietQrService
{
    Task<string> GeneratePayloadAsync(VietQrRequest request);
}

public class MockVietQrService : IMockVietQrService
{
    private readonly ILogger<MockVietQrService> _logger;

    public MockVietQrService(ILogger<MockVietQrService> logger)
    {
        _logger = logger;
    }

    public async Task<string> GeneratePayloadAsync(VietQrRequest request)
    {
        // Validate input parameters
        ValidateRequest(request);

        try
        {
            // Build VietQR payload according to EMVCo standard
            var payload = BuildVietQrPayload(request);
            
            _logger.LogInformation("Generated VietQR payload for order {OrderId}, amount {Amount}", 
                request.OrderId, request.Amount);

            return await Task.FromResult(payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate VietQR payload for order {OrderId}", request.OrderId);
            throw;
        }
    }

    private void ValidateRequest(VietQrRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.BankId))
            throw new ArgumentException("BankId is required", nameof(request.BankId));

        if (string.IsNullOrWhiteSpace(request.AccountNo))
            throw new ArgumentException("AccountNo is required", nameof(request.AccountNo));

        if (string.IsNullOrWhiteSpace(request.AccountName))
            throw new ArgumentException("AccountName is required", nameof(request.AccountName));

        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be greater than zero", nameof(request.Amount));

        if (string.IsNullOrWhiteSpace(request.OrderId))
            throw new ArgumentException("OrderId is required", nameof(request.OrderId));
    }

    private string BuildVietQrPayload(VietQrRequest request)
    {
        // EMVCo QR Code Standard Format
        // 00: Payload Format Indicator
        // 01: Point of Initiation Method
        // 26-28: Merchant Account Information
        // 52: Merchant Category Code
        // 53: Transaction Currency
        // 54: Transaction Amount
        // 58: Country Code
        // 59: Merchant Name
        // 60: Merchant City
        // 62: Additional Data (Order ID in addInfo)

        var payload = new List<string>();

        // Payload Format Indicator (01): EMVCo QR Code
        payload.Add("000201010212");

        // Merchant Account Information (26-28)
        // 26: Globally Unique Identifier
        payload.Add("0010A000000727"); // VietQR provider ID
        
        // 27: Application Identifier (Bank ID)
        var bankIdField = $"0108{request.BankId.PadRight(8, ' ')}";
        payload.Add(bankIdField);
        
        // 28: Application Definition (Account Number)
        var accountField = $"0211{request.AccountNo.PadRight(11, ' ')}";
        payload.Add(accountField);

        // Merchant Category Code (52): 4-digit MCC for retail
        payload.Add("52045812"); // 5812 = Restaurants/Bars

        // Transaction Currency (53): 704 = Vietnamese Dong
        payload.Add("5303704");

        // Transaction Amount (54): Amount without decimal places
        var amountField = $"5408{((long)request.Amount).ToString("D8")}";
        payload.Add(amountField);

        // Country Code (58): 704 = Vietnam
        payload.Add("5802VN");

        // Merchant Name (59): Account name
        var merchantNameField = $"59{(request.AccountName.Length + 2):D2}{request.AccountName}";
        payload.Add(merchantNameField);

        // Merchant City (60): City (using HCMC as default)
        payload.Add("6007HO CHI MINH");

        // Additional Data (62): Order ID in addInfo
        var addInfoField = $"6208{request.OrderId.PadRight(8, ' ')}";
        payload.Add(addInfoField);

        // CRC (63): Checksum (simplified - would need proper CRC16 implementation)
        payload.Add("6304");

        // Combine all fields
        var fullPayload = string.Join("", payload);
        
        // Add placeholder CRC (in production, calculate actual CRC16)
        var payloadWithCrc = fullPayload + "ABCD";

        return payloadWithCrc;
    }
}
