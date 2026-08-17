using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;
using Xunit;

namespace VanAn.Core.Tests.KhachLink
{
    /// <summary>
    /// KhachLink Multi-Profile R1 Sprint 6: Service integration tests for KhachLinkInstanceService.
    /// Uses in-memory DbContext (VanAnDbContext) to test CRUD + by-domain lookup + unique domain validation.
    /// </summary>
    public class KhachLinkInstanceServiceTests : IDisposable
    {
        private readonly VanAnDbContext _context;
        private readonly KhachLinkInstanceService _service;

        public KhachLinkInstanceServiceTests()
        {
            var options = new DbContextOptionsBuilder<VanAnDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new VanAnDbContext(options);
            _service = new KhachLinkInstanceService(_context);
        }

        public void Dispose() => _context.Dispose();

        [Fact]
        public async Task CreateAsync_PersistsInstance_WithCorrectFields()
        {
            var instance = await _service.CreateAsync("Test Instance", KhachLinkProfile.Directory, "test.khachvip.online");

            Assert.NotEqual(Guid.Empty, instance.Id);
            Assert.Equal("Test Instance", instance.Label);
            Assert.Equal(KhachLinkProfile.Directory, instance.Profile);
            Assert.Equal("test.khachvip.online", instance.CustomDomain);
            Assert.True(instance.IsActive);

            // Verify persisted
            var fromDb = await _context.KhachLinkInstances.FindAsync(instance.Id);
            Assert.NotNull(fromDb);
            Assert.Equal("Test Instance", fromDb!.Label);
        }

        [Fact]
        public async Task CreateAsync_NormalizesCustomDomain_ToLowercase()
        {
            var instance = await _service.CreateAsync("Test", KhachLinkProfile.FullCommerce, "TEST.KhachVip.Online");

            Assert.Equal("test.khachvip.online", instance.CustomDomain);
        }

        [Fact]
        public async Task CreateAsync_WithDuplicateDomain_ThrowsInvalidOperationException()
        {
            await _service.CreateAsync("First", KhachLinkProfile.FullCommerce, "dup.khachvip.online");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateAsync("Second", KhachLinkProfile.Directory, "DUP.khachvip.online"));
        }

        [Fact]
        public async Task CreateAsync_WithEmptyLabel_ThrowsArgumentException()
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.CreateAsync("", KhachLinkProfile.FullCommerce, "test.khachvip.online"));
        }

        [Fact]
        public async Task CreateAsync_WithEmptyDomain_ThrowsArgumentException()
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.CreateAsync("Test", KhachLinkProfile.FullCommerce, ""));
        }

        [Fact]
        public async Task CreateAsync_WithNavFlagsOverride_UsesOverride()
        {
            var customFlags = new KhachLinkNavFlags { ShowCart = true, ShowHome = false };

            var instance = await _service.CreateAsync("Test", KhachLinkProfile.Directory, "test.khachvip.online", null, customFlags);

            Assert.True(instance.NavFlags.ShowCart);  // override (Directory preset = false)
            Assert.False(instance.NavFlags.ShowHome); // override (Directory preset = true)
        }

        [Fact]
        public async Task CreateAsync_WithNullNavFlags_UsesProfilePreset()
        {
            var instance = await _service.CreateAsync("Test", KhachLinkProfile.Directory, "test.khachvip.online", null, null);

            Assert.True(instance.NavFlags.ShowHome);   // Directory preset
            Assert.False(instance.NavFlags.ShowCart);  // Directory preset
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsInstance_WhenExists()
        {
            var created = await _service.CreateAsync("Test", KhachLinkProfile.FullCommerce, "test.khachvip.online");

            var found = await _service.GetByIdAsync(created.Id);

            Assert.NotNull(found);
            Assert.Equal(created.Id, found!.Id);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
        {
            var found = await _service.GetByIdAsync(Guid.NewGuid());

            Assert.Null(found);
        }

        [Fact]
        public async Task GetByDomainAsync_ReturnsInstance_WhenDomainMatches()
        {
            await _service.CreateAsync("Test", KhachLinkProfile.FullCommerce, "find.khachvip.online");

            var found = await _service.GetByDomainAsync("find.khachvip.online");

            Assert.NotNull(found);
            Assert.Equal("find.khachvip.online", found!.CustomDomain);
        }

        [Fact]
        public async Task GetByDomainAsync_IsCaseInsensitive()
        {
            await _service.CreateAsync("Test", KhachLinkProfile.FullCommerce, "case.khachvip.online");

            var found = await _service.GetByDomainAsync("CASE.KhachVip.Online");

            Assert.NotNull(found);
        }

        [Fact]
        public async Task GetByDomainAsync_ReturnsNull_WhenDomainNotFound()
        {
            var found = await _service.GetByDomainAsync("nonexistent.khachvip.online");

            Assert.Null(found);
        }

        [Fact]
        public async Task GetByDomainAsync_ReturnsInstance_WhenInstanceDeactivated()
        {
            // #134-fix: GetByDomainAsync now returns the instance regardless of IsActive
            // so KhachLink runtime can show a "disabled" page instead of falling back
            // to FullCommerce defaults (which made disabled instances still work).
            var created = await _service.CreateAsync("Test", KhachLinkProfile.FullCommerce, "inactive.khachvip.online");
            await _service.DeactivateAsync(created.Id);

            var found = await _service.GetByDomainAsync("inactive.khachvip.online");

            Assert.NotNull(found);
            Assert.False(found.IsActive);
        }

        [Fact]
        public async Task GetByDomainAsync_WithEmptyDomain_ReturnsNull()
        {
            var found = await _service.GetByDomainAsync("");

            Assert.Null(found);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsAllInstances()
        {
            await _service.CreateAsync("A", KhachLinkProfile.FullCommerce, "a.khachvip.online");
            await _service.CreateAsync("B", KhachLinkProfile.Directory, "b.khachvip.online");
            await _service.CreateAsync("C", KhachLinkProfile.FullCommerce, "c.khachvip.online");

            var all = await _service.GetAllAsync();

            Assert.Equal(3, all.Count);
        }

        [Fact]
        public async Task UpdateAsync_PersistsProfileAndNavFlags()
        {
            var created = await _service.CreateAsync("Test", KhachLinkProfile.FullCommerce, "update.khachvip.online");
            var newFlags = new KhachLinkNavFlags { ShowCart = false, ShowHome = true };

            var result = await _service.UpdateAsync(created.Id, KhachLinkProfile.Directory, newFlags);

            Assert.True(result);
            var fromDb = await _context.KhachLinkInstances.FindAsync(created.Id);
            Assert.Equal(KhachLinkProfile.Directory, fromDb!.Profile);
            Assert.False(fromDb.NavFlags.ShowCart);
        }

        [Fact]
        public async Task UpdateAsync_ReturnsFalse_WhenInstanceNotFound()
        {
            var result = await _service.UpdateAsync(Guid.NewGuid(), KhachLinkProfile.Directory, new KhachLinkNavFlags());

            Assert.False(result);
        }

        [Fact]
        public async Task UpdateAsync_WithNullNavFlags_ThrowsArgumentNullException()
        {
            var created = await _service.CreateAsync("Test", KhachLinkProfile.FullCommerce, "test.khachvip.online");

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _service.UpdateAsync(created.Id, KhachLinkProfile.Directory, null!));
        }

        [Fact]
        public async Task DeactivateAsync_SetsIsActiveFalse()
        {
            var created = await _service.CreateAsync("Test", KhachLinkProfile.FullCommerce, "deactivate.khachvip.online");

            var result = await _service.DeactivateAsync(created.Id);

            Assert.True(result);
            var fromDb = await _context.KhachLinkInstances.FindAsync(created.Id);
            Assert.False(fromDb!.IsActive);
        }

        [Fact]
        public async Task DeactivateAsync_ReturnsFalse_WhenInstanceNotFound()
        {
            var result = await _service.DeactivateAsync(Guid.NewGuid());

            Assert.False(result);
        }
    }
}
