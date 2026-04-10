using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;
using VanAn.Test.Services;

namespace VanAn.Tests;

public class VietQrPayloadTests
{
    private IMockVietQrService _vietQrService;

    public VietQrPayloadTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddScoped<IMockVietQrService, MockVietQrService>();
        
        var serviceProvider = services.BuildServiceProvider();
        _vietQrService = serviceProvider.GetRequiredService<IMockVietQrService>();
    }

    [Fact]
    public async Task GeneratePayload_StandardParameters_ShouldMatchVietQRCompactFormat()
    {
        // Arrange
        var bankAccountId = "970418"; // Vietcombank
        var accountNo = "1234567890";
        var accountName = "VAN AN GROUP";
        var amount = 75000m;
        var orderId = "ORD-20260402-001";

        // Act
        var payload = await _vietQrService.GeneratePayloadAsync(new VanAn.Test.Services.VietQrRequest
        {
            BankId = bankAccountId,
            AccountNo = accountNo,
            AccountName = accountName,
            Amount = amount,
            OrderId = orderId
        });

        // Assert
        // Expected VietQR compact format: 000201010212...
        Assert.NotNull(payload);
        Assert.StartsWith("000201010212", payload);
        Assert.Contains(accountNo, payload);
        Assert.Contains(amount.ToString(), payload);
        Assert.Contains(orderId, payload);
    }

    [Fact]
    public async Task GeneratePayload_ZeroAmount_ShouldThrowException()
    {
        // Arrange
        var request = new VanAn.Test.Services.VietQrRequest
        {
            BankId = "970418",
            AccountNo = "1234567890",
            AccountName = "VAN AN GROUP",
            Amount = 0m, // Zero amount
            OrderId = "ORD-20260402-001"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _vietQrService.GeneratePayloadAsync(request);
        });

        Assert.Contains("Amount must be greater than zero", exception.Message);
    }

    [Fact]
    public async Task GeneratePayload_NegativeAmount_ShouldThrowException()
    {
        // Arrange
        var request = new VanAn.Test.Services.VietQrRequest
        {
            BankId = "970418",
            AccountNo = "1234567890",
            AccountName = "VAN AN GROUP",
            Amount = -1000m, // Negative amount
            OrderId = "ORD-20260402-001"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _vietQrService.GeneratePayloadAsync(request);
        });

        Assert.Contains("Amount must be greater than zero", exception.Message);
    }

    [Fact]
    public async Task GeneratePayload_EmptyBankId_ShouldThrowException()
    {
        // Arrange
        var request = new VanAn.Test.Services.VietQrRequest
        {
            BankId = "", // Empty bank ID
            AccountNo = "1234567890",
            AccountName = "VAN AN GROUP",
            Amount = 75000m,
            OrderId = "ORD-20260402-001"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _vietQrService.GeneratePayloadAsync(request);
        });

        Assert.Contains("BankId is required", exception.Message);
    }

    [Fact]
    public async Task GeneratePayload_EmptyAccountNo_ShouldThrowException()
    {
        // Arrange
        var request = new VanAn.Test.Services.VietQrRequest
        {
            BankId = "970418",
            AccountNo = "", // Empty account number
            AccountName = "VAN AN GROUP",
            Amount = 75000m,
            OrderId = "ORD-20260402-001"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _vietQrService.GeneratePayloadAsync(request);
        });

        Assert.Contains("AccountNo is required", exception.Message);
    }

    [Fact]
    public async Task GeneratePayload_EmptyOrderId_ShouldThrowException()
    {
        // Arrange
        var request = new VanAn.Test.Services.VietQrRequest
        {
            BankId = "970418",
            AccountNo = "1234567890",
            AccountName = "VAN AN GROUP",
            Amount = 75000m,
            OrderId = "" // Empty order ID
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _vietQrService.GeneratePayloadAsync(request);
        });

        Assert.Contains("OrderId is required", exception.Message);
    }
}

