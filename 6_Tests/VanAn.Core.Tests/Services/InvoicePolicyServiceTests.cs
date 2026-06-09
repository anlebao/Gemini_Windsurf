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
}
