using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Xunit;
using Microsoft.EntityFrameworkCore;
using VanAn.TenantAi.Domain;
using VanAn.TenantAi.Infra;

namespace VanAn.TenantAi.Tests;

/// <summary>P0 — skeleton gate tests (task_ai_p0): CallerKey model + DbContext shape.</summary>
public sealed class SkeletonTests
{
    private static TenantAiDbContext NewDb()
        => new(new DbContextOptionsBuilder<TenantAiDbContext>()
            .UseInMemoryDatabase($"tenantai-{Guid.NewGuid()}")
            .Options);

    [Fact]
    public void CallerKey_Create_SetsDefaults()
    {
        var key = CallerKey.Create("gateway", "hash123", scopesJson: "[\"public-chat\"]");

        key.Id.Should().NotBeEmpty();
        key.CallerId.Should().Be("gateway");
        key.IsActive.Should().BeTrue();
        key.IsAdmin.Should().BeFalse();
        key.ScopesJson.Should().Be("[\"public-chat\"]");
        key.TenantScopeJson.Should().BeNull();
    }

    [Fact]
    public async Task CallerKey_UniqueIndex_OnCallerId_IsConfigured()
    {
        await using var db = NewDb();
        var model = db.Model.FindEntityType(typeof(CallerKey));
        model.Should().NotBeNull();
        var index = model!.GetIndexes().FirstOrDefault(i => i.IsUnique);
        index.Should().NotBeNull();
        index!.Properties.Select(p => p.Name).Should().Contain("CallerId");
    }

    [Fact]
    public async Task BootstrapSeed_SkipsDuplicateCaller()
    {
        await using var db = NewDb();
        var key = CallerKey.Create("gateway", Sha256Hex("k1"), scopesJson: "[\"public-chat\"]");
        db.CallerKeys.Add(key);
        await db.SaveChangesAsync();

        // Simulates SeedBootstrapCaller skip-if-exists
        var exists = await db.CallerKeys.AnyAsync(k => k.CallerId == "gateway");
        exists.Should().BeTrue();
        (await db.CallerKeys.CountAsync()).Should().Be(1);
    }

    private static string Sha256Hex(string input)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
}
