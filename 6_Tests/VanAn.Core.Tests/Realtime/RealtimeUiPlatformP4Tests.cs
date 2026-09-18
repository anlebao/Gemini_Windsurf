using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RichardSzalay.MockHttp;
using VanAn.UI.Platform.Adapters;
using VanAn.UI.Platform.Core.Interfaces;
using VanAn.UI.Platform.Realtime;
using Xunit;

namespace VanAn.Core.Tests.Realtime;

/// <summary>
/// Realtime Platform P4: UI Platform extraction tests.
///   T1-T3  — RealtimeEndpointUrls.DeriveGatewayBaseUrl (F4, ported from ChatPanel.DeriveGatewayUrl / C8)
///   T4-T5  — NavigationRealtimeEndpointProvider hub URLs (F4)
///   T6-T11 — RealtimeHttpAdapter against MockHttp (UI-4): URL building, identity headers, parsing, errors
/// </summary>
public class RealtimeUiPlatformP4Tests
{
    private const string GatewayBase = "https://api2.khachvip.online";

    // === T1-T3: endpoint derivation (F4 / C8) ===

    [Theory(DisplayName = "T1: DeriveGatewayBaseUrl — *.khachvip.online subdomain suffix maps 1:1")]
    [InlineData("https://diemthuong2.khachvip.online/", "https://api2.khachvip.online")]
    [InlineData("https://diemthuong.khachvip.online/", "https://api.khachvip.online")]
    [InlineData("https://app3.khachvip.online/", "https://api3.khachvip.online")]
    [InlineData("https://app4.khachvip.online/", "https://api4.khachvip.online")]
    public void DeriveGatewayBaseUrl_KhachVipSubdomain_MapsSuffix(string baseUrl, string expected)
    {
        Assert.Equal(expected, RealtimeEndpointUrls.DeriveGatewayBaseUrl(baseUrl));
    }

    [Theory(DisplayName = "T2: DeriveGatewayBaseUrl — custom domains stay same-origin (nginx proxies /api + /hubs)")]
    [InlineData("https://timlathay.com/", "https://timlathay.com")]
    [InlineData("https://shop.example.com/", "https://shop.example.com")]
    public void DeriveGatewayBaseUrl_CustomDomain_SameOrigin(string baseUrl, string expected)
    {
        Assert.Equal(expected, RealtimeEndpointUrls.DeriveGatewayBaseUrl(baseUrl));
    }

    [Fact(DisplayName = "T3: DeriveGatewayBaseUrl — localhost keeps its port (dev)")]
    public void DeriveGatewayBaseUrl_Localhost_KeepsPort()
    {
        Assert.Equal("https://localhost:5002", RealtimeEndpointUrls.DeriveGatewayBaseUrl("https://localhost:5002/"));
    }

    // === T4-T5: NavigationRealtimeEndpointProvider (F4) ===

    [Fact(DisplayName = "T4: NavigationRealtimeEndpointProvider — hub URLs derive from the gateway base")]
    public void EndpointProvider_HubUrls_DeriveFromGatewayBase()
    {
        var nav = new FakeNavigationManager("https://diemthuong2.khachvip.online/");
        var provider = new NavigationRealtimeEndpointProvider(nav);

        Assert.Equal("https://api2.khachvip.online", provider.GetGatewayBaseUrl());
        Assert.Equal("https://api2.khachvip.online/hubs/messaging", provider.GetMessagingHubUrl());
        Assert.Equal("https://api2.khachvip.online/hubs/tracking", provider.GetTrackingHubUrl());
    }

    [Fact(DisplayName = "T5: NavigationRealtimeEndpointProvider — custom domain hub URLs are same-origin")]
    public void EndpointProvider_CustomDomain_SameOriginHubs()
    {
        var nav = new FakeNavigationManager("https://timlathay.com/");
        var provider = new NavigationRealtimeEndpointProvider(nav);

        Assert.Equal("https://timlathay.com/hubs/messaging", provider.GetMessagingHubUrl());
        Assert.Equal("https://timlathay.com/hubs/tracking", provider.GetTrackingHubUrl());
    }

    // === T6-T11: RealtimeHttpAdapter (UI-4) ===

    [Fact(DisplayName = "T6: GetHistoryAsync — builds the generic URL and sends X-Customer-Token")]
    public async Task GetHistoryAsync_SendsCustomerToken()
    {
        var subjectId = Guid.NewGuid();
        var handler = new MockHttpMessageHandler();
        handler.When(HttpMethod.Get, $"{GatewayBase}/api/realtime/conversations/Order/{subjectId}?take=100")
               .WithHeaders("X-Customer-Token", "tok-123")
               .Respond("application/json",
                   $$"""{"conversationId":"{{Guid.NewGuid()}}","messages":[{"id":"{{Guid.NewGuid()}}","senderId":"{{Guid.NewGuid()}}","content":"xin chào","sentAt":"2026-09-17T10:00:00Z","isRead":false}]}""");

        var adapter = BuildAdapter(handler);
        var result = await adapter.GetHistoryAsync("Order", subjectId, "tok-123", null);

        Assert.True(result.Success);
        Assert.Single(result.Messages);
        Assert.Equal("xin chào", result.Messages[0].Content);
    }

    [Fact(DisplayName = "T7: GetHistoryAsync — guest identity goes as X-Customer-Device-Id")]
    public async Task GetHistoryAsync_Guest_SendsDeviceHeader()
    {
        var subjectId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var handler = new MockHttpMessageHandler();
        handler.When(HttpMethod.Get, $"{GatewayBase}/api/realtime/conversations/Order/{subjectId}?take=100")
               .WithHeaders("X-Customer-Device-Id", deviceId.ToString())
               .Respond("application/json", """{"conversationId":"00000000-0000-0000-0000-000000000000","messages":[]}""");

        var adapter = BuildAdapter(handler);
        var result = await adapter.GetHistoryAsync("Order", subjectId, null, deviceId);

        Assert.True(result.Success);
        Assert.Empty(result.Messages);
    }

    [Fact(DisplayName = "T8: SendMessageAsync — POSTs the subject payload and parses the response")]
    public async Task SendMessageAsync_PostsSubjectPayload()
    {
        var subjectId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var handler = new MockHttpMessageHandler();
        handler.When(HttpMethod.Post, $"{GatewayBase}/api/realtime/conversations/messages")
               .WithHeaders("X-Customer-Token", "tok-123")
               .WithContent("""{"subjectType":"Order","subjectId":"SUBJECT_ID","content":"hello"}"""
                   .Replace("SUBJECT_ID", subjectId.ToString()))
               .Respond("application/json", $$"""{"conversationId":"{{Guid.NewGuid()}}","messageId":"{{messageId}}","sentAt":"2026-09-18T01:02:03Z"}""");

        var adapter = BuildAdapter(handler);
        var result = await adapter.SendMessageAsync("Order", subjectId, "hello", "tok-123", null);

        Assert.True(result.Success);
        Assert.Equal(messageId, result.MessageId);
    }

    [Fact(DisplayName = "T9: GetLatestAsync — 200 with null coordinates is Success with no position")]
    public async Task GetLatestAsync_NoPing_ReturnsSuccessWithNulls()
    {
        var subjectId = Guid.NewGuid();
        var handler = new MockHttpMessageHandler();
        handler.When(HttpMethod.Get, $"{GatewayBase}/api/realtime/location/Order/{subjectId}/latest")
               .WithHeaders("X-Customer-Token", "tok-123")
               .Respond("application/json", """{"subjectType":"Order","subjectId":"SUBJECT_ID","lat":null,"lng":null,"trackerId":null,"recordedAt":null}"""
                   .Replace("SUBJECT_ID", subjectId.ToString()));

        var adapter = BuildAdapter(handler);
        var result = await adapter.GetLatestAsync("Order", subjectId, "tok-123", null);

        Assert.True(result.Success);
        Assert.Null(result.Lat);
        Assert.Null(result.Lng);
    }

    [Fact(DisplayName = "T10: RecordPingAsync — POSTs coordinates and parses recordedAt")]
    public async Task RecordPingAsync_PostsCoordinates()
    {
        var subjectId = Guid.NewGuid();
        var handler = new MockHttpMessageHandler();
        handler.When(HttpMethod.Post, $"{GatewayBase}/api/realtime/location/ping")
               .WithHeaders("X-Customer-Device-Id", "11111111-1111-1111-1111-111111111111")
               .Respond("application/json", """{"recordedAt":"2026-09-18T02:00:00Z"}""");

        var adapter = BuildAdapter(handler);
        var result = await adapter.RecordPingAsync("Order", subjectId, 10.8, 106.7, null,
            Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.True(result.Success);
        Assert.Equal(new DateTime(2026, 9, 18, 2, 0, 0, DateTimeKind.Utc), result.RecordedAt);
    }

    [Fact(DisplayName = "T12: staff token authenticates via Authorization Bearer (P5)")]
    public async Task GetHistoryAsync_StaffToken_SendsBearer()
    {
        var subjectId = Guid.NewGuid();
        var handler = new MockHttpMessageHandler();
        handler.When(HttpMethod.Get, $"{GatewayBase}/api/realtime/conversations/Shop/{subjectId}?take=100")
               .WithHeaders("Authorization", "Bearer staff-jwt-123")
               .Respond("application/json", """{"conversationId":"00000000-0000-0000-0000-000000000000","messages":[]}""");

        var adapter = BuildAdapter(handler);
        var result = await adapter.GetHistoryAsync("Shop", subjectId, null, null, 100, "staff-jwt-123");

        Assert.True(result.Success);
    }

    [Fact(DisplayName = "T11: HTTP errors map to ErrorCode + ErrorMessage from the body")]
    public async Task GetHistoryAsync_Forbidden_MapsError()
    {
        var subjectId = Guid.NewGuid();
        var handler = new MockHttpMessageHandler();
        handler.When(HttpMethod.Get, $"{GatewayBase}/api/realtime/conversations/Order/{subjectId}?take=100")
               .WithHeaders("X-Customer-Token", "tok-123")
               .Respond(System.Net.HttpStatusCode.Forbidden, "application/json",
                   """{"error":"Bạn không có quyền truy cập chủ thể này."}""");

        var adapter = BuildAdapter(handler);
        var result = await adapter.GetHistoryAsync("Order", subjectId, "tok-123", null);

        Assert.False(result.Success);
        Assert.Equal(403, result.ErrorCode);
        Assert.Equal("Bạn không có quyền truy cập chủ thể này.", result.ErrorMessage);
    }

    // === helpers ===

    private static RealtimeHttpAdapter BuildAdapter(MockHttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient("realtime", client => { })
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        return new RealtimeHttpAdapter(factory, new StubEndpoints(), NullLogger<RealtimeHttpAdapter>.Instance);
    }

    private sealed class StubEndpoints : IRealtimeEndpointProvider
    {
        public string GetGatewayBaseUrl() => GatewayBase;
        public string GetMessagingHubUrl() => $"{GatewayBase}/hubs/messaging";
        public string GetTrackingHubUrl() => $"{GatewayBase}/hubs/tracking";
    }

    private sealed class FakeNavigationManager(string baseUri) : NavigationManager
    {
        protected override void EnsureInitialized() => Initialize(baseUri, baseUri);
    }
}
