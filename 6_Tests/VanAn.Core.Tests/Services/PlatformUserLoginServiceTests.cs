using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain.Common;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Platform SystemAdmin — Unit tests for PlatformUserLoginService.
/// Covers: correct password login, wrong password, inactive user, user not found.
/// </summary>
public class PlatformUserLoginServiceTests
{
    private readonly Mock<IVanAnDbContext> _mockDb;
    private readonly Mock<IJwtTokenService> _mockJwtService;
    private readonly PlatformUserLoginService _service;

    public PlatformUserLoginServiceTests()
    {
        _mockDb = new Mock<IVanAnDbContext>();
        _mockJwtService = new Mock<IJwtTokenService>();
        _service = new PlatformUserLoginService(_mockDb.Object, _mockJwtService.Object);
    }

    [Fact]
    public async Task LoginAsync_CorrectPassword_ShouldReturnSuccessResult()
    {
        // Arrange
        var password = "VanAn@2026";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, 12);
        var user = new PlatformUser("sysadmin@vanan.vn", passwordHash, "System Admin", "sysadmin@vanan.vn");

        var mockSet = new Mock<DbSet<PlatformUser>>();
        _mockDb.Setup(d => d.PlatformUsers).Returns(mockSet.Object);
        _mockDb.Setup(d => d.PlatformUsers.IgnoreQueryFilters()).Returns(mockSet.Object);
        mockSet.Setup(s => s.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PlatformUser, bool>>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(user);

        _mockJwtService.Setup(j => j.GenerateToken(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<IEnumerable<System.Security.Claims.Claim>>()))
               .Returns("test-jwt-token");

        // Act
        var result = await _service.LoginAsync("sysadmin@vanan.vn", password);

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().Be("test-jwt-token");
        result.Role.Should().Be("SystemAdmin");
        result.Email.Should().Be("sysadmin@vanan.vn");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ShouldReturnNull()
    {
        // Arrange
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("VanAn@2026", 12);
        var user = new PlatformUser("sysadmin@vanan.vn", passwordHash, "System Admin", "sysadmin@vanan.vn");

        var mockSet = new Mock<DbSet<PlatformUser>>();
        _mockDb.Setup(d => d.PlatformUsers).Returns(mockSet.Object);
        _mockDb.Setup(d => d.PlatformUsers.IgnoreQueryFilters()).Returns(mockSet.Object);
        mockSet.Setup(s => s.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PlatformUser, bool>>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(user);

        // Act
        var result = await _service.LoginAsync("sysadmin@vanan.vn", "WrongPassword");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_ShouldReturnNull()
    {
        // Arrange
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("VanAn@2026", 12);
        var user = new PlatformUser("sysadmin@vanan.vn", passwordHash, "System Admin", "sysadmin@vanan.vn");
        // Use reflection to set IsActive to false (no public setter)
        var isActiveProperty = typeof(PlatformUser).GetProperty("IsActive");
        if (isActiveProperty != null)
        {
            isActiveProperty.SetValue(user, false);
        }

        var mockSet = new Mock<DbSet<PlatformUser>>();
        _mockDb.Setup(d => d.PlatformUsers).Returns(mockSet.Object);
        _mockDb.Setup(d => d.PlatformUsers.IgnoreQueryFilters()).Returns(mockSet.Object);
        mockSet.Setup(s => s.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PlatformUser, bool>>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(user);

        // Act
        var result = await _service.LoginAsync("sysadmin@vanan.vn", "VanAn@2026");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ShouldReturnNull()
    {
        // Arrange
        var mockSet = new Mock<DbSet<PlatformUser>>();
        _mockDb.Setup(d => d.PlatformUsers).Returns(mockSet.Object);
        _mockDb.Setup(d => d.PlatformUsers.IgnoreQueryFilters()).Returns(mockSet.Object);
        mockSet.Setup(s => s.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PlatformUser, bool>>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((PlatformUser?)null);

        // Act
        var result = await _service.LoginAsync("nonexistent@vanan.vn", "VanAn@2026");

        // Assert
        result.Should().BeNull();
    }
}
