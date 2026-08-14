using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text.Json;
using VanAn.KhachLink.Services.Http;

namespace VanAn.KhachLink.Components.Pages.Qr;

/// <summary>
/// #126 R2 Sprint 4: KhachLink QR Wallet page.
/// Lists claimed QR sessions from localStorage, syncs status with server.
/// Tap a card → fullscreen QR for guard to scan (or 6-digit code if no QR payload).
/// </summary>
public partial class Wallet : ComponentBase
{
    private bool _isLoggedIn = false;
    private bool _loading = true;
    private string _error = string.Empty;
    private string? _customerToken;
    private List<WalletSession> _activeSessions = new();

    // Fullscreen QR modal
    private bool _showFullscreen = false;
    private WalletSession? _fullscreenSession;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _customerToken = await JS.InvokeAsync<string?>("localStorage.getItem", "customer_token");
            _isLoggedIn = !string.IsNullOrEmpty(_customerToken);
            if (_isLoggedIn)
            {
                await LoadWalletAsync();
            }
            _loading = false;
            StateHasChanged();
        }
    }

    private async Task LoadWalletAsync()
    {
        try
        {
            // 1. Load sessions from localStorage
            var json = await JS.InvokeAsync<string?>("vananQrWallet.getSessions");
            var sessions = string.IsNullOrEmpty(json)
                ? new List<WalletSession>()
                : JsonSerializer.Deserialize<List<WalletSession>>(json) ?? new List<WalletSession>();

            if (sessions.Count == 0)
            {
                _activeSessions = new List<WalletSession>();
                return;
            }

            // 2. Sync with server — get current statuses
            if (!string.IsNullOrEmpty(_customerToken))
            {
                var sessionIds = sessions.Select(s => s.SessionId).ToList();
                var statuses = await GuardQrApi.GetMySessionsAsync(sessionIds, _customerToken);

                // 3. Merge: only keep sessions that are still active (Claimed or Issued)
                // Remove CheckedOut/Voided/Flagged from wallet
                _activeSessions = sessions
                    .Where(s =>
                    {
                        var status = statuses.FirstOrDefault(x => x.SessionId == s.SessionId);
                        if (status == null) return true; // Keep if server doesn't know (might be cross-tenant issue)
                        return status.Status is "Claimed" or "Issued";
                    })
                    .OrderByDescending(s => s.IssuedAt)
                    .ToList();

                // 4. Save updated list back to localStorage (remove checked-out)
                if (_activeSessions.Count < sessions.Count)
                {
                    await JS.InvokeVoidAsync("vananQrWallet.saveSessions",
                        JsonSerializer.Serialize(_activeSessions));
                }
            }
            else
            {
                _activeSessions = sessions.OrderByDescending(s => s.IssuedAt).ToList();
            }
        }
        catch (Exception ex)
        {
            _error = $"Lỗi tải ví QR: {ex.Message}";
        }
    }

    private async Task ShowFullscreenQr(WalletSession session)
    {
        _fullscreenSession = session;
        _showFullscreen = true;
        StateHasChanged();

        // Generate QR on canvas + set max brightness
        if (!string.IsNullOrEmpty(session.QrPayload))
        {
            await JS.InvokeVoidAsync("vananQrWallet.setBrightness", 1.0);
            await Task.Delay(100); // Wait for canvas to render
            await JS.InvokeVoidAsync("vananQrWallet.generateQrOnCanvas", "qr-fullscreen-canvas", session.QrPayload, 350);
        }
    }

    private async Task CloseFullscreen()
    {
        _showFullscreen = false;
        _fullscreenSession = null;
        await JS.InvokeVoidAsync("vananQrWallet.resetBrightness");
        StateHasChanged();
    }

    private void GoClaim() => Nav.NavigateTo("/qr/claim");
    private void GoLogin() => Nav.NavigateTo($"/login?returnUrl={Uri.EscapeDataString("/qr/wallet")}");

    // === Local DTO for localStorage serialization ===
    public class WalletSession
    {
        public Guid SessionId { get; set; }
        public string? QrPayload { get; set; }
        public string? ShortCode { get; set; }
        public string PlateNumber { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public Guid? TenantId { get; set; }
        public DateTime ClaimedAt { get; set; }
    }
}
