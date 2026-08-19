using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text.Json;
using VanAn.KhachLink.Services.Http;

namespace VanAn.KhachLink.Components.Pages.Qr;

/// <summary>
/// OCR Hub S1: KhachLink QR Wallet page — merged with Claim page.
/// 2 tabs: "Vé của tôi" (wallet list) + "Nhận QR mới" (QRScanner + short code input).
/// No login required — anonymous users save QR to localStorage (like add-to-cart).
/// Logged-in users optionally sync with server via /api/guard/claim.
/// </summary>
public partial class Wallet : ComponentBase
{
    private bool _isLoggedIn = false;
    private bool _loading = true;
    private string _error = string.Empty;
    private string _success = string.Empty;
    private string? _customerToken;

    // Tab state — S1: merge claim + wallet into 1 page
    private string _activeTab = "wallet"; // "wallet" | "claim"
    private string _mode = "camera"; // "camera" | "code" (for claim tab)
    private string _shortCodeInput = string.Empty;
    private bool _claiming = false;
    private bool _showBackupWarning = false;

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

            // S1: Always load wallet from localStorage (no login gate)
            await LoadWalletAsync();
            _loading = false;
            StateHasChanged();

            // #130-fix3: Handle deep link from QR URL.
            // When customer scans QR with Zalo → opens /qr/wallet?data={base64(json)} → auto-claim.
            // (Moved from Claim.razor.cs — Claim now redirects here.)
            var uri = Nav.ToAbsoluteUri(Nav.Uri);
            var query = uri.Query.TrimStart('?');
            string? dataParam = null;
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = pair.IndexOf('=');
                if (eq > 0 && pair[..eq] == "data")
                {
                    dataParam = Uri.UnescapeDataString(pair[(eq + 1)..]);
                    break;
                }
            }
            if (!string.IsNullOrEmpty(dataParam))
            {
                // Switch to claim tab + auto-claim from deep link
                _activeTab = "claim";
                StateHasChanged();
                try
                {
                    var jsonPayload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(dataParam));
                    await DoClaimAsync(jsonPayload, null);
                }
                catch (Exception ex)
                {
                    _error = $"Mã QR không hợp lệ: {ex.Message}";
                    StateHasChanged();
                }
            }
        }
    }

    private void SwitchTab(string tab)
    {
        _activeTab = tab;
        _error = string.Empty;
        _success = string.Empty;
        StateHasChanged();
    }

    private void SwitchMode(string mode)
    {
        _mode = mode;
        _error = string.Empty;
        StateHasChanged();
    }

    private async Task LoadWalletAsync()
    {
        try
        {
            // 1. Load sessions from localStorage (always — no login required)
            // #150-fix: localStorage stores camelCase (sessionId, qrPayload, ...) but WalletSession
            // has PascalCase properties (SessionId, QrPayload, ...). Default JsonSerializer is
            // case-sensitive → all fields deserialize as null/default → "Vé không hợp lệ".
            // Fix: use PropertyNameCaseInsensitive = true.
            var json = await JS.InvokeAsync<string?>("vananQrWallet.getSessions");
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var sessions = string.IsNullOrEmpty(json)
                ? new List<WalletSession>()
                : JsonSerializer.Deserialize<List<WalletSession>>(json, options) ?? new List<WalletSession>();

            if (sessions.Count == 0)
            {
                _activeSessions = new List<WalletSession>();
                return;
            }

            // 2. Sync with server — get current statuses (only if logged in)
            if (!string.IsNullOrEmpty(_customerToken))
            {
                try
                {
                    var sessionIds = sessions.Select(s => s.SessionId).ToList();
                    var statuses = await GuardQrApi.GetMySessionsAsync(sessionIds, _customerToken);

                    // 3. Merge: only keep sessions that are still active (Claimed or Issued)
                    _activeSessions = sessions
                        .Where(s =>
                        {
                            var status = statuses.FirstOrDefault(x => x.SessionId == s.SessionId);
                            if (status == null) return true; // Keep if server doesn't know
                            return status.Status is "Claimed" or "Issued";
                        })
                        .OrderByDescending(s => s.IssuedAt)
                        .ToList();

                    // 4. Save updated list back to localStorage
                    if (_activeSessions.Count < sessions.Count)
                    {
                        await JS.InvokeVoidAsync("vananQrWallet.saveSessions",
                            JsonSerializer.Serialize(_activeSessions));
                    }
                }
                catch
                {
                    // Server sync failed — just show localStorage sessions (offline-friendly)
                    _activeSessions = sessions.OrderByDescending(s => s.IssuedAt).ToList();
                }
            }
            else
            {
                // Not logged in — just show localStorage sessions
                _activeSessions = sessions.OrderByDescending(s => s.IssuedAt).ToList();
            }
        }
        catch (Exception ex)
        {
            _error = $"Lỗi tải ví QR: {ex.Message}";
        }
    }

    /// <summary>QRScanner callback — camera detected a QR payload.</summary>
    private async Task OnQrDetected(string qrPayload)
    {
        await DoClaimAsync(qrPayload, null);
    }

    private async Task ClaimByCode()
    {
        if (string.IsNullOrWhiteSpace(_shortCodeInput)) return;
        await DoClaimAsync(null, _shortCodeInput.Trim());
    }

    /// <summary>
    /// S1: DoClaimAsync — handle both logged-in + anonymous.
    /// Logged-in: call API /api/guard/claim (optional server-side tracking).
    /// Anonymous: save QR payload/shortCode to localStorage wallet directly (like add-to-cart).
    /// </summary>
    private async Task DoClaimAsync(string? qrPayload, string? shortCode)
    {
        _claiming = true;
        _error = string.Empty;
        _success = string.Empty;
        _showBackupWarning = false;
        StateHasChanged();

        try
        {
            string? plateNumber = null;
            Guid? tenantId = ExtractTenantIdFromPayload(qrPayload);
            DateTime issuedAt = DateTime.UtcNow;
            Guid sessionId = Guid.NewGuid(); // fallback for anonymous

            if (!string.IsNullOrEmpty(_customerToken))
            {
                // Logged-in: call API for server-side claim (optional tracking)
                try
                {
                    var result = await GuardQrApi.ClaimAsync(qrPayload, shortCode, _customerToken);
                    if (result != null && result.Success)
                    {
                        plateNumber = result.PlateNumber;
                        sessionId = result.SessionId;
                        issuedAt = result.IssuedAt;
                    }
                    // If API fails, continue to save locally (don't block user)
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Wallet] Claim API failed (saving locally): {ex.Message}");
                }
            }

            // S1: Always save to localStorage wallet (both logged-in + anonymous)
            var walletItem = new
            {
                sessionId = sessionId,
                qrPayload = qrPayload,
                shortCode = shortCode,
                plateNumber = plateNumber ?? string.Empty,
                issuedAt = issuedAt,
                tenantId = tenantId,
                claimedAt = DateTime.UtcNow
            };
            await JS.InvokeVoidAsync("vananQrWallet.addSession", walletItem);

            // S1: Show backup warning for anonymous users (review fix — prevent lost ticket disputes)
            if (string.IsNullOrEmpty(_customerToken))
            {
                _showBackupWarning = true;
            }

            _success = qrPayload != null
                ? "Đã nhận QR gửi xe! Lưu vào ví thành công."
                : "Đã nhận QR gửi xe! Vé giấy không cần nữa.";
            StateHasChanged();

            // Reload wallet list + switch to wallet tab
            await LoadWalletAsync();
            await Task.Delay(800);
            _activeTab = "wallet";
            StateHasChanged();
        }
        catch (Exception ex)
        {
            _error = $"Lỗi: {ex.Message}";
            StateHasChanged();
        }
        finally
        {
            _claiming = false;
            StateHasChanged();
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

    private static Guid? ExtractTenantIdFromPayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("tn", out var tn) && tn.TryGetGuid(out var guid))
                return guid;
        }
        catch { }
        return null;
    }

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
