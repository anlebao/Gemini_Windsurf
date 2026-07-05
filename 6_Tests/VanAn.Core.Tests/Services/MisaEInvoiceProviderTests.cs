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
/// Unit tests for MisaEInvoiceProvider — W6-T5 rewrite per real MISA meInvoice API spec.
///
/// Endpoint map (from MisaEInvoiceProvider.cs — W6-T5 rewrite):
///   Auth   : POST api/integration/auth/token             → MisaAuthResponse { Success, Data.{access_token}, ErrorCode }
///   Submit : POST api/integration/invoice                → MisaInvoiceResult { Success, Data.{InvNo, TransactionID}, ErrorCode }
///   Status : GET  api/integration/invoice/{invNo}        → MisaStatusResult  { Success, Data.{InvNo, InvStatus, ApprovedDate} }
///   Cancel : POST api/integration/invoice/cancel         → MisaCancelResult  { Success, ErrorCode, ErrorMessage }
///   GetFile: GET  api/integration/invoice/{invNo}/file   → byte[] (PDF/XML)
///   Health : GET  health                                  → HTTP status code
///
/// MisaConfig positional: (CompanyCode, ApiKey, AppId, Username, Password, InvoiceSeries, BaseUrl, ProductionBaseUrl?, SandboxBaseUrl?)
/// EInvoiceRequest positional: (TenantId, InvoiceId, OrderId, InvoiceType, Amount, VatAmount,
///                              TotalAmount, CustomerName, CustomerTaxCode, CustomerAddress,
///                              InvoiceDate, AdditionalData, SupplierTaxCode, LineItems, CurrencyCode, PaymentType)
///
/// JSON property names (from MisaDTOs.cs — W6-T5 rewrite, PascalCase per MISA spec):
///   MisaAuthResponse   : Success, Data.{access_token}, ErrorCode, ErrorMessage
///   MisaInvoiceResult  : Success, Data.{InvNo, TransactionID, ReservationCode}, ErrorCode
///   MisaStatusResult   : Success, Data.{InvNo, InvStatus, ApprovedDate}, ErrorCode
///   MisaCancelResult   : Success, ErrorCode, ErrorMessage
/// </summary>
public class MisaEInvoiceProviderTests
{
    // ── shared helpers ───────────────────────────────────────────────────────

    private static MisaConfig MakeConfig() =>
        new("CTYTEST", "api-key-test", "app-id-test", "testuser", "testpass", "1C25TAA",
            BaseUrl: "https://testapi.meinvoice.vn/",
            ProductionBaseUrl: null,
            SandboxBaseUrl: null);

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
        AdditionalData:  new Dictionary<string, string>(),
        SupplierTaxCode: "0123456789",
        LineItems:       new List<InvoiceItem>(),
        CurrencyCode:    "VND",
        PaymentType:     "CASH");

    private static EInvoiceRequest MakeRequestWithLineItems() => new(
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
        AdditionalData:  new Dictionary<string, string>(),
        SupplierTaxCode: "0123456789",
        LineItems:       new List<InvoiceItem>
        {
            new(new TenantId(Guid.NewGuid()), new ElectronicInvoiceId(Guid.NewGuid()),
                "MISA-ITEM-001", "MISA Test Product", "box", 4, 50000m, 10m)
        },
        CurrencyCode:    "VND",
        PaymentType:     "CASH");

    private static MisaEInvoiceProvider MakeProvider(MockHttpMessageHandler handler)
    {
        var client  = new HttpClient(handler) { BaseAddress = new Uri("https://testapi.meinvoice.vn/") };
        var options = Options.Create(MakeConfig());
        return new MisaEInvoiceProvider(client, options, NullLogger<MisaEInvoiceProvider>.Instance);
    }

    // ── TC-M01: Submit thành công (Success=true, Data.InvNo) ──────────────────
    [Fact]
    public async Task Submit_AuthSucceeds_SuccessTrue_ReturnsSuccess()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/token", HttpMethod.Post,
            new
            {
                Success = true,
                Data = new { access_token = "misa-token" },
                ErrorCode = (string?)null
            });
        handler.AddResponse("api/integration/invoice", HttpMethod.Post,
            new
            {
                Success = true,
                Data = new { InvNo = "MS-001", TransactionID = "tx-misa-001", ReservationCode = "RES-001" },
                ErrorCode = (string?)null
            });

        var result = await MakeProvider(handler).SubmitInvoiceAsync(MakeRequest());

        result.Success.Should().BeTrue();
        result.ProviderInvoiceNumber.Should().Be("MS-001");
        result.ErrorMessage.Should().BeNull();
        result.TransactionUuid.Should().Be("tx-misa-001");
        result.ReservationCode.Should().Be("RES-001");
    }

    // ── TC-M02: Auth fail 401 → Failure ──────────────────────────────────────
    [Fact]
    public async Task Submit_AuthReturns401_ReturnsFailure()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddRawResponse("auth/token", HttpMethod.Post, HttpStatusCode.Unauthorized, "");

        var result = await MakeProvider(handler).SubmitInvoiceAsync(MakeRequest());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // ── TC-M03: Auth Success=false → Failure ─────────────────────────────────
    [Fact]
    public async Task Submit_AuthSuccessFalse_ReturnsFailure()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/token", HttpMethod.Post,
            new
            {
                Success = false,
                Data = (object?)null,
                ErrorCode = "AUTH001",
                ErrorMessage = "Invalid appid"
            });

        var result = await MakeProvider(handler).SubmitInvoiceAsync(MakeRequest());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid appid");
    }

    // ── TC-M04: Submit Success=false → Failure ───────────────────────────────
    [Fact]
    public async Task Submit_SuccessFalse_ReturnsFailure()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/token", HttpMethod.Post,
            new
            {
                Success = true,
                Data = new { access_token = "misa-token" },
                ErrorCode = (string?)null
            });
        handler.AddResponse("api/integration/invoice", HttpMethod.Post,
            new
            {
                Success = false,
                Data = (object?)null,
                ErrorCode = "INV001",
                ErrorMessage = "Duplicate invoice"
            });

        var result = await MakeProvider(handler).SubmitInvoiceAsync(MakeRequest());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Duplicate invoice");
    }

    // ── TC-M05: Submit with line items → payload includes OriginalInvoiceDetail ─
    [Fact]
    public async Task Submit_WithLineItems_PayloadIncludesOriginalInvoiceDetail()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/token", HttpMethod.Post,
            new
            {
                Success = true,
                Data = new { access_token = "misa-token" },
                ErrorCode = (string?)null
            });
        handler.AddResponse("api/integration/invoice", HttpMethod.Post,
            new
            {
                Success = true,
                Data = new { InvNo = "MS-002" },
                ErrorCode = (string?)null
            });

        var capturingHandler = new CapturingHandler(handler, "api/integration/invoice");
        var client = new HttpClient(capturingHandler) { BaseAddress = new Uri("https://testapi.meinvoice.vn/") };
        var provider = new MisaEInvoiceProvider(client, Options.Create(MakeConfig()),
            NullLogger<MisaEInvoiceProvider>.Instance);

        var result = await provider.SubmitInvoiceAsync(MakeRequestWithLineItems());

        result.Success.Should().BeTrue();
        result.ProviderInvoiceNumber.Should().Be("MS-002");

        var payload = capturingHandler.CapturedBody!;
        payload.Should().Contain("OriginalInvoiceDetail");
        payload.Should().Contain("MISA-ITEM-001");
        payload.Should().Contain("TaxRateInfo");
        payload.Should().Contain("SignType");
        payload.Should().Contain("\"SignType\":2"); // 2 = sync
    }

    // ── TC-M06: Submit → Bearer auth (NOT Cookie) ────────────────────────────
    [Fact]
    public async Task Submit_AuthUsesBearerNotCookie()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/token", HttpMethod.Post,
            new
            {
                Success = true,
                Data = new { access_token = "misa-bearer-token" },
                ErrorCode = (string?)null
            });
        handler.AddResponse("api/integration/invoice", HttpMethod.Post,
            new
            {
                Success = true,
                Data = new { InvNo = "MS-003" },
                ErrorCode = (string?)null
            });

        var capturingHandler = new CapturingHandler(handler, "api/integration/invoice");
        var client = new HttpClient(capturingHandler) { BaseAddress = new Uri("https://testapi.meinvoice.vn/") };
        var provider = new MisaEInvoiceProvider(client, Options.Create(MakeConfig()),
            NullLogger<MisaEInvoiceProvider>.Instance);

        await provider.SubmitInvoiceAsync(MakeRequest());

        var createRequest = capturingHandler.CapturedRequest;
        createRequest.Should().NotBeNull();
        createRequest!.Headers.Authorization.Should().NotBeNull();
        createRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        createRequest.Headers.Authorization!.Parameter.Should().Be("misa-bearer-token");
    }

    // ── TC-M07: GetStatus APPROVED → TaxApproved ─────────────────────────────
    [Fact]
    public async Task GetStatus_Approved_ReturnsTaxApproved()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/token", HttpMethod.Post,
            new
            {
                Success = true,
                Data = new { access_token = "misa-token" },
                ErrorCode = (string?)null
            });
        // Status endpoint: GET api/integration/invoice/{invNo} — match by "invoice/MS-001"
        handler.AddResponse("invoice/MS-001", HttpMethod.Get,
            new
            {
                Success = true,
                Data = new { InvNo = "MS-001", InvStatus = "APPROVED", ApprovedDate = "2025-06-14T00:00:00Z" },
                ErrorCode = (string?)null
            });

        var tenantId = new TenantId(Guid.NewGuid());
        var result = await MakeProvider(handler)
            .GetInvoiceStatusAsync(tenantId, "MS-001");

        result.Status.Should().Be(InvoiceStatus.TaxApproved);
        result.ApprovedAt.Should().NotBeNull();
    }

    // ── TC-M08: GetStatus REJECTED → Rejected ────────────────────────────────
    [Fact]
    public async Task GetStatus_Rejected_ReturnsRejected()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/token", HttpMethod.Post,
            new
            {
                Success = true,
                Data = new { access_token = "misa-token" },
                ErrorCode = (string?)null
            });
        handler.AddResponse("invoice/MS-001", HttpMethod.Get,
            new
            {
                Success = true,
                Data = new { InvNo = "MS-001", InvStatus = "REJECTED", ApprovedDate = (string?)null },
                ErrorCode = "REJ001",
                ErrorMessage = "Rejected by CQT"
            });

        var tenantId = new TenantId(Guid.NewGuid());
        var result = await MakeProvider(handler)
            .GetInvoiceStatusAsync(tenantId, "MS-001");

        result.Status.Should().Be(InvoiceStatus.Rejected);
        result.ApprovedAt.Should().BeNull();
        result.FailureReason.Should().Contain("Rejected by CQT");
    }

    // ── TC-M09: GetStatus PROCESSING → PendingSend ───────────────────────────
    [Fact]
    public async Task GetStatus_Processing_ReturnsPendingSend()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/token", HttpMethod.Post,
            new
            {
                Success = true,
                Data = new { access_token = "misa-token" },
                ErrorCode = (string?)null
            });
        handler.AddResponse("invoice/MS-001", HttpMethod.Get,
            new
            {
                Success = true,
                Data = new { InvNo = "MS-001", InvStatus = "PROCESSING", ApprovedDate = (string?)null },
                ErrorCode = (string?)null
            });

        var tenantId = new TenantId(Guid.NewGuid());
        var result = await MakeProvider(handler)
            .GetInvoiceStatusAsync(tenantId, "MS-001");

        result.Status.Should().Be(InvoiceStatus.PendingSend);
    }

    // ── TC-M10: Cancel Success=true → Success ────────────────────────────────
    [Fact]
    public async Task Cancel_SuccessTrue_ReturnsSuccess()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/token", HttpMethod.Post,
            new
            {
                Success = true,
                Data = new { access_token = "misa-token" },
                ErrorCode = (string?)null
            });
        handler.AddResponse("invoice/cancel", HttpMethod.Post,
            new { Success = true, ErrorCode = (string?)null, ErrorMessage = (string?)null });

        var tenantId = new TenantId(Guid.NewGuid());
        var result = await MakeProvider(handler)
            .CancelInvoiceAsync(tenantId, "MS-001", "Sai thong tin");

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    // ── TC-M11: Cancel Success=false → Failure ───────────────────────────────
    [Fact]
    public async Task Cancel_SuccessFalse_ReturnsFailure()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/token", HttpMethod.Post,
            new
            {
                Success = true,
                Data = new { access_token = "misa-token" },
                ErrorCode = (string?)null
            });
        handler.AddResponse("invoice/cancel", HttpMethod.Post,
            new { Success = false, ErrorCode = "CAN001", ErrorMessage = "Already cancelled" });

        var tenantId = new TenantId(Guid.NewGuid());
        var result = await MakeProvider(handler)
            .CancelInvoiceAsync(tenantId, "MS-001", "Sai thong tin");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Already cancelled");
    }

    // ── TC-M12: Cancel 500 → Failure ─────────────────────────────────────────
    [Fact]
    public async Task Cancel_500_ReturnsFailure()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/token", HttpMethod.Post,
            new
            {
                Success = true,
                Data = new { access_token = "misa-token" },
                ErrorCode = (string?)null
            });
        handler.AddRawResponse("invoice/cancel", HttpMethod.Post,
            HttpStatusCode.InternalServerError, "{}");

        var tenantId = new TenantId(Guid.NewGuid());
        var result = await MakeProvider(handler)
            .CancelInvoiceAsync(tenantId, "MS-001", "Sai thong tin");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // ── TC-M13: GetInvoiceFile returns byte[] ────────────────────────────────
    [Fact]
    public async Task GetInvoiceFile_ReturnsByteArray()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("auth/token", HttpMethod.Post,
            new
            {
                Success = true,
                Data = new { access_token = "misa-token" },
                ErrorCode = (string?)null
            });
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
        handler.AddRawResponse("/file", HttpMethod.Get, HttpStatusCode.OK,
            System.Text.Encoding.UTF8.GetString(pdfBytes));

        var tenantId = new TenantId(Guid.NewGuid());
        var result = await MakeProvider(handler)
            .GetInvoiceFileAsync(tenantId, "MS-001", "pdf");

        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    // ── TC-M14: HealthCheck 200 → true ───────────────────────────────────────
    [Fact]
    public async Task HealthCheck_200_ReturnsTrue()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddRawResponse("health", HttpMethod.Get, HttpStatusCode.OK, "{}");

        var result = await MakeProvider(handler).HealthCheckAsync();

        result.Should().BeTrue();
    }

    // ── TC-M15: HealthCheck 503 → false ──────────────────────────────────────
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
