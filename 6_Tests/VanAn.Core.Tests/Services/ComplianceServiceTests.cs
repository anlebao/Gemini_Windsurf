using VanAn.CoreHub.Services.Orchestration;
using VanAn.Shared.Domain;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Services;

public class ComplianceServiceTests
{
    private readonly ComplianceService _sut = new();
    private readonly ElectronicInvoiceId _invoiceId = new(Guid.NewGuid());

    [Fact]
    public async Task ValidateComplianceAsync_WithoutDbContext_ThrowsInvalidOperationException()
    {
        var act = async () => await _sut.ValidateComplianceAsync(_invoiceId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires a database context*");
    }

    [Fact]
    public async Task IsCompliantAsync_WithoutDbContext_ReturnsFalse()
    {
        var result = await _sut.IsCompliantAsync(_invoiceId, CancellationToken.None);

        result.Should().BeFalse();
    }
}
