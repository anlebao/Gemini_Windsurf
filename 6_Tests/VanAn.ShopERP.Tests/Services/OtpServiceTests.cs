using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using VanAn.ShopERP.Services;

namespace VanAn.ShopERP.Tests.Services;

/// <summary>
/// W0 hardening (2026-11-10, REVIEW B1 — user duyệt):
/// OtpService CSPRNG + attempt lock + purpose binding.
/// </summary>
public class OtpServiceTests
{
    private static (OtpService Service, MemoryCache Cache) Create(int maxAttempts = 5, int lockoutMinutes = 5, int ttlMinutes = 5)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Otp:MaxAttempts"] = maxAttempts.ToString(),
                ["Otp:LockoutMinutes"] = lockoutMinutes.ToString(),
                ["Otp:TtlMinutes"] = ttlMinutes.ToString(),
            })
            .Build();
        var cache = new MemoryCache(new MemoryCacheOptions());
        return (new OtpService(cache, config), cache);
    }

    [Fact]
    public void Generate_ReturnsSixDigitNumericCode()
    {
        var (svc, _) = Create();
        for (var i = 0; i < 50; i++)
        {
            var otp = svc.GenerateAndStoreOtp("0901234567");
            otp.Length.Should().Be(6);
            otp.All(char.IsDigit).Should().BeTrue();
            otp.Should().NotBe("000000");
        }
    }

    [Fact]
    public void Generate_ProducesUniqueCodesAcrossBatch()
    {
        var (svc, _) = Create();
        var codes = Enumerable.Range(0, 200).Select(_ => svc.GenerateAndStoreOtp("0901234567")).ToHashSet();
        codes.Count.Should().BeGreaterThan(190); // CSPRNG — collisions nearly impossible
    }

    [Fact]
    public void Verify_CorrectCode_ReturnsTrue_AndCodeIsSingleUse()
    {
        var (svc, _) = Create();
        var otp = svc.GenerateAndStoreOtp("0901234567");
        svc.VerifyOtp("0901234567", otp).Should().BeTrue();
        svc.VerifyOtp("0901234567", otp).Should().BeFalse(); // replay
    }

    [Fact]
    public void Verify_WrongCode_ReturnsFalse()
    {
        var (svc, _) = Create();
        svc.GenerateAndStoreOtp("0901234567");
        svc.VerifyOtp("0901234567", "000000").Should().BeFalse();
    }

    [Fact]
    public void Verify_WrongCodeMaxAttempts_LocksEvenWithCorrectCode()
    {
        var (svc, _) = Create(maxAttempts: 3);
        var otp = svc.GenerateAndStoreOtp("0901234567");
        svc.VerifyOtp("0901234567", "111111").Should().BeFalse();
        svc.VerifyOtp("0901234567", "222222").Should().BeFalse();
        svc.VerifyOtp("0901234567", "333333").Should().BeFalse();
        // locked: correct code rejected
        svc.VerifyOtp("0901234567", otp).Should().BeFalse();
    }

    [Fact]
    public void Generate_ResetsAttemptCounter_AfterPreviousFailures()
    {
        var (svc, _) = Create(maxAttempts: 3);
        svc.GenerateAndStoreOtp("0901234567");
        svc.VerifyOtp("0901234567", "111111").Should().BeFalse();
        svc.VerifyOtp("0901234567", "222222").Should().BeFalse();
        // regenerate resets counter → wrong again should NOT lock yet
        var otp = svc.GenerateAndStoreOtp("0901234567");
        svc.VerifyOtp("0901234567", "333333").Should().BeFalse();
        svc.VerifyOtp("0901234567", otp).Should().BeTrue();
    }

    [Fact]
    public void Verify_PurposeSeparated_OtpCannotBeUsedAcrossPurposes()
    {
        var (svc, _) = Create();
        var otp = svc.GenerateAndStoreOtp("0901234567", "customer-login");
        svc.VerifyOtp("0901234567", otp, "customer-upgrade").Should().BeFalse();
        svc.VerifyOtp("0901234567", otp, "customer-login").Should().BeTrue();
    }

    [Fact]
    public void Verify_DefaultPurpose_BackwardCompat()
    {
        var (svc, _) = Create();
        var otp = svc.GenerateAndStoreOtp("0901234567"); // default purpose
        svc.VerifyOtp("0901234567", otp).Should().BeTrue();
    }

    [Fact]
    public void Verify_ExpiredOrMissingOtp_CountsAsAttempt()
    {
        var (svc, _) = Create(maxAttempts: 2);
        // no OTP generated — two wrong verifies trigger lock
        svc.VerifyOtp("0901234567", "111111").Should().BeFalse();
        svc.VerifyOtp("0901234567", "222222").Should().BeFalse();
        svc.VerifyOtp("0901234567", "333333").Should().BeFalse(); // locked
    }
}