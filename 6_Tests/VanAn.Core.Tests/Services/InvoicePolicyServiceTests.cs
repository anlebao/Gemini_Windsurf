using VanAn.CoreHub.Services.Orchestration;
using VanAn.Shared.Domain;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Services;

public class InvoicePolicyServiceTests
{
    private readonly InvoicePolicyService _sut = new();
    private readonly ElectronicInvoiceId _invoiceId = new(Guid.NewGuid());

    [Fact]
    public async Task ValidateInvoiceAsync_WithoutDbContext_ThrowsInvalidOperationException()
    {
        var act = async () => await _sut.ValidateInvoiceAsync(_invoiceId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires a database context*");
    }

    [Fact]
    public async Task CanSubmitAsync_WithoutDbContext_ReturnsFalse()
    {
        var result = await _sut.CanSubmitAsync(_invoiceId, CancellationToken.None);

        result.Should().BeFalse();
    }

    // T01
    [Fact]
    public void DetermineRecipientType_ValidMst10Digits_ReturnsB2B()
    {
        var result = _sut.DetermineRecipientType("0301234567", null);

        result.Should().Be(InvoiceRecipientType.B2B);
    }

    // T02
    [Fact]
    public void DetermineRecipientType_NullMstWithPhone_ReturnsRetailMember()
    {
        var result = _sut.DetermineRecipientType(null, "0912345678");

        result.Should().Be(InvoiceRecipientType.RetailMember);
    }

    // T03
    [Fact]
    public void DetermineRecipientType_NullMstNullPhone_ReturnsRetailAnonymous()
    {
        var result = _sut.DetermineRecipientType(null, null);

        result.Should().Be(InvoiceRecipientType.RetailAnonymous);
    }

    // T04
    [Fact]
    public void DetermineRecipientType_EmptyMstEmptyPhone_ReturnsRetailAnonymous()
    {
        var result = _sut.DetermineRecipientType("", "");

        result.Should().Be(InvoiceRecipientType.RetailAnonymous);
    }

    // T05
    [Fact]
    public void IsEInvoiceRequired_HKDBelow1Billion_ReturnsFalse()
    {
        var result = _sut.IsEInvoiceRequired(BusinessType.HouseholdBusiness, 500_000_000m);

        result.Should().BeFalse();
    }

    // T06
    [Fact]
    public void IsEInvoiceRequired_HKDAbove1Billion_ReturnsTrue()
    {
        var result = _sut.IsEInvoiceRequired(BusinessType.HouseholdBusiness, 1_500_000_000m);

        result.Should().BeTrue();
    }

    // T07
    [Fact]
    public void IsEInvoiceRequired_CompanyAnyRevenue_ReturnsTrue()
    {
        var result = _sut.IsEInvoiceRequired(BusinessType.Company, 200_000_000m);

        result.Should().BeTrue();
    }

    // T08 — RetailAnonymous with empty TaxCode must NOT throw
    [Fact]
    public void ValidateBusinessPolicy_RetailAnonymousEmptyTaxCode_DoesNotThrow()
    {
        var tenantId = new TenantId(Guid.NewGuid());
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey,
            InvoiceType.Goods,
            amount: 10_000m,
            vatAmount: 1_000m,
            totalAmount: 11_000m,
            customerName: "Khách lẻ",
            customerTaxCode: "",
            customerAddress: "",
            recipientType: InvoiceRecipientType.RetailAnonymous);

        var act = () => InvoicePolicyServiceTestHelper.InvokeValidateBusinessPolicy(invoice);

        act.Should().NotThrow();
    }

    // T09 — B2B with empty TaxCode must throw
    [Fact]
    public void ValidateBusinessPolicy_B2BEmptyTaxCode_ThrowsInvalidOperationException()
    {
        var tenantId = new TenantId(Guid.NewGuid());
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = new InvoiceIdempotencyKey(Guid.NewGuid().ToString());
        var invoice = new ElectronicInvoice(
            tenantId, orderId, idempotencyKey,
            InvoiceType.Goods,
            amount: 10_000m,
            vatAmount: 1_000m,
            totalAmount: 11_000m,
            customerName: "Công ty ABC",
            customerTaxCode: "",
            customerAddress: "Hà Nội",
            recipientType: InvoiceRecipientType.B2B);

        var act = () => InvoicePolicyServiceTestHelper.InvokeValidateBusinessPolicy(invoice);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CustomerTaxCode is required for B2B*");
    }
}

/// <summary>
/// Test helper to expose private static ValidateBusinessPolicy via reflection for T08/T09
/// </summary>
internal static class InvoicePolicyServiceTestHelper
{
    public static void InvokeValidateBusinessPolicy(ElectronicInvoice invoice)
    {
        var method = typeof(InvoicePolicyService)
            .GetMethod("ValidateBusinessPolicy",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException("ValidateBusinessPolicy method not found");

        try
        {
            method.Invoke(null, [invoice]);
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }
}
