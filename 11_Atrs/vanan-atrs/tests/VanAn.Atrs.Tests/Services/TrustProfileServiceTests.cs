using Microsoft.EntityFrameworkCore;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;
using Xunit;

namespace VanAn.Atrs.Tests.Services;

public sealed class TrustProfileServiceTests : IDisposable
{
    private readonly AtrsDbContext _db;
    private readonly TrustProfileService _service;

    public TrustProfileServiceTests()
    {
        var options = new DbContextOptionsBuilder<AtrsDbContext>()
            .UseInMemoryDatabase($"tps-{Guid.NewGuid()}")
            .Options;
        _db = new AtrsDbContext(options);
        _service = new TrustProfileService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetOrCreate_FirstCall_CreatesSubjectLinkProfile()
    {
        var customerId = Guid.NewGuid();
        var subjectId = await _service.GetOrCreateForCustomerAsync(customerId);

        Assert.NotEqual(Guid.Empty, subjectId);
        Assert.Equal(1, await _db.TrustSubjects.CountAsync());
        Assert.Equal(1, await _db.TrustSubjectLinks.CountAsync());
        var profile = await _db.AccountTrustProfiles.SingleAsync();
        Assert.Equal(TrustProfileStatus.PENDING_SETUP, profile.Status);
        Assert.Equal(AssuranceState.BASIC, profile.AssuranceState);
    }

    [Fact]
    public async Task GetOrCreate_SecondCall_SameSubject()
    {
        var customerId = Guid.NewGuid();
        var first = await _service.GetOrCreateForCustomerAsync(customerId);
        var second = await _service.GetOrCreateForCustomerAsync(customerId);

        Assert.Equal(first, second);
        Assert.Equal(1, await _db.TrustSubjects.CountAsync());
    }

    [Fact]
    public async Task TryResolveByLink_Unknown_ReturnsNull()
    {
        var subjectId = await _service.TryResolveByLinkAsync(TrustIdentityType.Customer, Guid.NewGuid());
        Assert.Null(subjectId);
    }
}