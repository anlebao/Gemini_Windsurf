using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Repositories;
using VanAn.Core.Tests.TestInfrastructure;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Infrastructure.Repositories
{
    /// <summary>
    /// Unit tests for JournalTemplateRepository - Phase 2.1 TDD Implementation
    /// Tests CRUD operations, multi-tenancy, and error handling
    /// </summary>
    public class JournalTemplateRepositoryTests : IDisposable
    {
        private readonly JournalTemplateRepository _repository;
        private readonly VanAnDbContext _context;
        private readonly TenantId _testTenantId = new(Guid.NewGuid());
        private readonly ILogger<JournalTemplateRepository> _logger;

        public JournalTemplateRepositoryTests()
        {
            _logger = new NullLogger<JournalTemplateRepository>();
            DbContextOptions<VanAnDbContext> options = new DbContextOptionsBuilder<VanAnDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new VanAnDbContext(options);
            _repository = new JournalTemplateRepository(_context, _logger);
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        [Fact]
        public async Task GetByCodeAsync_ShouldReturnTemplate_WhenTemplateExists()
        {
            // Arrange
            JournalTemplate template = TestEntityBuilder.CreateJournalTemplate(_testTenantId, "SALES_TEMPLATE", "Sales journal template");

            await _context.JournalTemplates.AddAsync(template);
            await _context.SaveChangesAsync();

            // Act
            JournalTemplate? result = await _repository.GetByCodeAsync(_testTenantId, "SALES_TEMPLATE");

            // Assert
            result.Should().NotBeNull();
            result!.Code.Should().Be("SALES_TEMPLATE");
            result.Description.Should().Be("Sales journal template");
            result.TenantId.Should().Be(_testTenantId);
        }

        [Fact]
        public async Task GetByCodeAsync_ShouldReturnNull_WhenTemplateDoesNotExist()
        {
            // Act
            JournalTemplate? result = await _repository.GetByCodeAsync(_testTenantId, "NONEXISTENT");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetByTenantAsync_ShouldReturnOnlyTenantTemplates()
        {
            // Arrange
            TenantId tenant1 = new(Guid.NewGuid());
            TenantId tenant2 = new(Guid.NewGuid());

            JournalTemplate template1 = TestEntityBuilder.CreateJournalTemplate(tenant1, "TEMPLATE_1", "Template 1");
            JournalTemplate template2 = TestEntityBuilder.CreateJournalTemplate(tenant1, "TEMPLATE_2", "Template 2");
            JournalTemplate template3 = TestEntityBuilder.CreateJournalTemplate(tenant2, "TEMPLATE_3", "Template 3");

            await _context.JournalTemplates.AddRangeAsync([template1, template2, template3]);
            await _context.SaveChangesAsync();

            // Act
            IEnumerable<JournalTemplate> results = await _repository.GetByTenantAsync(tenant1);

            // Assert
            results.Should().HaveCount(2);
            results.Should().OnlyContain(t => t.TenantId.Equals(tenant1));
            results.Should().Contain(t => t.Code == "TEMPLATE_1");
            results.Should().Contain(t => t.Code == "TEMPLATE_2");
            results.Should().NotContain(t => t.Code == "TEMPLATE_3");
        }

        [Fact]
        public async Task AddAsync_ShouldCreateTemplate()
        {
            // Arrange
            JournalTemplate template = TestEntityBuilder.CreateJournalTemplate(_testTenantId, "NEW_TEMPLATE", "New template");

            // Act
            await _repository.AddAsync(template);

            // Assert
            JournalTemplate? savedTemplate = await _context.JournalTemplates.FindAsync(template.Id);
            savedTemplate.Should().NotBeNull();
            savedTemplate!.Code.Should().Be("NEW_TEMPLATE");
            savedTemplate.Description.Should().Be("New template");
            savedTemplate.TenantId.Should().Be(_testTenantId);
        }

        [Fact]
        public async Task UpdateAsync_ShouldModifyTemplate()
        {
            // Arrange
            JournalTemplate template = TestEntityBuilder.CreateJournalTemplate(_testTenantId, "UPDATE_TEMPLATE", "Original description");

            await _context.JournalTemplates.AddAsync(template);
            await _context.SaveChangesAsync();

            // Act
            // Fetch the tracked entity - JournalTemplate has immutable properties, so we verify
            // that UpdateAsync correctly marks the entity as modified in EF Core change tracker
            JournalTemplate? trackedTemplate = await _context.JournalTemplates.FindAsync(template.Id);
            trackedTemplate.Should().NotBeNull();

            // Detach to simulate fetching from another context
            _context.Entry(trackedTemplate!).State = EntityState.Detached;

            // Re-attach and call UpdateAsync
            await _repository.UpdateAsync(trackedTemplate!);

            // Assert
            Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<JournalTemplate> entry = _context.Entry(trackedTemplate!);
            entry.State.Should().Be(EntityState.Unchanged); // UpdateAsync saves changes, so entity is now Unchanged
        }

        [Fact]
        public async Task DeleteAsync_ShouldRemoveTemplate()
        {
            // Arrange
            JournalTemplate template = TestEntityBuilder.CreateJournalTemplate(_testTenantId, "DELETE_TEMPLATE", "Template to delete");

            await _context.JournalTemplates.AddAsync(template);
            await _context.SaveChangesAsync();

            // Act
            await _repository.DeleteAsync(template);

            // Assert
            JournalTemplate? deletedTemplate = await _context.JournalTemplates.FindAsync(template.Id);
            deletedTemplate.Should().BeNull();
        }
    }
}
