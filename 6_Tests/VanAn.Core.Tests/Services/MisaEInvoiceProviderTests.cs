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
/// Unit tests for MisaEInvoiceProvider — all HTTP calls are stubbed via MockHttpMessageHandler.
///
/// Endpoint map (from MisaEInvoiceProvider.cs):
///   Auth   : POST auth/login                          → MisaAuthResponse  { access_token }
///   Submit : POST einvoices                           → MisaInvoiceResult { is_success, inv_no, error_message }
///   Status : GET  einvoices/{id}/status               → MisaStatusResult  { invoice_status, approved_date }  (snake_case)
///   Cancel : POST einvoices/{id}/cancel               → HTTP status code
///   Health : GET  health                              → HTTP status code
///
/// MisaConfig positional: (CompanyCode, ApiKey, Username, Password, InvoiceSeries, SandboxBaseUrl)
/// EInvoiceRequest positional: (TenantId, InvoiceId, OrderId, InvoiceType, Amount, VatAmount,
///                              TotalAmount, CustomerName, CustomerTaxCode, CustomerAddress,
///                              InvoiceDate, AdditionalData)
///
/// JSON property names (from MisaDTOs.cs — snake_case):
///   MisaInvoiceResult : is_success, inv_no, error_message
///   MisaStatusResult  : invoice_status, approved_date
/// </summary>
public class MisaEInvoiceProviderTests
{
    // ── shared helpers ───────────────────────────────────────────────────────

    private static MisaConfig MakeConfig() =>
        new("CTYTEST", "api-key-test", "testuser", "testpass", "1C25TAA",
            "https://api.meinvoice.vn/");

    private static EInvoiceRequest MakeRequest() => new(
        TenantId:        new TenantId(Guid.NewGuid()),
        InvoiceId:       new ElectronicInvoiceId(Guid.NewGuid()),
        OrderId:         new OrderId(Guid.NewGuid()),
        InvoiceType:     InvoiceType.Goods,
        Amount:          200_000m,
        VatAmount:       20_000m,
        TotalAmount:     220_000m,
        CustomerName:    "Cong ty Misa Test",
        CustomerTaxCode: "0987654321",
        CustomerAddress: "456 Tran Hung Dao, Ha Noi",
        InvoiceDate:     DateTime.UtcNow,
        AdditionalData:  new Dictionary<string, string>());

    private static MisaEInvoiceProvider MakeProvider(MockHttpMessageHandler handler)
    {
        var client  = new HttpClient(handler) { BaseAddress = new Uri("https://api.meinvoice.vn/") };
        var options = Options.Create(MakeConfig());
        return new MisaEInvoiceProvider(client, options, NullLogger<MisaEInvoiceProvider>.Instance);
    }

    // ── TC-M01: Submit thành công ────────────────────────────────────────────
    // Mock: auth/login → { access_token:"misa-token" }
    //       einvoices (POST) → { is_success:true, inv_no:"MS-001" }  ← snake_case!
    // Expected: Success=true, ProviderInvoiceNumber="MS-001", ErrorMessage=null
    [Fact]
    public async Task Submit_AuthSucceeds_IsSuccessTrue_ReturnsSuccess()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/login", HttpMethod.Post,
            new { access_token = "misa-token" });
        handler.AddResponse("einvoices", HttpMethod.Post,
            new { is_success = true, inv_no = "MS-001", error_message = (string?)null });

        var result = await MakeProvider(handler).SubmitInvoiceAsync(MakeRequest());

        result.Success.Should().BeTrue();
        result.ProviderInvoiceNumber.Should().Be("MS-001");
        result.ErrorMessage.Should().BeNull();
    }

    // ── TC-M02: Auth fail 401 → Failure ─────────────────────────────────────
    // Mock: auth/login → 401 Unauthorized
    // Expected: Success=false, ErrorMessage≠null (caught InvalidOperationException)
    [Fact]
    public async Task Submit_AuthReturns401_ReturnsFailure()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddRawResponse("auth/login", HttpMethod.Post, HttpStatusCode.Unauthorized, "");

        var result = await MakeProvider(handler).SubmitInvoiceAsync(MakeRequest());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // ── TC-M03: Submit is_success=false → Failure ───────────────────────────
    // Mock: auth OK, einvoices → { is_success:false, error_message:"Duplicate invoice" }
    // Expected: Success=false, ErrorMessage contains "Duplicate invoice"
    [Fact]
    public async Task Submit_IsSuccessFalse_ReturnsFailure()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/login", HttpMethod.Post,
            new { access_token = "misa-token" });
        handler.AddResponse("einvoices", HttpMethod.Post,
            new { is_success = false, inv_no = (string?)null, error_message = "Duplicate invoice" });

        var result = await MakeProvider(handler).SubmitInvoiceAsync(MakeRequest());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Duplicate invoice");
    }

    // ── TC-M04: GetStatus APPROVED → TaxApproved ────────────────────────────
    // Mock: auth OK, einvoices/MS-001/status (url contains "/status") →
    //       { invoice_status:"APPROVED", approved_date:"2025-06-14T00:00:00Z" }  ← snake_case!
    // Expected: Status=TaxApproved, ApprovedAt≠null
    [Fact]
    public async Task GetStatus_Approved_ReturnsTaxApproved()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/login", HttpMethod.Post,
            new { access_token = "misa-token" });
        handler.AddResponse("/status", HttpMethod.Get,
            new { invoice_status = "APPROVED", approved_date = "2025-06-14T00:00:00Z" });

        var tenantId = new TenantId(Guid.NewGuid());
        var result   = await MakeProvider(handler)
            .GetInvoiceStatusAsync(tenantId, "MS-001");

        result.Status.Should().Be(InvoiceStatus.TaxApproved);
        result.ApprovedAt.Should().NotBeNull();
    }

    // ── TC-M05: GetStatus REJECTED → Rejected ───────────────────────────────
    // Mock: auth OK, /status → { invoice_status:"REJECTED" }
    // Expected: Status=Rejected, ApprovedAt=null
    [Fact]
    public async Task GetStatus_Rejected_ReturnsRejected()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/login", HttpMethod.Post,
            new { access_token = "misa-token" });
        handler.AddResponse("/status", HttpMethod.Get,
            new { invoice_status = "REJECTED", approved_date = (string?)null });

        var tenantId = new TenantId(Guid.NewGuid());
        var result   = await MakeProvider(handler)
            .GetInvoiceStatusAsync(tenantId, "MS-001");

        result.Status.Should().Be(InvoiceStatus.Rejected);
        result.ApprovedAt.Should().BeNull();
    }

    // ── TC-M06: GetStatus PROCESSING → PendingSend ──────────────────────────
    // Mock: auth OK, /status → { invoice_status:"PROCESSING" }
    // Expected: Status=PendingSend (default branch in switch)
    [Fact]
    public async Task GetStatus_Processing_ReturnsPendingSend()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/login", HttpMethod.Post,
            new { access_token = "misa-token" });
        handler.AddResponse("/status", HttpMethod.Get,
            new { invoice_status = "PROCESSING", approved_date = (string?)null });

        var tenantId = new TenantId(Guid.NewGuid());
        var result   = await MakeProvider(handler)
            .GetInvoiceStatusAsync(tenantId, "MS-001");

        result.Status.Should().Be(InvoiceStatus.PendingSend);
    }

    // ── TC-M07: Cancel 200 → Success ────────────────────────────────────────
    // Mock: auth OK, einvoices/MS-001/cancel (url contains "/cancel") → 200 OK
    // Expected: Success=true, ErrorMessage=null
    [Fact]
    public async Task Cancel_200_ReturnsSuccess()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/login", HttpMethod.Post,
            new { access_token = "misa-token" });
        handler.AddRawResponse("/cancel", HttpMethod.Post, HttpStatusCode.OK, "{}");

        var tenantId = new TenantId(Guid.NewGuid());
        var result   = await MakeProvider(handler)
            .CancelInvoiceAsync(tenantId, "MS-001", "Khach doi y");

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    // ── TC-M08: Cancel 500 → Failure ────────────────────────────────────────
    // Mock: auth OK, /cancel → 500 Internal Server Error
    // Expected: Success=false, ErrorMessage contains "Cancel failed"
    [Fact]
    public async Task Cancel_500_ReturnsFailure()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/login", HttpMethod.Post,
            new { access_token = "misa-token" });
        handler.AddRawResponse("/cancel", HttpMethod.Post,
            HttpStatusCode.InternalServerError, "{}");

        var tenantId = new TenantId(Guid.NewGuid());
        var result   = await MakeProvider(handler)
            .CancelInvoiceAsync(tenantId, "MS-001", "Khach doi y");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Cancel failed");
    }

    // ── TC-M09: HealthCheck 200 → true ──────────────────────────────────────
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

    // ── TC-M10: HealthCheck 503 → false ─────────────────────────────────────
    // Mock: health → 503 Service Unavailable (caught in try/catch, returns false)
    // Expected: false
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
