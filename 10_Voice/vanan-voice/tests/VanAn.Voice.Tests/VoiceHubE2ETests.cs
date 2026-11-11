using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http.Json;
using Xunit;
using VanAn.Voice.Api;
using VanAn.Voice.Api.Services;
using VanAn.Voice.Contracts;

namespace VanAn.Voice.Tests;

/// <summary>
/// End-to-end over the real hub (/hubs/voice) with real staff JWTs — the SRS "no fake
/// connections" rule applies to media; signaling here runs through the actual pipeline
/// (TestServer + SignalR client + auth validators).
/// </summary>
public class VoiceHubE2ETests
{
    private static (WebApplicationFactory<VoiceProgram> Factory, string DbPath) CreateFactory()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"voice-hub-{Guid.NewGuid():N}.db");
        var factory = new WebApplicationFactory<VoiceProgram>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Jwt:Secret", TestJwt.Secret);
            b.UseSetting("Jwt:Issuer", TestJwt.Issuer);
            b.UseSetting("Jwt:Audience", TestJwt.Audience);
            b.UseSetting("Voice:DbPath", dbPath);
            b.UseSetting("Voice:GatewayDb", "");
            b.UseSetting("Voice:ShopErpBaseUrl", "");
            b.UseSetting("Metering:BaseUrl", "");
            b.UseSetting("Metering:CallerId", "");
            b.UseSetting("Metering:CallerKey", "");
        });
        return (factory, dbPath);
    }

    private static async Task<HubConnection> ConnectAsync(WebApplicationFactory<VoiceProgram> factory, string jwt)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/voice", o =>
            {
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                o.AccessTokenProvider = () => Task.FromResult<string?>(jwt);
                o.Transports = HttpTransportType.WebSockets;
                o.WebSocketFactory = (context, ct) =>
                {
                    var client = factory.Server.CreateWebSocketClient();
                    client.SubProtocols.Add("json");
                    // TestServer's WebSocketClient drops query strings — carry the token in the
                    // Authorization header instead (StaffJwtValidator accepts both transports).
                    client.ConfigureRequest = request => request.Headers["Authorization"] = "Bearer " + jwt;
                    return new ValueTask<System.Net.WebSockets.WebSocket>(client.ConnectAsync(context.Uri, ct));
                };
            })
            .Build();
        await connection.StartAsync().WaitAsync(TimeSpan.FromSeconds(15));
        return connection;
    }

    [Fact]
    public async Task Full_call_over_the_wire()
    {
        var (factory, dbPath) = CreateFactory();
        await using var _ = factory;
        try
        {
            // Enable open calling for the tenant through the real admin endpoint (§14.4).
            var admin = factory.CreateClient();
            var configResponse = await admin.PutAsJsonAsync(
                $"/v1/tenants/{VoiceIdentities.Tenant}/voice-config",
                new VoiceTenantConfig(VoiceOpenCalling: true));
            configResponse.EnsureSuccessStatusCode();

            await using var connA = await ConnectAsync(factory, TestJwt.Mint(VoiceIdentities.UserA, VoiceIdentities.Tenant));
            await using var connB = await ConnectAsync(factory, TestJwt.Mint(VoiceIdentities.UserB, VoiceIdentities.Tenant));
            var callId = Guid.NewGuid();

            var incoming = new TaskCompletionSource<IncomingCallDto>();
            connB.On<IncomingCallDto>("IncomingCall", d => incoming.TrySetResult(d));
            var accepted = new TaskCompletionSource<CallIdDto>();
            connA.On<CallIdDto>("CallAccepted", d => accepted.TrySetResult(d));
            var receiveOffer = new TaskCompletionSource<ReceiveSdpDto>();
            connB.On<ReceiveSdpDto>("ReceiveOffer", d => receiveOffer.TrySetResult(d));
            var receiveAnswer = new TaskCompletionSource<ReceiveSdpDto>();
            connA.On<ReceiveSdpDto>("ReceiveAnswer", d => receiveAnswer.TrySetResult(d));
            var connected = new TaskCompletionSource<CallIdDto>();
            connA.On<CallIdDto>("CallConnected", d => connected.TrySetResult(d));
            var ended = new TaskCompletionSource<CallIdWithReasonDto>();
            connB.On<CallIdWithReasonDto>("CallEnded", d => ended.TrySetResult(d));

            var result = await connA.InvokeAsync<StartCallResult>("StartCall",
                new StartCallRequest(callId, VoiceIdentities.UserB, null, null));
            result.Status.Should().Be("RINGING");

            var incomingCall = await incoming.Task.WaitAsync(TimeSpan.FromSeconds(10));
            incomingCall.CallerUserId.Should().Be(VoiceIdentities.UserA);

            await connB.InvokeAsync("AcceptCall", callId);
            (await accepted.Task.WaitAsync(TimeSpan.FromSeconds(10))).CallId.Should().Be(callId);

            await connA.InvokeAsync("SendOffer", new SdpDto(callId, "offer-e2e"));
            (await receiveOffer.Task.WaitAsync(TimeSpan.FromSeconds(10))).Sdp.Should().Be("offer-e2e");

            await connB.InvokeAsync("SendAnswer", new SdpDto(callId, "answer-e2e"));
            (await receiveAnswer.Task.WaitAsync(TimeSpan.FromSeconds(10))).Sdp.Should().Be("answer-e2e");

            await connA.InvokeAsync("NotifyConnected", callId);
            (await connected.Task.WaitAsync(TimeSpan.FromSeconds(10))).CallId.Should().Be(callId);

            await connA.InvokeAsync("EndCall", callId);
            var end = await ended.Task.WaitAsync(TimeSpan.FromSeconds(10));
            end.CallId.Should().Be(callId);
        }
        finally
        {
            try { File.Delete(dbPath); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task Unauthorized_connection_is_rejected()
    {
        var (factory, dbPath) = CreateFactory();
        await using var _ = factory;
        try
        {
            var connection = new HubConnectionBuilder()
                .WithUrl("http://localhost/hubs/voice", o =>
                {
                    o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    o.AccessTokenProvider = () => Task.FromResult<string?>("not-a-valid-token");
                    o.Transports = HttpTransportType.WebSockets;
                    o.WebSocketFactory = (context, ct) =>
                    {
                        var client = factory.Server.CreateWebSocketClient();
                        client.SubProtocols.Add("json");
                        client.ConfigureRequest = request => request.Headers["Authorization"] = "Bearer not-a-valid-token";
                        return new ValueTask<System.Net.WebSockets.WebSocket>(client.ConnectAsync(context.Uri, ct));
                    };
                })
                .Build();

            // The server rejects the handshake (HubException in OnConnectedAsync) — the client
            // either surfaces it from StartAsync or lands in the closed state.
            Exception? startException = null;
            try
            {
                await connection.StartAsync().WaitAsync(TimeSpan.FromSeconds(15));
            }
            catch (Exception ex)
            {
                startException = ex;
            }

            if (startException == null)
            {
                await Task.Delay(500);
                connection.State.Should().Be(HubConnectionState.Disconnected);
            }
            else
            {
                startException.Should().NotBeNull();
            }
        }
        finally
        {
            try { File.Delete(dbPath); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        var (factory, dbPath) = CreateFactory();
        await using var _ = factory;
        try
        {
            var client = factory.CreateClient();
            var response = await client.GetAsync("/v1/health");
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        }
        finally
        {
            try { File.Delete(dbPath); } catch { /* best effort */ }
        }
    }
}