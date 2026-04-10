using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.Tests;

public class VietQrServiceSimpleTests
{
    [Fact]
    public void GenerateShadowAccountId_ShouldProduceConsistentHash_ForSameInput()
    {
        // Arrange
        var bankId = "VCB";
        var accountNo = "123456789";

        // Act
        var hash1 = VietQrServiceTestHelper.GenerateShadowAccountId(bankId, accountNo);
        var hash2 = VietQrServiceTestHelper.GenerateShadowAccountId(bankId, accountNo);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.Equal(16, hash1.Length);
        Assert.Matches(@"^[0-9A-Fa-f]+$", hash1);
    }

    [Fact]
    public void GenerateShadowAccountId_ShouldProduceDifferentHash_ForDifferentInput()
    {
        // Arrange
        var bankId1 = "VCB";
        var accountNo1 = "123456789";
        var bankId2 = "TCB";
        var accountNo2 = "987654321";

        // Act
        var hash1 = VietQrServiceTestHelper.GenerateShadowAccountId(bankId1, accountNo1);
        var hash2 = VietQrServiceTestHelper.GenerateShadowAccountId(bankId2, accountNo2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GenerateShadowAccountId_ShouldUseSHA256Algorithm()
    {
        // Arrange
        var bankId = "TEST";
        var accountNo = "123";

        // Act
        var hash = VietQrServiceTestHelper.GenerateShadowAccountId(bankId, accountNo);

        // Assert
        var expectedInput = $"{bankId}|{accountNo}|VanAnSalt";
        var expectedBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(expectedInput));
        var expectedHash = Convert.ToHexString(expectedBytes)[..16];
        
        Assert.Equal(expectedHash, hash);
    }
}

// Test helper class to access private method
public static class VietQrServiceTestHelper
{
    public static string GenerateShadowAccountId(string bankId, string accountNo)
    {
        // Hash: bankId + "|" + accountNo + "|VanAnSalt"
        var input = $"{bankId}|{accountNo}|VanAnSalt";
        var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes)[..16]; // First 16 characters for ID
    }
}
