using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;
using Xunit;
using VanAn.Voice.Api.Services;

namespace VanAn.Voice.Tests;

public class MeteringClientTests
{
    private static (MeteringClient Client, MockHttpMessageHandler Mock) Create(Action<Dictionary<string, string?>>? config = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Metering:BaseUrl"] = "http://metering.test/",
            ["Metering:CallerId"] = "voice",
            ["Metering:CallerKey"] = "key"
        };
        config?.Invoke(values);

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var mock = new MockHttpMessageHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddHttpClient<MeteringClient>(client =>
        {
            if (!string.IsNullOrWhiteSpace(values["Metering:BaseUrl"]))
            {
                client.BaseAddress = new Uri(values["Metering:BaseUrl"]!);
                client.DefaultRequestHeaders.Add("X-Caller-Id", values["Metering:CallerId"]);
                client.DefaultRequestHeaders.Add("X-Metering-Key", values["Metering:CallerKey"]);
            }
            client.Timeout = TimeSpan.FromSeconds(3);
        }).ConfigurePrimaryHttpMessageHandler(() => mock);
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<MeteringClient>(), mock);
    }

    [Fact]
    public async Task Deny_true_when_wallet_below_limit()
    {
        var (client, mock) = Create();
        mock.When("/v1/wallets/*/check*").Respond(_ => Json(new { deny = true, balance = 0m }));

        var result = await client.CheckAsync(Guid.NewGuid(), "voice.call", 1);

        result.Should().NotBeNull();
        result!.Deny.Should().BeTrue();
    }

    [Fact]
    public async Task Allow_when_wallet_above_limit()
    {
        var (client, mock) = Create();
        mock.When("/v1/wallets/*/check*").Respond(_ => Json(new { deny = false, balance = 100m }));

        var result = await client.CheckAsync(Guid.NewGuid(), "voice.call", 1);

        result!.Deny.Should().BeFalse();
    }

    [Fact]
    public async Task Fail_open_when_metering_unreachable()
    {
        var (client, mock) = Create();
        mock.When("/v1/wallets/*/check*").Throw(new HttpRequestException("connection refused"));

        var result = await client.CheckAsync(Guid.NewGuid(), "voice.call", 1);

        result.Should().BeNull(); // caller treats null as "allow"
    }

    [Fact]
    public async Task Check_result_cached_30_seconds()
    {
        var (client, mock) = Create();
        var requests = 0;
        mock.When("/v1/wallets/*/check*").Respond(_ =>
        {
            requests++;
            return Json(new { deny = false, balance = 50m });
        });

        var tenant = Guid.NewGuid();
        await client.CheckAsync(tenant, "voice.call", 1);
        await client.CheckAsync(tenant, "voice.call", 1);

        requests.Should().Be(1); // second call served from cache
    }

    [Fact]
    public async Task Batch_sends_idempotency_keys_in_body_and_caller_headers()
    {
        var (client, mock) = Create();
        string? body = null;
        HttpRequestMessage? captured = null;
        mock.When("/v1/usage/events:batch").Respond(req =>
        {
            captured = req;
            try { body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult(); }
            catch { /* body capture is best-effort — the response must still succeed */ }
            return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });

        var ok = await client.RecordUsageBatchAsync(new[]
        {
            new MeteringUsageEvent("call-1", Guid.NewGuid(), "voice.call", 1, "call", DateTime.UtcNow),
            new MeteringUsageEvent("call-1:min", Guid.NewGuid(), "voice.minute", 3, "minute", DateTime.UtcNow)
        });

        ok.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.Headers.GetValues("X-Caller-Id").Should().Contain("voice");
        captured.Headers.GetValues("X-Metering-Key").Should().Contain("key");
        body.Should().Contain("call-1");
        body.Should().Contain("call-1:min");
        body.Should().Contain("voice.minute");
    }

    [Fact]
    public async Task Disabled_client_returns_null_and_false()
    {
        var (client, _) = Create(values =>
        {
            values["Metering:BaseUrl"] = "";
            values["Metering:CallerKey"] = "";
        });

        (await client.CheckAsync(Guid.NewGuid(), "voice.call", 1)).Should().BeNull();
        (await client.RecordUsageBatchAsync(new[] { new MeteringUsageEvent("k", Guid.NewGuid(), "voice.call", 1, "call", DateTime.UtcNow) }))
            .Should().BeFalse();
    }

    private static System.Net.Http.HttpResponseMessage Json(object body)
        => new(System.Net.HttpStatusCode.OK) { Content = System.Net.Http.Json.JsonContent.Create(body) };
}