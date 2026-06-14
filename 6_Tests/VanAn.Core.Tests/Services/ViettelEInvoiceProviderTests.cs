using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using VanAn.Core.Tests.Helpers;
using VanAn.CoreHub.Services.Providers.EInvoice;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// Unit tests for ViettelEInvoiceProvider — all HTTP calls are stubbed via MockHttpMessageHandler.
///
/// Endpoint map (from ViettelEInvoiceProvider.cs):
///   Auth   : POST auth/token                                → ViettelAuthResponse { access_token }
///   Submit : POST InvoiceAPI/services/createInvoice         → ViettelInvoiceResult { errorCode, description, result.invoiceNo }
///   Status : GET  InvoiceAPI/services/getInvoiceStatus?...  → ViettelStatusResult  { invoiceStatus }  (camelCase)
///   Cancel : POST InvoiceAPI/services/cancelInvoice         → HTTP status code
///   Health : GET  health                                    → HTTP status code
///
/// ViettelConfig positional: (Username, Password, TaxCode, TemplateCode, SerialNumber, SandboxBaseUrl)
/// EInvoiceRequest positional: (TenantId, InvoiceId, OrderId, InvoiceType, Amount, VatAmount,
///                              TotalAmount, CustomerName, CustomerTaxCode, CustomerAddress,
///                              InvoiceDate, AdditionalData)
/// </summary>
public class ViettelEInvoiceProviderTests
{
    // ── shared helpers ───────────────────────────────────────────────────────

    private static ViettelConfig MakeConfig() =>
        new("testuser", "testpass", "0123456789", "01GTKT0/001", "1C25TAA",
            "https://sinvoice.viettel.vn/");

    private static EInvoiceRequest MakeRequest() => new(
        TenantId:        new TenantId(Guid.NewGuid()),
        InvoiceId:       new ElectronicInvoiceId(Guid.NewGuid()),
        OrderId:         new OrderId(Guid.NewGuid()),
        InvoiceType:     InvoiceType.Goods,
        Amount:          100_000m,
        VatAmount:       10_000m,
        TotalAmount:     110_000m,
        CustomerName:    "Cong ty TNHH Test",
        CustomerTaxCode: "0987654321",
        CustomerAddress: "123 Le Loi, Ha Noi",
        InvoiceDate:     DateTime.UtcNow,
        AdditionalData:  new Dictionary<string, string>());

    private static ViettelEInvoiceProvider MakeProvider(MockHttpMessageHandler handler)
    {
        var client  = new HttpClient(handler) { BaseAddress = new Uri("https://sinvoice.viettel.vn/") };
        var options = Options.Create(MakeConfig());
        return new ViettelEInvoiceProvider(client, options, NullLogger<ViettelEInvoiceProvider>.Instance);
    }

    // ── TC-V01: Submit thành công ────────────────────────────────────────────
    // Mock: auth/token → { access_token:"vt-token" }
    //       createInvoice → { errorCode:"0", result:{ invoiceNo:"VT-001" } }
    // Expected: Success=true, ProviderInvoiceNumber="VT-001", ErrorMessage=null
    [Fact]
    public async Task Submit_AuthSucceeds_ErrorCode0_ReturnsSuccess()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/token", HttpMethod.Post,
            new { access_token = "vt-token" });
        handler.AddResponse("createInvoice", HttpMethod.Post,
            new { errorCode = "0", description = "OK", result = new { invoiceNo = "VT-001" } });

        var result = await MakeProvider(handler).SubmitInvoiceAsync(MakeRequest());

        result.Success.Should().BeTrue();
        result.ProviderInvoiceNumber.Should().Be("VT-001");
        result.ErrorMessage.Should().BeNull();
    }

    // ── TC-V02: Auth fail 401 → Failure ─────────────────────────────────────
    // Mock: auth/token → 401 Unauthorized
    // Expected: Success=false, ErrorMessage≠null (caught InvalidOperationException)
    [Fact]
    public async Task Submit_AuthReturns401_ReturnsFailure()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddRawResponse("auth/token", HttpMethod.Post, HttpStatusCode.Unauthorized, "");

        var result = await MakeProvider(handler).SubmitInvoiceAsync(MakeRequest());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // ── TC-V03: ErrorCode khác "0" → Failure ────────────────────────────────
    // Mock: auth OK, createInvoice → { errorCode:"1001", description:"Invalid invoice data" }
    // Expected: Success=false, ErrorMessage contains "Invalid invoice data"
    [Fact]
    public async Task Submit_ErrorCodeNonZero_ReturnsFailure()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/token", HttpMethod.Post,
            new { access_token = "vt-token" });
        handler.AddResponse("createInvoice", HttpMethod.Post,
            new { errorCode = "1001", description = "Invalid invoice data", result = (object?)null });

        var result = await MakeProvider(handler).SubmitInvoiceAsync(MakeRequest());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid invoice data");
    }

    // ── TC-V04: GetStatus APPROVED → TaxApproved ────────────────────────────
    // Mock: auth OK, getInvoiceStatus → { invoiceStatus:"APPROVED" }  (camelCase per ViettelDTOs.cs)
    // Expected: Status=TaxApproved, ApprovedAt≠null
    [Fact]
    public async Task GetStatus_Approved_ReturnsTaxApproved()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/token", HttpMethod.Post,
            new { access_token = "vt-token" });
        handler.AddResponse("getInvoiceStatus", HttpMethod.Get,
            new { errorCode = "0", invoiceStatus = "APPROVED" });

        var tenantId = new TenantId(Guid.NewGuid());
        var result   = await MakeProvider(handler)
            .GetInvoiceStatusAsync(tenantId, "VT-001");

        result.Status.Should().Be(InvoiceStatus.TaxApproved);
        result.ApprovedAt.Should().NotBeNull();
    }

    // ── TC-V05: GetStatus REJECTED → Rejected ───────────────────────────────
    // Mock: auth OK, getInvoiceStatus → { invoiceStatus:"REJECTED" }
    // Expected: Status=Rejected, ApprovedAt=null
    [Fact]
    public async Task GetStatus_Rejected_ReturnsRejected()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/token", HttpMethod.Post,
            new { access_token = "vt-token" });
        handler.AddResponse("getInvoiceStatus", HttpMethod.Get,
            new { errorCode = "0", invoiceStatus = "REJECTED" });

        var tenantId = new TenantId(Guid.NewGuid());
        var result   = await MakeProvider(handler)
            .GetInvoiceStatusAsync(tenantId, "VT-001");

        result.Status.Should().Be(InvoiceStatus.Rejected);
        result.ApprovedAt.Should().BeNull();
    }

    // ── TC-V06: GetStatus unknown → PendingSend ──────────────────────────────
    // Mock: auth OK, getInvoiceStatus → { invoiceStatus:"PROCESSING" }
    // Expected: Status=PendingSend (default branch in switch)
    [Fact]
    public async Task GetStatus_Unknown_ReturnsPendingSend()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/token", HttpMethod.Post,
            new { access_token = "vt-token" });
        handler.AddResponse("getInvoiceStatus", HttpMethod.Get,
            new { errorCode = "0", invoiceStatus = "PROCESSING" });

        var tenantId = new TenantId(Guid.NewGuid());
        var result   = await MakeProvider(handler)
            .GetInvoiceStatusAsync(tenantId, "VT-001");

        result.Status.Should().Be(InvoiceStatus.PendingSend);
    }

    // ── TC-V07: Cancel 200 → Success ────────────────────────────────────────
    // Mock: auth OK, cancelInvoice → 200 OK
    // Expected: Success=true, ErrorMessage=null
    [Fact]
    public async Task Cancel_200_ReturnsSuccess()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/token", HttpMethod.Post,
            new { access_token = "vt-token" });
        handler.AddRawResponse("cancelInvoice", HttpMethod.Post,
            HttpStatusCode.OK, "{}");

        var tenantId = new TenantId(Guid.NewGuid());
        var result   = await MakeProvider(handler)
            .CancelInvoiceAsync(tenantId, "VT-001", "Sai thong tin");

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    // ── TC-V08: Cancel 500 → Failure ────────────────────────────────────────
    // Mock: auth OK, cancelInvoice → 500 Internal Server Error
    // Expected: Success=false, ErrorMessage contains "Cancel failed"
    [Fact]
    public async Task Cancel_500_ReturnsFailure()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/token", HttpMethod.Post,
            new { access_token = "vt-token" });
        handler.AddRawResponse("cancelInvoice", HttpMethod.Post,
            HttpStatusCode.InternalServerError, "{}");

        var tenantId = new TenantId(Guid.NewGuid());
        var result   = await MakeProvider(handler)
            .CancelInvoiceAsync(tenantId, "VT-001", "Sai thong tin");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Cancel failed");
    }

    // ── TC-V09: HealthCheck 200 → true ──────────────────────────────────────
    // Mock: health → 200 OK
    // Expected: true
    [Fact]
    public async Task HealthCheck_200_ReturnsTrue()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddRawResponse("health", HttpMethod.Get, HttpStatusCode.OK, "{}");

        var result = await MakeProvider(handler).HealthCheckAsync();

        result.Should().BeTrue();
    }

    // ── TC-V10: HealthCheck 503 → false ─────────────────────────────────────
    // Mock: health → 503 Service Unavailable
    // Expected: false (caught in try/catch, returns false)
    [Fact]
    public async Task HealthCheck_503_ReturnsFalse()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddRawResponse("health", HttpMethod.Get,
            HttpStatusCode.ServiceUnavailable, "");

        var result = await MakeProvider(handler).HealthCheckAsync();

        result.Should().BeFalse();
    }
}
