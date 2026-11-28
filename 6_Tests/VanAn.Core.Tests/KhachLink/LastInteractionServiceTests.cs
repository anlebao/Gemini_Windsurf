using FluentAssertions;
using Microsoft.JSInterop;
using VanAn.KhachLink.Services;
using Xunit;

namespace VanAn.Core.Tests.KhachLink;

/// <summary>
/// Store Context (task_store_context_back_to_store): rich LastStoreVisit record
/// {tenantId, slug, name, visitedAt} written to `khachlink_last_store` + legacy
/// `khachlink_last_tenant_id` kept for existing readers. Legacy bare-GUID data
/// still readable (migration). Fail-open on corrupt JSON / JS errors / prerender.
/// </summary>
public class LastInteractionServiceTests
{
    private const string LegacyKey = "khachlink_last_tenant_id";
    private const string StoreKey = "khachlink_last_store";

    private sealed class FakeJsRuntime : IJSRuntime
    {
        public Dictionary<string, string> Storage { get; } = new();
        public bool ThrowOnGet { get; set; }
        public bool ThrowOnSet { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,
            CancellationToken cancellationToken, object?[]? args)
        {
            switch (identifier)
            {
                case "localStorage.getItem":
                    if (ThrowOnGet) throw new JSException("denied");
                    Storage.TryGetValue((string)args![0]!, out var v);
                    return ValueTask.FromResult((TValue)(object?)v!);
                case "localStorage.setItem":
                    if (ThrowOnSet) throw new JSException("quota exceeded");
                    Storage[(string)args[0]!] = (string)args[1]!;
                    return new ValueTask<TValue>();
                case "localStorage.removeItem":
                    Storage.Remove((string)args![0]!);
                    return new ValueTask<TValue>();
                default:
                    throw new InvalidOperationException($"Unexpected JS call: {identifier}");
            }
        }
    }

    private static LastInteractionService NewService(FakeJsRuntime js) => new(js);

    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Record_WritesBothRichAndLegacyKeys()
    {
        var js = new FakeJsRuntime();
        var sut = NewService(js);

        await sut.RecordInteractionAsync(TenantA, "an-tam", "An Tâm");

        js.Storage.Should().ContainKey(LegacyKey).WhoseValue.Should().Be(TenantA.ToString());
        js.Storage.Should().ContainKey(StoreKey);
        var visit = System.Text.Json.JsonSerializer.Deserialize<LastStoreVisit>(js.Storage[StoreKey]);
        visit.Should().NotBeNull();
        visit!.TenantId.Should().Be(TenantA);
        visit.Slug.Should().Be("an-tam");
        visit.Name.Should().Be("An Tâm");
    }

    [Fact]
    public async Task Record_ThenGet_RoundtripsRichRecord()
    {
        var js = new FakeJsRuntime();
        var sut = NewService(js);

        await sut.RecordInteractionAsync(TenantA, "an-tam", "An Tâm");
        var visit = await sut.GetLastStoreVisitAsync();

        visit.Should().NotBeNull();
        visit!.TenantId.Should().Be(TenantA);
        visit.Slug.Should().Be("an-tam");
        visit.Name.Should().Be("An Tâm");
        visit.VisitedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Record_WithoutSlugName_StillRecords()
    {
        var js = new FakeJsRuntime();
        var sut = NewService(js);

        await sut.RecordInteractionAsync(TenantA);
        var visit = await sut.GetLastStoreVisitAsync();

        visit.Should().NotBeNull();
        visit!.TenantId.Should().Be(TenantA);
        visit.Slug.Should().BeNull();
        visit.Name.Should().BeNull();
    }

    [Fact]
    public async Task GetLastTenantId_LegacyOnly_ReturnsLegacyGuid()
    {
        var js = new FakeJsRuntime();
        js.Storage[LegacyKey] = TenantA.ToString();
        var sut = NewService(js);

        (await sut.GetLastTenantIdAsync()).Should().Be(TenantA);

        var visit = await sut.GetLastStoreVisitAsync();
        visit.Should().NotBeNull();
        visit!.TenantId.Should().Be(TenantA);
        visit.Slug.Should().BeNull();
        visit.Name.Should().BeNull();
    }

    [Fact]
    public async Task Record_EmptyGuid_IsNoOp()
    {
        var js = new FakeJsRuntime();
        var sut = NewService(js);

        await sut.RecordInteractionAsync(Guid.Empty, "x", "x");

        js.Storage.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_CorruptJson_FallsBackToLegacyOrNull()
    {
        var js = new FakeJsRuntime();
        js.Storage[StoreKey] = "{not-json";
        js.Storage[LegacyKey] = TenantA.ToString();
        var sut = NewService(js);

        var visit = await sut.GetLastStoreVisitAsync();
        visit.Should().NotBeNull();
        visit!.TenantId.Should().Be(TenantA);
        visit.Slug.Should().BeNull();

        (await sut.GetLastTenantIdAsync()).Should().Be(TenantA);
    }

    [Fact]
    public async Task Get_JsThrows_ReturnsNullAndEmpty()
    {
        var js = new FakeJsRuntime { ThrowOnGet = true };
        var sut = NewService(js);

        (await sut.GetLastStoreVisitAsync()).Should().BeNull();
        (await sut.GetLastTenantIdAsync()).Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task Record_LastWriteWins()
    {
        var js = new FakeJsRuntime();
        var sut = NewService(js);

        await sut.RecordInteractionAsync(TenantA, "an-tam", "An Tâm");
        await sut.RecordInteractionAsync(TenantB, "shop-b", "Shop B");

        var visit = await sut.GetLastStoreVisitAsync();
        visit!.TenantId.Should().Be(TenantB);
        visit.Slug.Should().Be("shop-b");
        (await sut.GetLastTenantIdAsync()).Should().Be(TenantB);
    }

    [Fact]
    public async Task GetLastTenantId_NewKeyWinsOverStaleLegacy()
    {
        var js = new FakeJsRuntime();
        var sut = NewService(js);

        // Legacy data from before the upgrade (tenant A), then a new visit (tenant B).
        js.Storage[LegacyKey] = TenantA.ToString();
        await sut.RecordInteractionAsync(TenantB, "shop-b", "Shop B");

        (await sut.GetLastTenantIdAsync()).Should().Be(TenantB);
    }

    [Fact]
    public async Task Clear_RemovesBothKeys()
    {
        var js = new FakeJsRuntime();
        var sut = NewService(js);

        await sut.RecordInteractionAsync(TenantA, "an-tam", "An Tâm");
        await sut.ClearAsync();

        js.Storage.Should().BeEmpty();
        (await sut.GetLastStoreVisitAsync()).Should().BeNull();
        (await sut.GetLastTenantIdAsync()).Should().Be(Guid.Empty);
    }
}
