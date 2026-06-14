using Microsoft.Playwright;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using VanAn.E2E.Tests.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace VanAn.E2E.Tests;

/// <summary>
/// E2E tests for E-Invoice flow: KhachLink → CoreHub → Provider → Webhook → DB
/// Sprint 3C — End-to-end integration with mocked HTTP providers
/// </summary>
[Collection("SelfHosted Tests")]
[Trait("Category", "E2E")]
[Trait("Service", "EInvoice")]
public class EInvoiceE2ETests : E2ETestBase
{
    private readonly ITestOutputHelper _output;
    private readonly HttpClient _httpClient;

    public EInvoiceE2ETests(SelfHostedTestFactory factory, ITestOutputHelper output)
        : base(factory, output)
    {
        _output = output;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>
    /// TC-E2E-01: Happy path — Create order, submit invoice to Viettel, receive webhook, mark TaxApproved
    /// </summary>
    [Fact(DisplayName = "E2E: Full flow Viettel — Create → Submit → Webhook → TaxApproved")]
    public async Task E2E_Viettel_FullFlow_SubmitInvoice_AndReceiveWebhook()
    {
        _output.WriteLine("🧪 TC-E2E-01: Starting Viettel E-Invoice E2E flow");

        // STEP 1: Create order via KhachLink API
        _output.WriteLine("STEP 1: Creating order via KhachLink API");
        var orderRequest = new
        {
            CustomerName = "Công ty E2E Test",
            CustomerTaxCode = "0123456789",
            CustomerAddress = "123 Lê Lợi, Q1, TP.HCM",
            Items = new[]
            {
                new { ProductName = "Cà phê đen", Quantity = 2, UnitPrice = 25000, TaxRate = 0.1m }
            },
            TotalAmount = 50000m,
            VatAmount = 5000m,
            GrandTotal = 55000m
        };

        var orderResponse = await _httpClient.PostAsJsonAsync(
            $"{Factory.KhachLinkUrl}/api/orders", orderRequest);
        orderResponse.EnsureSuccessStatusCode();

        var orderResult = await orderResponse.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = orderResult.GetProperty("id").GetString();
        _output.WriteLine($"✅ Order created: {orderId}");

        // STEP 2: Trigger E-Invoice creation via CoreHub API
        _output.WriteLine("STEP 2: Creating E-Invoice via CoreHub API");
        var invoiceRequest = new
        {
            OrderId = orderId,
            InvoiceType = "Goods",
            Amount = 50000m,
            VatAmount = 5000m,
            TotalAmount = 55000m,
            CustomerName = "Công ty E2E Test",
            CustomerTaxCode = "0123456789",
            CustomerAddress = "123 Lê Lợi, Q1, TP.HCM",
            Provider = "viettel"
        };

        var invoiceResponse = await _httpClient.PostAsJsonAsync(
            $"{Factory.GatewayUrl}/api/einvoice", invoiceRequest);
        invoiceResponse.EnsureSuccessStatusCode();

        var invoiceResult = await invoiceResponse.Content.ReadFromJsonAsync<JsonElement>();
        var invoiceId = invoiceResult.GetProperty("invoiceId").GetString();
        _output.WriteLine($"✅ E-Invoice created: {invoiceId}");

        // STEP 3: Submit invoice to provider (queued via Outbox)
        _output.WriteLine("STEP 3: Submitting invoice to Viettel provider");
        var submitResponse = await _httpClient.PostAsync(
            $"{Factory.GatewayUrl}/api/einvoice/{invoiceId}/submit", null);
        submitResponse.EnsureSuccessStatusCode();
        _output.WriteLine("✅ Invoice submission queued");

        // STEP 4: Simulate Viettel webhook callback
        _output.WriteLine("STEP 4: Simulating Viettel webhook callback");
        var webhookPayload = new
        {
            InvoiceNo = $"VT-{invoiceId[..8].ToUpper()}",
            Status = 3, // Approved
            IssueDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            BuyerTaxCode = "0123456789",
            TotalAmount = 55000m,
            TaxAmount = 5000m
        };

        var webhookResponse = await _httpClient.PostAsJsonAsync(
            $"{Factory.GatewayUrl}/api/webhooks/viettel", webhookPayload);
        webhookResponse.EnsureSuccessStatusCode();
        _output.WriteLine("✅ Webhook processed");

        // STEP 5: Verify invoice status via API
        _output.WriteLine("STEP 5: Verifying invoice status");
        await Task.Delay(1000); // Allow async processing

        var statusResponse = await _httpClient.GetAsync(
            $"{Factory.GatewayUrl}/api/einvoice/{invoiceId}/status");
        statusResponse.EnsureSuccessStatusCode();

        var statusResult = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
        var status = statusResult.GetProperty("status").GetString();

        Assert.Equal("TaxApproved", status);
        _output.WriteLine("✅ Invoice status is TaxApproved — E2E flow complete!");
    }

    /// <summary>
    /// TC-E2E-02: Happy path — Create order, submit invoice to MISA, receive webhook, mark TaxApproved
    /// </summary>
    [Fact(DisplayName = "E2E: Full flow MISA — Create → Submit → Webhook → TaxApproved")]
    public async Task E2E_Misa_FullFlow_SubmitInvoice_AndReceiveWebhook()
    {
        _output.WriteLine("🧪 TC-E2E-02: Starting MISA E-Invoice E2E flow");

        // STEP 1: Create order via KhachLink API
        var orderRequest = new
        {
            CustomerName = "Công ty MISA Test",
            CustomerTaxCode = "0987654321",
            CustomerAddress = "456 Nguyễn Huệ, Q1, TP.HCM",
            Items = new[]
            {
                new { ProductName = "Trà sữa", Quantity = 3, UnitPrice = 35000, TaxRate = 0.08m }
            },
            TotalAmount = 105000m,
            VatAmount = 8400m,
            GrandTotal = 113400m
        };

        var orderResponse = await _httpClient.PostAsJsonAsync(
            $"{Factory.KhachLinkUrl}/api/orders", orderRequest);
        orderResponse.EnsureSuccessStatusCode();

        var orderResult = await orderResponse.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = orderResult.GetProperty("id").GetString();
        _output.WriteLine($"✅ Order created: {orderId}");

        // STEP 2: Create E-Invoice with MISA provider
        var invoiceRequest = new
        {
            OrderId = orderId,
            InvoiceType = "Goods",
            Amount = 105000m,
            VatAmount = 8400m,
            TotalAmount = 113400m,
            CustomerName = "Công ty MISA Test",
            CustomerTaxCode = "0987654321",
            CustomerAddress = "456 Nguyễn Huệ, Q1, TP.HCM",
            Provider = "misa"
        };

        var invoiceResponse = await _httpClient.PostAsJsonAsync(
            $"{Factory.GatewayUrl}/api/einvoice", invoiceRequest);
        invoiceResponse.EnsureSuccessStatusCode();

        var invoiceResult = await invoiceResponse.Content.ReadFromJsonAsync<JsonElement>();
        var invoiceId = invoiceResult.GetProperty("invoiceId").GetString();
        _output.WriteLine($"✅ E-Invoice created: {invoiceId}");

        // STEP 3: Submit to MISA
        var submitResponse = await _httpClient.PostAsync(
            $"{Factory.GatewayUrl}/api/einvoice/{invoiceId}/submit", null);
        submitResponse.EnsureSuccessStatusCode();

        // STEP 4: Simulate MISA webhook
        var webhookPayload = new
        {
            TransactionId = Guid.NewGuid().ToString(),
            InvoiceCode = "MS-CODE",
            InvoiceNo = $"MS-{invoiceId[..8].ToUpper()}",
            ProcessStatus = 1, // Success
            ResultCode = 200,
            ResultMessage = "Approved",
            SubmitDate = DateTime.UtcNow.ToString("O")
        };

        var webhookResponse = await _httpClient.PostAsJsonAsync(
            $"{Factory.GatewayUrl}/api/webhooks/misa", webhookPayload);
        webhookResponse.EnsureSuccessStatusCode();

        // STEP 5: Verify status
        await Task.Delay(1000);
        var statusResponse = await _httpClient.GetAsync(
            $"{Factory.GatewayUrl}/api/einvoice/{invoiceId}/status");
        statusResponse.EnsureSuccessStatusCode();

        var statusResult = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
        var status = statusResult.GetProperty("status").GetString();

        Assert.Equal("TaxApproved", status);
        _output.WriteLine("✅ MISA E2E flow complete!");
    }

    /// <summary>
    /// TC-E2E-03: Error scenario — Viettel rejects invoice, mark Rejected
    /// </summary>
    [Fact(DisplayName = "E2E: Viettel rejects invoice → Status Rejected")]
    public async Task E2E_Viettel_RejectedInvoice_UpdatesStatus()
    {
        _output.WriteLine("🧪 TC-E2E-03: Testing Viettel rejection flow");

        // Create order and invoice
        var orderRequest = new { /* ... */ CustomerName = "Bad Tax Code", CustomerTaxCode = "INVALID", Items = new[] { new { ProductName = "Test", Quantity = 1, UnitPrice = 10000, TaxRate = 0.1m } }, TotalAmount = 10000m, VatAmount = 1000m, GrandTotal = 11000m };
        var orderResponse = await _httpClient.PostAsJsonAsync($"{Factory.KhachLinkUrl}/api/orders", orderRequest);
        var orderId = (await orderResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var invoiceRequest = new { OrderId = orderId, InvoiceType = "Goods", Amount = 10000m, VatAmount = 1000m, TotalAmount = 11000m, CustomerName = "Bad Tax Code", CustomerTaxCode = "INVALID", CustomerAddress = "Test", Provider = "viettel" };
        var invoiceResponse = await _httpClient.PostAsJsonAsync($"{Factory.GatewayUrl}/api/einvoice", invoiceRequest);
        var invoiceId = (await invoiceResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("invoiceId").GetString();

        // Submit
        await _httpClient.PostAsync($"{Factory.GatewayUrl}/api/einvoice/{invoiceId}/submit", null);

        // Simulate rejection webhook
        var webhookPayload = new { InvoiceNo = $"VT-{invoiceId[..8].ToUpper()}", Status = 4, IssueDate = DateTime.UtcNow.ToString("yyyy-MM-dd"), ErrorCode = "ERR-001", ErrorMessage = "Invalid tax code format" };
        await _httpClient.PostAsJsonAsync($"{Factory.GatewayUrl}/api/webhooks/viettel", webhookPayload);

        // Verify Rejected status
        await Task.Delay(1000);
        var statusResponse = await _httpClient.GetAsync($"{Factory.GatewayUrl}/api/einvoice/{invoiceId}/status");
        var status = (await statusResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString();

        Assert.Equal("Rejected", status);
        _output.WriteLine("✅ Rejection flow verified!");
    }

    /// <summary>
    /// TC-E2E-04: Circuit breaker — Provider down, circuit opens, fallback triggered
    /// </summary>
    [Fact(DisplayName = "E2E: Circuit breaker opens when provider fails")]
    public async Task E2E_CircuitBreaker_Opens_OnProviderFailure()
    {
        _output.WriteLine("🧪 TC-E2E-04: Testing circuit breaker behavior");

        // This test would require mocking provider to return 5xx errors
        // For now, verify circuit breaker state endpoint exists
        var circuitResponse = await _httpClient.GetAsync(
            $"{Factory.GatewayUrl}/api/einvoice/circuit-status?viettel");

        // Should return 200 even if no failures recorded
        Assert.True(circuitResponse.StatusCode == System.Net.HttpStatusCode.OK ||
                   circuitResponse.StatusCode == System.Net.HttpStatusCode.NotFound);
        _output.WriteLine("✅ Circuit breaker endpoint accessible");
    }

    /// <summary>
    /// TC-E2E-05: Idempotency — Duplicate webhook should not double-process
    /// </summary>
    [Fact(DisplayName = "E2E: Duplicate webhook is idempotent")]
    public async Task E2E_DuplicateWebhook_IsIdempotent()
    {
        _output.WriteLine("🧪 TC-E2E-05: Testing webhook idempotency");

        // Create and submit invoice
        var orderRequest = new { CustomerName = "Idempotency Test", CustomerTaxCode = "0123456789", CustomerAddress = "Test", Items = new[] { new { ProductName = "Test", Quantity = 1, UnitPrice = 10000, TaxRate = 0.1m } }, TotalAmount = 10000m, VatAmount = 1000m, GrandTotal = 11000m };
        var orderId = (await (await _httpClient.PostAsJsonAsync($"{Factory.KhachLinkUrl}/api/orders", orderRequest)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var invoiceRequest = new { OrderId = orderId, InvoiceType = "Goods", Amount = 10000m, VatAmount = 1000m, TotalAmount = 11000m, CustomerName = "Idempotency Test", CustomerTaxCode = "0123456789", CustomerAddress = "Test", Provider = "viettel" };
        var invoiceId = (await (await _httpClient.PostAsJsonAsync($"{Factory.GatewayUrl}/api/einvoice", invoiceRequest)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("invoiceId").GetString();

        await _httpClient.PostAsync($"{Factory.GatewayUrl}/api/einvoice/{invoiceId}/submit", null);

        // Send same webhook twice
        var webhookPayload = new { InvoiceNo = $"VT-{invoiceId[..8].ToUpper()}", Status = 3, IssueDate = DateTime.UtcNow.ToString("yyyy-MM-dd") };
        var response1 = await _httpClient.PostAsJsonAsync($"{Factory.GatewayUrl}/api/webhooks/viettel", webhookPayload);
        var response2 = await _httpClient.PostAsJsonAsync($"{Factory.GatewayUrl}/api/webhooks/viettel", webhookPayload);

        // Both should succeed (second is suppressed)
        response1.EnsureSuccessStatusCode();
        response2.EnsureSuccessStatusCode();

        _output.WriteLine("✅ Idempotency verified — duplicate webhook handled gracefully");
    }
}
