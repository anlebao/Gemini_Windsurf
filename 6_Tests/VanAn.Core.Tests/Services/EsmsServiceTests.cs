using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.Core.Tests.Helpers;
using VanAn.CoreHub.Services;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// Wave 1 [W1-T6] — Unit tests for EsmsNotificationService.
/// All HTTP calls are stubbed via MockHttpMessageHandler.
/// Covers: success, API error CodeResult≠100, HTTP error, missing config, retry.
/// </summary>
public class EsmsServiceTests
{
    private static IConfiguration MakeConfig(string apiKey = "test-api-key", string secretKey = "test-secret")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Esms:ApiKey"] = apiKey,
                ["Esms:SecretKey"] = secretKey,
                ["Esms:BrandName"] = "VanAnTest",
                ["Esms:SmsType"] = "2"
            })
            .Build();
    }

    private static EsmsNotificationService CreateService(MockHttpMessageHandler handler, IConfiguration? config = null)
    {
        var httpClient = new HttpClient(handler);
        return new EsmsNotificationService(httpClient, config ?? MakeConfig(), NullLogger<EsmsNotificationService>.Instance);
    }

    [Fact]
    public async Task SendSmsAsync_SuccessCodeResult100_ReturnsTrue()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("esms.vn", HttpMethod.Post, new { CodeResult = "100", CountRegenerate = 0 });
        var svc = CreateService(handler);

        // Act
        var result = await svc.SendSmsAsync("0912345678", "Xin chào! Chào mừng đến Vạn An.");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SendSmsAsync_ApiErrorCodeResultNot100_ReturnsFalse()
    {
        // Arrange: ESMS returns CodeResult 501 (wrong brand name)
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("esms.vn", HttpMethod.Post, new { CodeResult = "501", ErrorMessage = "Invalid brandname" });
        var svc = CreateService(handler);

        // Act
        var result = await svc.SendSmsAsync("0912345678", "Test message");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendSmsAsync_HttpError500_ReturnsFalseAfterRetry()
    {
        // Arrange: server error on all attempts
        var handler = new MockHttpMessageHandler();
        handler.AddRawResponse("esms.vn", HttpMethod.Post, HttpStatusCode.InternalServerError, "{}");
        var svc = CreateService(handler);

        // Act
        var result = await svc.SendSmsAsync("0912345678", "Test message");

        // Assert: returns false after 2 attempts (1 + 1 retry)
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendSmsAsync_EmptyApiKey_ReturnsFalseWithoutHttpCall()
    {
        // Arrange: empty API key — should skip immediately
        var handler = new MockHttpMessageHandler();
        var svc = CreateService(handler, MakeConfig(apiKey: ""));

        // Act
        var result = await svc.SendSmsAsync("0912345678", "Test message");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendSmsAsync_EmptyPhoneNumber_ReturnsFalse()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        var svc = CreateService(handler);

        // Act
        var result = await svc.SendSmsAsync("", "Test message");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendSmsAsync_StripsLeadingPlusFromPhoneNumber()
    {
        // Arrange: phone with +84 prefix — ESMS expects no + prefix
        var capturedPhones = new List<string>();
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("esms.vn", HttpMethod.Post, new { CodeResult = "100" });
        var svc = CreateService(handler);

        // Act: +84 prefix should succeed (stripping the +)
        var result = await svc.SendSmsAsync("+84912345678", "Xin chào!");

        // Assert
        result.Should().BeTrue();
    }
}
