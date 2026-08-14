using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using VanAn.KhachLink.Services.Http;

namespace VanAn.KhachLink.Components.Pages.Qr;

/// <summary>
/// #126 R2 Sprint 4: KhachLink QR Claim page.
/// Customer scans QR (camera) or enters 6-digit code → POST /api/guard/claim → store in localStorage → navigate to wallet.
/// </summary>
public partial class Claim : ComponentBase
{
    private bool _isLoggedIn = false;
    private string _mode = "camera"; // "camera" | "code"
    private string _shortCodeInput = string.Empty;
    private string _error = string.Empty;
    private string _success = string.Empty;
    private bool _claiming = false;
    private string? _customerToken;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _customerToken = await JS.InvokeAsync<string?>("localStorage.getItem", "customer_token");
            _isLoggedIn = !string.IsNullOrEmpty(_customerToken);
            StateHasChanged();
        }
    }

    private void SwitchMode(string mode)
    {
        _mode = mode;
        _error = string.Empty;
        StateHasChanged();
    }

    private void GoLogin()
    {
        Nav.NavigateTo($"/login?returnUrl={Uri.EscapeDataString("/qr/claim")}");
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

    private async Task DoClaimAsync(string? qrPayload, string? shortCode)
    {
        if (string.IsNullOrEmpty(_customerToken)) return;
        _claiming = true;
        _error = string.Empty;
        _success = string.Empty;
        StateHasChanged();

        try
        {
            var result = await GuardQrApi.ClaimAsync(qrPayload, shortCode, _customerToken);
            if (result == null || !result.Success)
            {
                _error = result?.Error ?? "Nhận QR thất bại.";
                StateHasChanged();
                return;
            }

            // Store claimed session in localStorage for wallet
            var walletItem = new
            {
                sessionId = result.SessionId,
                qrPayload = qrPayload, // null if claimed via short code
                shortCode = shortCode,
                plateNumber = result.PlateNumber,
                issuedAt = result.IssuedAt,
                tenantId = ExtractTenantIdFromPayload(qrPayload),
                claimedAt = DateTime.UtcNow
            };
            await JS.InvokeVoidAsync("vananQrWallet.addSession", walletItem);

            _success = qrPayload != null
                ? "Đã nhận QR gửi xe! Chuyển đến Ví QR..."
                : "Đã nhận QR gửi xe! Vé giấy không cần nữa. Chuyển đến Ví QR...";
            StateHasChanged();

            // Navigate to wallet after short delay
            await Task.Delay(1500);
            Nav.NavigateTo("/qr/wallet");
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
}
