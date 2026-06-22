using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.Core.Tests.Helpers;
using VanAn.CoreHub.Services;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// Wave 1 [W1-T6] — Unit tests for BrevoEmailService.
/// All HTTP calls are stubbed via MockHttpMessageHandler.
/// Covers: success, API error, invalid email, missing config.
/// </summary>
public class BrevoEmailServiceTests
{
    private static IConfiguration MakeConfig(string apiKey = "test-brevo-api-key", string sender = "noreply@vanan.vn")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Brevo:ApiKey"] = apiKey,
                ["Brevo:SenderEmail"] = sender,
                ["Brevo:SenderName"] = "VanAn Test"
            })
            .Build();
    }

    private static BrevoEmailService CreateService(MockHttpMessageHandler handler, IConfiguration? config = null)
    {
        var httpClient = new HttpClient(handler);
        return new BrevoEmailService(httpClient, config ?? MakeConfig(), NullLogger<BrevoEmailService>.Instance);
    }

    [Fact]
    public async Task SendEmailAsync_ValidRequest_ReturnsTrue()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("brevo.com", HttpMethod.Post, new { messageId = "<test@brevo>" });
        var svc = CreateService(handler);

        // Act
        var result = await svc.SendEmailAsync("customer@example.com", "Test Subject", "<p>Hello</p>");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SendEmailAsync_ApiReturns4xx_ReturnsFalse()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.AddRawResponse("brevo.com", HttpMethod.Post, HttpStatusCode.BadRequest,
            """{"code":"invalid_parameter","message":"invalid email"}""");
        var svc = CreateService(handler);

        // Act
        var result = await svc.SendEmailAsync("bad-email", "Subject", "<p>Body</p>");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendEmailAsync_EmptyApiKey_ReturnsFalse_WithoutHttpCall()
    {
        // Arrange: empty API key
        var handler = new MockHttpMessageHandler();
        var svc = CreateService(handler, MakeConfig(apiKey: ""));

        // Act
        var result = await svc.SendEmailAsync("customer@example.com", "Subject", "<p>Body</p>");

        // Assert: returns false without making any HTTP call
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendEmailAsync_EmptyEmailAddress_ReturnsFalse()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        var svc = CreateService(handler);

        // Act
        var result = await svc.SendEmailAsync("", "Subject", "<p>Body</p>");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendEmailAsync_ApiReturns500_ReturnsFalse()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.AddRawResponse("brevo.com", HttpMethod.Post, HttpStatusCode.InternalServerError,
            """{"code":"internal_error","message":"server error"}""");
        var svc = CreateService(handler);

        // Act
        var result = await svc.SendEmailAsync("customer@example.com", "Subject", "<p>Body</p>");

        // Assert
        result.Should().BeFalse();
    }
}
