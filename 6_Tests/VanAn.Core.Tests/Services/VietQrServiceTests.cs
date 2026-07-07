using Microsoft.Extensions.Logging.Abstractions;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// W6 Phase 2 / Bucket E: Unit tests for VietQrService.ValidateBankConfigAsync.
/// Implements the business rule from TT 152/2025/TT-BTC intent:
///   1. BankId must be in the supported banks list (Napas/VietQR BIN codes).
///   2. AccountNo must be non-empty and match ^\d{6,16}$ (numeric, 6-16 digits).
///   3. AccountName must be non-empty after trim.
/// These tests back the golden E2E test TC_QR_Validation (qr-payment.spec.ts:66).
/// </summary>
public class VietQrServiceTests
{
    private static VietQrService CreateService() =>
        new(NullLogger<VietQrService>.Instance, new HttpClient());

    private static BankConfig MakeConfig(string bankId, string accountNo, string accountName) =>
        new() { BankId = bankId, AccountNo = accountNo, AccountName = accountName };

    [Fact]
    public async Task ValidateBankConfigAsync_ValidBank_ReturnsTrue()
    {
        VietQrService service = CreateService();
        BankConfig config = MakeConfig("970422", "1234567890", "VALID BANK");

        bool result = await service.ValidateBankConfigAsync(config);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateBankConfigAsync_UnknownBin_ReturnsFalse()
    {
        // This is the core assertion the E2E test TC_QR_Validation checks.
        VietQrService service = CreateService();
        BankConfig config = MakeConfig("999999", "123", "INVALID BANK");

        bool result = await service.ValidateBankConfigAsync(config);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateBankConfigAsync_EmptyBankId_ReturnsFalse()
    {
        VietQrService service = CreateService();
        BankConfig config = MakeConfig("", "1234567890", "VALID BANK");

        bool result = await service.ValidateBankConfigAsync(config);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateBankConfigAsync_WhitespaceBankId_ReturnsFalse()
    {
        VietQrService service = CreateService();
        BankConfig config = MakeConfig("   ", "1234567890", "VALID BANK");

        bool result = await service.ValidateBankConfigAsync(config);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateBankConfigAsync_EmptyAccountNo_ReturnsFalse()
    {
        VietQrService service = CreateService();
        BankConfig config = MakeConfig("970422", "", "VALID BANK");

        bool result = await service.ValidateBankConfigAsync(config);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateBankConfigAsync_NonNumericAccountNo_ReturnsFalse()
    {
        VietQrService service = CreateService();
        BankConfig config = MakeConfig("970422", "abc123", "VALID BANK");

        bool result = await service.ValidateBankConfigAsync(config);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateBankConfigAsync_AccountNoTooShort_ReturnsFalse()
    {
        VietQrService service = CreateService();
        BankConfig config = MakeConfig("970422", "12345", "VALID BANK"); // 5 digits

        bool result = await service.ValidateBankConfigAsync(config);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateBankConfigAsync_AccountNoTooLong_ReturnsFalse()
    {
        VietQrService service = CreateService();
        BankConfig config = MakeConfig("970422", "12345678901234567", "VALID BANK"); // 17 digits

        bool result = await service.ValidateBankConfigAsync(config);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateBankConfigAsync_EmptyAccountName_ReturnsFalse()
    {
        VietQrService service = CreateService();
        BankConfig config = MakeConfig("970422", "1234567890", "");

        bool result = await service.ValidateBankConfigAsync(config);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateBankConfigAsync_WhitespaceAccountName_ReturnsFalse()
    {
        VietQrService service = CreateService();
        BankConfig config = MakeConfig("970422", "1234567890", "   ");

        bool result = await service.ValidateBankConfigAsync(config);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateBankConfigAsync_NullConfig_ThrowsArgumentNullException()
    {
        VietQrService service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.ValidateBankConfigAsync(null!));
    }

    [Fact]
    public async Task ValidateBankConfigAsync_BankIdWithWhitespace_StillValidAfterTrim()
    {
        // Verifies trimming behavior: " 970422 " should match "970422".
        VietQrService service = CreateService();
        BankConfig config = MakeConfig("  970422  ", "1234567890", "VALID BANK");

        bool result = await service.ValidateBankConfigAsync(config);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateBankConfigAsync_AllSupportedBanks_AreValid()
    {
        VietQrService service = CreateService();

        foreach (BankInfo bank in VietQrService.SupportedBanks)
        {
            BankConfig config = MakeConfig(bank.Id, "1234567890", "TEST ACCOUNT");
            bool result = await service.ValidateBankConfigAsync(config);
            Assert.True(result, $"Bank {bank.Id} ({bank.Name}) should be valid");
        }
    }
}
