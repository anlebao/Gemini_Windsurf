using FluentAssertions;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Wave 0 — W0-T6: Unit tests for BCrypt password hash/verify.
/// Covers: correct password verify, wrong password reject, hash format validation.
/// Work factor 4 used in tests for speed (production uses work factor 12).
/// </summary>
public class LoginPasswordTests
{
    private const string CorrectPassword = "VanAn@2026";
    private const string WrongPassword = "WrongPassword123";
    private const int TestWorkFactor = 4; // fast for unit tests; production uses 12

    [Fact]
    public void BCryptVerify_CorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        var hash = BCrypt.Net.BCrypt.HashPassword(CorrectPassword, TestWorkFactor);

        // Act
        var result = BCrypt.Net.BCrypt.Verify(CorrectPassword, hash);

        // Assert
        result.Should().BeTrue("because the correct password must match the hash");
    }

    [Fact]
    public void BCryptVerify_WrongPassword_ShouldReturnFalse()
    {
        // Arrange
        var hash = BCrypt.Net.BCrypt.HashPassword(CorrectPassword, TestWorkFactor);

        // Act
        var result = BCrypt.Net.BCrypt.Verify(WrongPassword, hash);

        // Assert
        result.Should().BeFalse("because an incorrect password must not match the hash");
    }

    [Fact]
    public void BCryptHash_ShouldProduceValidHashFormat()
    {
        // Act
        var hash = BCrypt.Net.BCrypt.HashPassword(CorrectPassword, TestWorkFactor);

        // Assert: BCrypt hash with work factor 4 starts with $2a$04$
        hash.Should().NotBeNullOrEmpty();
        hash.Should().StartWith("$2a$04$",
            "because BCrypt.Net-Next produces $2a$ version hashes with the correct work factor prefix");
        hash.Should().HaveLength(60, "because a BCrypt hash is always exactly 60 characters");
    }
}
