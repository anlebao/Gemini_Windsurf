using FluentAssertions;
using Microsoft.JSInterop;
using VanAn.UI.Platform.Services;
using Xunit;

namespace VanAn.Core.Tests.KhachLink;

/// <summary>
/// Personalized DidYouMean W1 (SRS v1.1 — client-side localStorage):
/// record-on-accept, resolve-before-search (normalize TV), per-scope isolation,
/// LRU 200/scope + TTL 180d hygiene, fail-open khi storage lỗi/corrupt.
/// </summary>
public class SearchCorrectionStoreTests
{
    private const string Key = "vanan_search_corrections";

    private sealed class FakeJsRuntime : IJSRuntime
    {
        public Dictionary<string, string> Storage { get; } = new();
        public bool ThrowOnGet { get; set; }
        public bool ThrowOnSet { get; set; }
        public string? LastSetJson { get; private set; }

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
                    var json = (string)args![1]!;
                    Storage[(string)args[0]!] = json;
                    LastSetJson = json;
                    return new ValueTask<TValue>();
                case "localStorage.removeItem":
                    Storage.Remove((string)args![0]!);
                    return new ValueTask<TValue>();
                default:
                    throw new InvalidOperationException($"Unexpected JS call: {identifier}");
            }
        }
    }

    private static SearchCorrectionStore NewStore(FakeJsRuntime js) => new(js);

    [Fact]
    public async Task Resolve_EmptyStore_ReturnsNull()
    {
        var sut = NewStore(new FakeJsRuntime());

        (await sut.ResolveAsync("product", "ta sua")).Should().BeNull();
    }

    [Fact]
    public async Task Record_ThenResolve_ReturnsSuggestion()
    {
        var js = new FakeJsRuntime();
        var sut = NewStore(js);

        await sut.RecordAsync("product", "tà sua", "Trà Sữa 79", "voice");

        (await sut.ResolveAsync("product", "tà sua")).Should().Be("Trà Sữa 79");
    }

    [Fact]
    public async Task Record_NormalizesVietnameseKeys()
    {
        var sut = NewStore(new FakeJsRuntime());

        await sut.RecordAsync("product", "TÀ SUA", "Trà Sữa", "typed");

        (await sut.ResolveAsync("product", "ta sua")).Should().Be("Trà Sữa");
        (await sut.ResolveAsync("product", "tà  sua")).Should().Be("Trà Sữa",
            "normalize collapses whitespace before lookup");
    }

    [Fact]
    public async Task Record_SameKeyTwice_UsesLatestSuggestionAndIncrements()
    {
        var js = new FakeJsRuntime();
        var sut = NewStore(js);

        await sut.RecordAsync("product", "tà sua", "Trà Sữa", "typed");
        await sut.RecordAsync("product", "tà sua", "Trà Sữa 79", "voice");

        (await sut.ResolveAsync("product", "tà sua")).Should().Be("Trà Sữa 79");
        js.LastSetJson.Should().Contain("\"n\":2", "accept count increments on re-learn");
    }

    [Fact]
    public async Task Record_RawEqualsSuggestionNormalized_IsIgnored()
    {
        var js = new FakeJsRuntime();
        var sut = NewStore(js);

        await sut.RecordAsync("product", "trà sữa", "Tra Sua", "typed");

        (await sut.ResolveAsync("product", "trà sữa")).Should().BeNull(
            "nothing to correct — raw already matches suggestion after normalize");
    }

    [Fact]
    public async Task Scopes_AreIsolated()
    {
        var sut = NewStore(new FakeJsRuntime());

        await sut.RecordAsync("product", "tà sua", "Trà Sữa", "voice");

        (await sut.ResolveAsync("tenant", "tà sua")).Should().BeNull(
            "product-scope correction must not leak into tenant search");
        (await sut.ResolveAsync("product", "tà sua")).Should().Be("Trà Sữa");
    }

    [Fact]
    public async Task Resolve_ExpiredEntry_PrunesAndReturnsNull()
    {
        var js = new FakeJsRuntime();
        var stale = DateTimeOffset.UtcNow.AddDays(-200).ToUnixTimeMilliseconds();
        js.Storage[Key] = "{\"v\":1,\"scopes\":{\"product\":{\"ta sua\":{\"c\":\"tra sua\",\"d\":\"Trà Sữa\",\"n\":5,\"t\":" + stale + ",\"s\":\"typed\"}}}}";
        var sut = NewStore(js);

        (await sut.ResolveAsync("product", "ta sua")).Should().BeNull(
            "entry older than TTL 180d is dead");
        js.Storage[Key].Should().NotContain("ta sua", "expired entry pruned on read");
    }

    [Fact]
    public async Task Resolve_CorruptJson_ReturnsNull_ThenRecordRecovers()
    {
        var js = new FakeJsRuntime();
        js.Storage[Key] = "@@not-json{{";
        var sut = NewStore(js);

        (await sut.ResolveAsync("product", "ta sua")).Should().BeNull();

        await sut.RecordAsync("product", "tà sua", "Trà Sữa", "typed");
        (await sut.ResolveAsync("product", "ta sua")).Should().Be("Trà Sữa",
            "corrupt store is replaced by a fresh one on next write");
    }

    [Fact]
    public async Task Record_LruEvictsOldest_WhenOverCap()
    {
        var js = new FakeJsRuntime();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var entries = string.Join(",", Enumerable.Range(1, 200).Select(i =>
            $"\"miss {i}\":{{\"c\":\"c{i}\",\"d\":\"D{i}\",\"n\":1,\"t\":{now - i},\"s\":\"typed\"}}"));
        js.Storage[Key] = "{\"v\":1,\"scopes\":{\"product\":{" + entries + "}}}";
        var sut = NewStore(js);

        await sut.RecordAsync("product", "new miss", "New Correct", "typed");

        var json = js.Storage[Key];
        json.Should().Contain("new miss");
        json.Should().NotContain("\"miss 200\"",
            "oldest lastUsed (t smallest) evicted first");
        json.Should().Contain("\"miss 1\"", "newest seeded entry survives");
    }

    [Fact]
    public async Task Forget_RemovesEntry()
    {
        var sut = NewStore(new FakeJsRuntime());
        await sut.RecordAsync("product", "tà sua", "Trà Sữa", "voice");

        await sut.ForgetAsync("product", "tà sua");

        (await sut.ResolveAsync("product", "tà sua")).Should().BeNull();
    }

    [Fact]
    public async Task Forget_Missing_IsNoOp()
    {
        var sut = NewStore(new FakeJsRuntime());

        await sut.ForgetAsync("product", "nothing here");

        (await sut.ResolveAsync("product", "nothing here")).Should().BeNull();
    }

    [Fact]
    public async Task Resolve_GetThrows_ReturnsNull()
    {
        var sut = NewStore(new FakeJsRuntime { ThrowOnGet = true });

        (await sut.ResolveAsync("product", "x")).Should().BeNull(
            "fail-open: private-mode/denied storage → feature silently off");
    }

    [Fact]
    public async Task Record_SetThrows_NoThrow_KeepsInMemoryHit()
    {
        var js = new FakeJsRuntime { ThrowOnSet = true };
        var sut = NewStore(js);

        await sut.RecordAsync("product", "tà sua", "Trà Sữa", "voice");

        (await sut.ResolveAsync("product", "ta sua")).Should().Be("Trà Sữa",
            "persist failure still leaves the in-memory correction for this session");
    }

    [Fact]
    public async Task Resolve_Hit_RefreshesLastUsed()
    {
        var js = new FakeJsRuntime();
        var recent = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeMilliseconds();
        js.Storage[Key] = "{\"v\":1,\"scopes\":{\"product\":{\"ta sua\":{\"c\":\"tra sua\",\"d\":\"Trà Sữa\",\"n\":1,\"t\":" + recent + ",\"s\":\"typed\"}}}}";
        var sut = NewStore(js);

        (await sut.ResolveAsync("product", "ta sua")).Should().Be("Trà Sữa");

        js.LastSetJson.Should().NotBeNull("hit persists refreshed lastUsed for LRU");
    }
}
