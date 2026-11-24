namespace VanAn.Directory.Services;

/// <summary>
/// Phase 2 Scaling pattern (copy từ ShopERP Program.cs): JSInvokable ping cho
/// circuit-keepalive.js — chỉ để giữ circuit busy qua proxy idle timeout,
/// không logic. JS gọi DotNet.invokeMethodAsync('VanAn.Directory', 'CircuitKeepalivePing') mỗi 10s.
/// </summary>
public static class CircuitKeepalive
{
    [Microsoft.JSInterop.JSInvokable]
    public static Task<bool> CircuitKeepalivePing() => Task.FromResult(true);
}
