using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using VanAn.TenantAi.Ai;
using VanAn.TenantAi.Contracts;
using Xunit;

namespace VanAn.TenantAi.Tests.ThreatSuite;

/// <summary>Capture-everything provider registered into the host's logging pipeline.</summary>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<string> Records { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Rec(Records);

    public void Dispose() { }

    private sealed class Rec(ConcurrentQueue<string> q) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
            => q.Enqueue(formatter(state, exception));
    }
}

/// <summary>
/// SRS §10 T7 — key/secret material must never reach logs. Runs the real auth +
/// chat pipeline (good and bad credentials) then scans every captured log record
/// for raw keys, key hashes, the signing secret, and ctx/sig header values.
/// </summary>
[Trait("Threat", "T7")]
public sealed class T7_KeyLeakLogTests : IDisposable
{
    private static readonly Guid TenantA = TenantAiApiFactory.TenantA;

    private readonly RecordingLoggerProvider _logs = new();
    private readonly TenantAiApiFactory _factory;
    private readonly HttpClient _client;

    public T7_KeyLeakLogTests()
    {
        var logs = _logs;
        _factory = TenantAiApiFactory.WithServices(s =>
        {
            s.RemoveAll<IEmbeddingProvider>();
            s.AddSingleton<IEmbeddingProvider>(new FakeEmbeddingProvider());
            s.RemoveAll<ILlmChatClient>();
            s.AddSingleton<ILlmChatClient>(new FakeLlmChatClient());
            s.AddSingleton<ILoggerProvider>(logs);
        });
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    private HttpRequestMessage Req(HttpMethod method, string path)
    {
        var r = new HttpRequestMessage(method, path);
        r.Headers.Add("X-Caller-Id", "gateway");
        r.Headers.Add("X-Ai-Key", TenantAiApiFactory.GatewayKey);
        var (ctx, sig) = TenantContextSigner.Sign(
            TenantContextSigner.CreatePayload(TenantA, "gateway", "req-t7"),
            TenantAiApiFactory.SigningSecret);
        r.Headers.Add("X-Tenant-Ctx", ctx);
        r.Headers.Add("X-Tenant-Ctx-Sig", sig);
        return r;
    }

    [Fact]
    public async Task T7_Logs_NeverContainKeysSecretsOrSignatures()
    {
        // Success path (full auth chain) — capture the exact wire values to scan for.
        var chat = Req(HttpMethod.Post, "/v1/chat");
        chat.Content = JsonContent.Create(new ChatRequest("t7", "t7-m1", "Xin chào"));
        var wireCtx = chat.Headers.GetValues("X-Tenant-Ctx").Single();
        var wireSig = chat.Headers.GetValues("X-Tenant-Ctx-Sig").Single();
        (await _client.SendAsync(chat)).StatusCode.Should().Be(HttpStatusCode.OK);

        var badKey = Req(HttpMethod.Get, "/v1/auth/check");
        badKey.Headers.Remove("X-Ai-Key");
        badKey.Headers.Add("X-Ai-Key", "totally-wrong-key-99");
        (await _client.SendAsync(badKey)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var badSig = Req(HttpMethod.Get, "/v1/auth/tenant-check");
        badSig.Headers.Remove("X-Tenant-Ctx-Sig");
        badSig.Headers.Add("X-Tenant-Ctx-Sig", "AAAA" + wireSig[4..]);
        (await _client.SendAsync(badSig)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        _logs.Records.Should().NotBeEmpty("middleware emits an audit line per request");

        var forbidden = new[]
        {
            TenantAiApiFactory.GatewayKey,
            TenantAiApiFactory.ShopKey,
            TenantAiApiFactory.SigningSecret,
            "totally-wrong-key-99",
            Sha256Hex(TenantAiApiFactory.GatewayKey),
            Sha256Hex(TenantAiApiFactory.ShopKey),
            wireSig,  // full valid signature
            wireCtx,  // full ctx header value
        };
        foreach (var record in _logs.Records)
            foreach (var secret in forbidden)
                record.Should().NotContain(secret, $"log record leaks secret material: {record}");
    }

    private static string Sha256Hex(string input)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
}
