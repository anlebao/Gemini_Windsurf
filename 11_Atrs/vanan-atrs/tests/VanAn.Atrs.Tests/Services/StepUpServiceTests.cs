using VanAn.Atrs.Infra;
using Xunit;

namespace VanAn.Atrs.Tests.Services;

/// <summary>
/// Step-up primitive (FR-010, ATRS-3): token ngắn hạn, đúng subject, đúng amr, hết hạn reject,
/// subject khác reject. Dùng Jwt:Secret (same signing key ATRS tự verify).
/// </summary>
public sealed class StepUpServiceTests
{
    private const string Secret = "test-secret-0123456789-0123456789-0123456789";

    private static StepUpService CreateService(string? secret = Secret, int ttlMinutes = 10)
        => new(new TestConfiguration(secret, ttlMinutes));

    private sealed class TestConfiguration(string? secret, int ttlMinutes) : Microsoft.Extensions.Configuration.IConfiguration
    {
        private readonly Dictionary<string, string> _values = new()
        {
            ["Jwt:Secret"] = secret ?? "",
            ["Jwt:Issuer"] = "VanAn",
            ["StepUp:TtlMinutes"] = ttlMinutes.ToString()
        };

        public string? this[string key]
        {
            get => _values.TryGetValue(key, out var v) ? v : null;
            set => _values[key] = value!;
        }

        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotImplementedException();
        public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) => throw new NotImplementedException();
        public IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() => [];
    }

    [Fact]
    public void IssueThenValidate_ValidSubject_ReturnsAmr()
    {
        var service = CreateService();
        var subject = Guid.NewGuid();

        var token = service.Issue(subject, "passkey");
        var amr = service.Validate(token, subject);

        Assert.Equal("passkey", amr);
    }

    [Fact]
    public void Validate_WrongSubject_ReturnsNull()
    {
        var service = CreateService();
        var token = service.Issue(Guid.NewGuid(), "passkey");

        Assert.Null(service.Validate(token, Guid.NewGuid()));
    }

    [Fact]
    public void Validate_TamperedToken_ReturnsNull()
    {
        var service = CreateService();
        var subject = Guid.NewGuid();
        var token = service.Issue(subject, "passkey");

        Assert.Null(service.Validate(token + "x", subject));
    }

    [Fact]
    public void Validate_NullOrEmpty_ReturnsNull()
    {
        var service = CreateService();
        Assert.Null(service.Validate(null, Guid.NewGuid()));
        Assert.Null(service.Validate("", Guid.NewGuid()));
    }

    [Fact]
    public void Validate_ExpiredToken_ReturnsNull()
    {
        var service = CreateService();
        var subject = Guid.NewGuid();
        var token = service.Issue(subject, "recovery-code");

        // Mô phỏng clock sau expiry (10') + skew → token đã hết hạn
        Assert.Null(service.Validate(token, subject, DateTime.UtcNow.AddMinutes(11)));
    }

    [Fact]
    public void Validate_SimulatedClockStillValid_ReturnsAmr()
    {
        var service = CreateService();
        var subject = Guid.NewGuid();
        var token = service.Issue(subject, "passkey");

        Assert.Equal("passkey", service.Validate(token, subject, DateTime.UtcNow.AddMinutes(5)));
    }

    [Fact]
    public void Validate_DifferentSecret_ReturnsNull()
    {
        var issuer = CreateService(Secret);
        var verifier = CreateService(Secret + "-different");
        var subject = Guid.NewGuid();
        var token = issuer.Issue(subject, "passkey");

        Assert.Null(verifier.Validate(token, subject));
    }

    [Fact]
    public void Issue_WithoutSecret_Throws()
    {
        var service = CreateService(secret: null);
        Assert.Throws<InvalidOperationException>(() => service.Issue(Guid.NewGuid(), "passkey"));
    }
}