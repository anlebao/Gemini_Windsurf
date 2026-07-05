using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using VanAn.Core.Tests.Helpers;
using VanAn.CoreHub.Services.Providers.EInvoice;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// Unit tests for ViettelEInvoiceProvider — W6-T4 rewrite per real Viettel S-Invoice v2.0 API spec.
///
/// Endpoint map (from ViettelEInvoiceProvider.cs — W6-T4 rewrite):
///   Auth   : POST auth/login                                      → ViettelAuthResponse { access_token } (Cookie-based)
///   Submit : POST InvoiceAPI/InvoiceWS/createInvoice/{taxCode}    → ViettelInvoiceResult { errorCode, result.{invoiceNo, transactionID, reservationCode} }
///   Status : POST InvoiceAPI/InvoiceWS/searchInvoiceByTransactionUuid → ViettelStatusResult { result.{invoiceNo, invoiceStatus} }
///   Cancel : POST InvoiceAPI/InvoiceWS/cancelTransactionInvoice   → ViettelCancelResult { errorCode, description }
///   GetFile: POST InvoiceAPI/InvoiceUtilsWS/getInvoiceRepresentationFile → byte[] (PDF/XML)
///   Health : GET  health                                          → HTTP status code
///
/// ViettelConfig positional: (Username, Password, TaxCode, TemplateCode, SerialNumber, BaseUrl, ProductionBaseUrl?, SandboxBaseUrl?)
/// EInvoiceRequest positional: (TenantId, InvoiceId, OrderId, InvoiceType, Amount, VatAmount,
///                              TotalAmount, CustomerName, CustomerTaxCode, CustomerAddress,
///                              InvoiceDate, AdditionalData, SupplierTaxCode, LineItems, CurrencyCode, PaymentType)
/// </summary>
public class ViettelEInvoiceProviderTests
{
    // ── shared helpers ───────────────────────────────────────────────────────

    private static readonly string TestTaxCode = "0123456789";

    private static ViettelConfig MakeConfig() =>
        new("testuser", "testpass", TestTaxCode, "01GTKT0/001", "1C25TAA",
            BaseUrl: "https://vinvoice.viettel.vn/",
            ProductionBaseUrl: null,
            SandboxBaseUrl: null);

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
        AdditionalData:  new Dictionary<string, string>(),
        SupplierTaxCode: TestTaxCode,
        LineItems:       new List<InvoiceItem>(),
        CurrencyCode:    "VND",
        PaymentType:     "CASH");

    private static EInvoiceRequest MakeRequestWithLineItems() => new(
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
        AdditionalData:  new Dictionary<string, string>(),
        SupplierTaxCode: TestTaxCode,
        LineItems:       new List<InvoiceItem>
        {
            new(new TenantId(Guid.NewGuid()), new ElectronicInvoiceId(Guid.NewGuid()),
                "ITEM-001", "Test Product", "pcs", 2, 50000m, 10m)
        },
        CurrencyCode:    "VND",
        PaymentType:     "CASH");

    private static ViettelEInvoiceProvider MakeProvider(MockHttpMessageHandler handler)
    {
        var client  = new HttpClient(handler) { BaseAddress = new Uri("https://vinvoice.viettel.vn/") };
        var options = Options.Create(MakeConfig());
        return new ViettelEInvoiceProvider(client, options, NullLogger<ViettelEInvoiceProvider>.Instance);
    }

    // ── TC-V01: Submit thành công (errorCode=0, result.invoiceNo) ─────────────
    [Fact]
    public async Task Submit_AuthSucceeds_ErrorCode0_ReturnsSuccess()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/login", HttpMethod.Post,
            new { access_token = "vt-token" });
        handler.AddResponse("createInvoice", HttpMethod.Post,
            new
            {
                errorCode = "0",
                description = "OK",
                result = new
                {
                    supplierTaxCode = TestTaxCode,
                    invoiceNo = "VT-001",
                    transactionID = "tx-uuid-001",
                    reservationCode = "RES-001"
                }
            });

        var result = await MakeProvider(handler).SubmitInvoiceAsync(MakeRequest());

        result.Success.Should().BeTrue();
        result.ProviderInvoiceNumber.Should().Be("VT-001");
        result.ErrorMessage.Should().BeNull();
        result.TransactionUuid.Should().Be("tx-uuid-001");
        result.ReservationCode.Should().Be("RES-001");
    }

    // ── TC-V02: Auth fail 401 → Failure ──────────────────────────────────────
    [Fact]
    public async Task Submit_AuthReturns401_ReturnsFailure()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddRawResponse("auth/login", HttpMethod.Post, HttpStatusCode.Unauthorized, "");

        var result = await MakeProvider(handler).SubmitInvoiceAsync(MakeRequest());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // ── TC-V03: ErrorCode khác "0" → Failure ─────────────────────────────────
    [Fact]
    public async Task Submit_ErrorCodeNonZero_ReturnsFailure()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/login", HttpMethod.Post,
            new { access_token = "vt-token" });
        handler.AddResponse("createInvoice", HttpMethod.Post,
            new { errorCode = "1001", description = "Invalid invoice data", result = (object?)null });

        var result = await MakeProvider(handler).SubmitInvoiceAsync(MakeRequest());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid invoice data");
    }

    // ── TC-V04: Submit with line items → payload includes itemInfo[] ─────────
    // Verifies that line items are mapped to itemInfo[] in the nested payload.
    [Fact]
    public async Task Submit_WithLineItems_PayloadIncludesItemInfo()
    {
        string? capturedPayload = null;
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/login", HttpMethod.Post,
            new { access_token = "vt-token" });

        // Custom handler to capture the createInvoice payload
        handler.AddRawResponse("createInvoice", HttpMethod.Post, HttpStatusCode.OK,
            """{"errorCode":"0","description":"OK","result":{"invoiceNo":"VT-002","transactionID":"tx-002","reservationCode":"RES-002"}}""");

        // Use a wrapping handler to capture the request body
        var capturingHandler = new CapturingHandler(handler, "createInvoice");
        var client = new HttpClient(capturingHandler) { BaseAddress = new Uri("https://vinvoice.viettel.vn/") };
        var provider = new ViettelEInvoiceProvider(client, Options.Create(MakeConfig()),
            NullLogger<ViettelEInvoiceProvider>.Instance);

        var result = await provider.SubmitInvoiceAsync(MakeRequestWithLineItems());

        result.Success.Should().BeTrue();
        result.ProviderInvoiceNumber.Should().Be("VT-002");

        capturedPayload = capturingHandler.CapturedBody;
        capturedPayload.Should().NotBeNull();
        capturedPayload.Should().Contain("itemInfo");
        capturedPayload.Should().Contain("ITEM-001");
        capturedPayload.Should().Contain("Test Product");
        capturedPayload.Should().Contain("generalInvoiceInfo");
        capturedPayload.Should().Contain("buyerInfo");
        capturedPayload.Should().Contain("sellerInfo");
        capturedPayload.Should().Contain("summarizeInfo");
        capturedPayload.Should().Contain("taxBreakdowns");
        capturedPayload.Should().Contain("transactionUUID");
    }

    // ── TC-V05: Submit → Cookie auth (NOT Bearer) ────────────────────────────
    // Verifies that auth sets Cookie header, not Authorization Bearer.
    [Fact]
    public async Task Submit_AuthUsesCookieNotBearer()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/login", HttpMethod.Post,
            new { access_token = "vt-cookie-token" });
        handler.AddResponse("createInvoice", HttpMethod.Post,
            new { errorCode = "0", result = new { invoiceNo = "VT-003" } });

        var capturingHandler = new CapturingHandler(handler, "createInvoice");
        var client = new HttpClient(capturingHandler) { BaseAddress = new Uri("https://vinvoice.viettel.vn/") };
        var provider = new ViettelEInvoiceProvider(client, Options.Create(MakeConfig()),
            NullLogger<ViettelEInvoiceProvider>.Instance);

        await provider.SubmitInvoiceAsync(MakeRequest());

        var createRequest = capturingHandler.CapturedRequest;
        createRequest.Should().NotBeNull();
        // Cookie header should contain access_token
        createRequest!.Headers.Contains("Cookie").Should().BeTrue();
        var cookieValues = createRequest.Headers.GetValues("Cookie");
        cookieValues.Should().Contain(v => v.Contains("access_token=vt-cookie-token"));
        // Should NOT have Authorization Bearer header
        createRequest.Headers.Authorization.Should().BeNull();
    }

    // ── TC-V06: Submit → transactionUUID in payload ──────────────────────────
    [Fact]
    public async Task Submit_PayloadContainsTransactionUuid()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/login", HttpMethod.Post,
            new { access_token = "vt-token" });
        handler.AddResponse("createInvoice", HttpMethod.Post,
            new { errorCode = "0", result = new { invoiceNo = "VT-004" } });

        var capturingHandler = new CapturingHandler(handler, "createInvoice");
        var client = new HttpClient(capturingHandler) { BaseAddress = new Uri("https://vinvoice.viettel.vn/") };
        var provider = new ViettelEInvoiceProvider(client, Options.Create(MakeConfig()),
            NullLogger<ViettelEInvoiceProvider>.Instance);

        var request = MakeRequest();
        await provider.SubmitInvoiceAsync(request);

        var payload = capturingHandler.CapturedBody!;
        payload.Should().Contain($"\"transactionUUID\":\"{request.TransactionUuid}\"");
    }

    // ── TC-V07: GetStatus APPROVED → TaxApproved ─────────────────────────────
    [Fact]
    public async Task GetStatus_Approved_ReturnsTaxApproved()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/login", HttpMethod.Post,
            new { access_token = "vt-token" });
        handler.AddResponse("searchInvoiceByTransactionUuid", HttpMethod.Post,
            new
            {
                errorCode = "0",
                result = new { invoiceNo = "VT-001", invoiceStatus = "APPROVED" }
            });

        var tenantId = new TenantId(Guid.NewGuid());
        var result = await MakeProvider(handler)
            .GetInvoiceStatusAsync(tenantId, "tx-uuid-001");

        result.Status.Should().Be(InvoiceStatus.TaxApproved);
        result.ApprovedAt.Should().NotBeNull();
    }

    // ── TC-V08: GetStatus REJECTED → Rejected ────────────────────────────────
    [Fact]
    public async Task GetStatus_Rejected_ReturnsRejected()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/login", HttpMethod.Post,
            new { access_token = "vt-token" });
        handler.AddResponse("searchInvoiceByTransactionUuid", HttpMethod.Post,
            new
            {
                errorCode = "0",
                description = "Rejected by CQT",
                result = new { invoiceNo = "VT-001", invoiceStatus = "REJECTED" }
            });

        var tenantId = new TenantId(Guid.NewGuid());
        var result = await MakeProvider(handler)
            .GetInvoiceStatusAsync(tenantId, "tx-uuid-001");

        result.Status.Should().Be(InvoiceStatus.Rejected);
        result.ApprovedAt.Should().BeNull();
        result.FailureReason.Should().Contain("Rejected by CQT");
    }

    // ── TC-V09: GetStatus unknown → PendingSend ──────────────────────────────
    [Fact]
    public async Task GetStatus_Unknown_ReturnsPendingSend()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/login", HttpMethod.Post,
            new { access_token = "vt-token" });
        handler.AddResponse("searchInvoiceByTransactionUuid", HttpMethod.Post,
            new
            {
                errorCode = "0",
                result = new { invoiceNo = "VT-001", invoiceStatus = "PROCESSING" }
            });

        var tenantId = new TenantId(Guid.NewGuid());
        var result = await MakeProvider(handler)
            .GetInvoiceStatusAsync(tenantId, "tx-uuid-001");

        result.Status.Should().Be(InvoiceStatus.PendingSend);
    }

    // ── TC-V10: Cancel 200 + errorCode=0 → Success ───────────────────────────
    [Fact]
    public async Task Cancel_200_ErrorCode0_ReturnsSuccess()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/login", HttpMethod.Post,
            new { access_token = "vt-token" });
        handler.AddResponse("cancelTransactionInvoice", HttpMethod.Post,
            new { errorCode = "0", description = "Cancelled" });

        var tenantId = new TenantId(Guid.NewGuid());
        var result = await MakeProvider(handler)
            .CancelInvoiceAsync(tenantId, "VT-001", "Sai thong tin");

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    // ── TC-V11: Cancel errorCode != 0 → Failure ──────────────────────────────
    [Fact]
    public async Task Cancel_ErrorCodeNonZero_ReturnsFailure()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/login", HttpMethod.Post,
            new { access_token = "vt-token" });
        handler.AddResponse("cancelTransactionInvoice", HttpMethod.Post,
            new { errorCode = "2001", description = "Invoice already cancelled" });

        var tenantId = new TenantId(Guid.NewGuid());
        var result = await MakeProvider(handler)
            .CancelInvoiceAsync(tenantId, "VT-001", "Sai thong tin");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invoice already cancelled");
    }

    // ── TC-V12: Cancel 500 → Failure ─────────────────────────────────────────
    [Fact]
    public async Task Cancel_500_ReturnsFailure()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/login", HttpMethod.Post,
            new { access_token = "vt-token" });
        handler.AddRawResponse("cancelTransactionInvoice", HttpMethod.Post,
            HttpStatusCode.InternalServerError, "{}");

        var tenantId = new TenantId(Guid.NewGuid());
        var result = await MakeProvider(handler)
            .CancelInvoiceAsync(tenantId, "VT-001", "Sai thong tin");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // ── TC-V13: GetInvoiceFile returns byte[] ────────────────────────────────
    [Fact]
    public async Task GetInvoiceFile_ReturnsByteArray()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/login", HttpMethod.Post,
            new { access_token = "vt-token" });
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // "%PDF-1.4"
        handler.AddRawResponse("getInvoiceRepresentationFile", HttpMethod.Post,
            HttpStatusCode.OK, System.Text.Encoding.UTF8.GetString(pdfBytes));

        var tenantId = new TenantId(Guid.NewGuid());
        var result = await MakeProvider(handler)
            .GetInvoiceFileAsync(tenantId, "VT-001", "pdf");

        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    // ── TC-V14: HealthCheck 200 → true ───────────────────────────────────────
    [Fact]
    public async Task HealthCheck_200_ReturnsTrue()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddRawResponse("health", HttpMethod.Get, HttpStatusCode.OK, "{}");

        var result = await MakeProvider(handler).HealthCheckAsync();

        result.Should().BeTrue();
    }

    // ── TC-V15: HealthCheck 503 → false ──────────────────────────────────────
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

/// <summary>
/// Wrapping handler that captures the request body + headers for a specific URL substring.
/// Used to verify payload structure (nested fields, Cookie auth, transactionUUID).
/// DelegatingHandler allows calling base.SendAsync (unlike HttpMessageHandler).
/// </summary>
internal class CapturingHandler : DelegatingHandler
{
    private readonly string _captureUrlSubstring;

    public string? CapturedBody { get; private set; }
    public HttpRequestMessage? CapturedRequest { get; private set; }

    public CapturingHandler(HttpMessageHandler inner, string captureUrlSubstring)
        : base(inner)
    {
        _captureUrlSubstring = captureUrlSubstring;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString() ?? "";
        if (url.Contains(_captureUrlSubstring, StringComparison.OrdinalIgnoreCase))
        {
            if (request.Content is not null)
            {
                CapturedBody = await request.Content.ReadAsStringAsync(cancellationToken);
                // Clone headers before sending (request may be disposed after send)
                CapturedRequest = new HttpRequestMessage(request.Method, request.RequestUri);
                foreach (var h in request.Headers)
                {
                    CapturedRequest.Headers.TryAddWithoutValidation(h.Key, h.Value);
                }
            }
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
