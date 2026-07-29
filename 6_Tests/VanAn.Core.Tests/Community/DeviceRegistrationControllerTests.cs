using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Services;
using VanAn.Gateway.Controllers;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using Xunit;

namespace VanAn.Core.Tests.Community
{
    /// <summary>
    /// CC-S0-T3 (Sprint 0.5): Unit tests for Gateway DeviceRegistrationController.
    /// Verifies token validation, request validation, and service call flow.
    /// Uses mocked IDeviceRegistrationService + IHttpClientFactory (ShopERP /me forward mocked).
    /// </summary>
    public class DeviceRegistrationControllerTests
    {
        [Fact]
        public async Task RegisterDevice_WithoutToken_Returns401()
        {
            var (controller, _) = BuildController();
            var req = new DeviceRegistrationController.RegisterDeviceRequest
            {
                DeviceToken = "token123",
                FingerprintHash = "hash123"
            };

            var result = await controller.RegisterDevice(req);

            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.NotNull(unauthorized.Value);
        }

        [Fact]
        public async Task RegisterDevice_WithEmptyFingerprint_Returns400()
        {
            var (controller, _) = BuildController(withValidToken: true);
            var req = new DeviceRegistrationController.RegisterDeviceRequest
            {
                DeviceToken = "token123",
                FingerprintHash = ""
            };

            var result = await controller.RegisterDevice(req);

            // Token validation passes (mocked), then request validation fails
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task RegisterDevice_WithEmptyDeviceToken_Returns400()
        {
            var (controller, _) = BuildController(withValidToken: true);
            var req = new DeviceRegistrationController.RegisterDeviceRequest
            {
                DeviceToken = "",
                FingerprintHash = "hash123"
            };

            var result = await controller.RegisterDevice(req);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        private static (DeviceRegistrationController controller, MockDeviceRegistrationService service) BuildController(bool withValidToken = false)
        {
            var service = new MockDeviceRegistrationService();
            var httpFactory = new MockHttpClientFactory(withValidToken);
            var controller = new DeviceRegistrationController(
                service,
                httpFactory,
                NullLogger<DeviceRegistrationController>.Instance);

            var httpContext = new DefaultHttpContext();
            if (withValidToken)
                httpContext.Request.Headers["X-Customer-Token"] = "valid-token";
            httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            return (controller, service);
        }
    }

    /// <summary>
    /// Mock IDeviceRegistrationService — tracks calls, returns fake DeviceRegistrationResult.
    /// </summary>
    internal class MockDeviceRegistrationService : IDeviceRegistrationService
    {
        public int CallCount { get; private set; }
        public Guid LastCustomerId { get; private set; }

        public Task<DeviceRegistrationResult> RegisterDeviceAsync(
            Guid customerId, string deviceToken, string fingerprintHash,
            string fingerprintSignals, string userAgent, string platform, string ipAddress)
        {
            CallCount++;
            LastCustomerId = customerId;
            var tenantId = new TenantId(Guid.NewGuid());
            var device = new DeviceRegistration(
                tenantId, customerId, deviceToken, fingerprintHash,
                fingerprintSignals, userAgent, platform, ipAddress);
            return Task.FromResult(new DeviceRegistrationResult(device, null));
        }
    }

    /// <summary>
    /// Mock IHttpClientFactory — returns a fake HttpClient that responds to
    /// /api/customer-identity/me with 200 + customerId (if withValidToken=true),
    /// or 401 (if false).
    /// </summary>
    internal class MockHttpClientFactory : IHttpClientFactory
    {
        private readonly bool _withValidToken;

        public MockHttpClientFactory(bool withValidToken) => _withValidToken = withValidToken;

        public HttpClient CreateClient(string name)
        {
            var handler = new MockHttpHandler(_withValidToken);
            return new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        }

        private class MockHttpHandler : HttpMessageHandler
        {
            private readonly bool _withValidToken;

            public MockHttpHandler(bool withValidToken) => _withValidToken = withValidToken;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (!_withValidToken)
                {
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized));
                }

                var json = """{"customerId":"00000000-0000-0000-0000-000000000001"}""";
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                });
            }
        }
    }
}
