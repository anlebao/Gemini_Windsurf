using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using VanAn.ShopERP.Services;
using VanAn.UI.Platform.Components;

namespace VanAn.ShopERP.Components.Pages.Guard
{
    /// <summary>
    /// #126: Guard Scanner page code-behind.
    /// 3 tabs: Issue (capture photos + create QR) | Verify (scan QR + checkout/flag) | Today (stats + list).
    /// Camera + QR interop via guard-camera.js + qr-scanner.js.
    /// </summary>
    public partial class Scan : ComponentBase, IDisposable
    {
        // #130: JS interop return type for uploadCapturedPhoto(slot, jwt, baseUrl).
        // { success: bool, key: string, error: string } — small JSON, safe for SignalR.
        private class UploadResult
        {
            public bool Success { get; set; }
            public string Key { get; set; } = string.Empty;
            public string Error { get; set; } = string.Empty;
        }
        private string activeTab = "issue";
        private string errorMessage = string.Empty;
        private string successMessage = string.Empty;

        // === Issue tab state ===
        // #126-fix2: Photos stored JS-side (survives circuit disconnect). Blazor only tracks plate number + phone.
        private int issueStep = 1;
        private string plateNumber = string.Empty;
        private string customerPhone = string.Empty;
        private bool issuing = false;
        private string qrImageBase64 = string.Empty;
        private string issuedShortCode = string.Empty;
        private Guid issuedSessionId;

        // === Blazor interactivity guard (Gate 2 Category C) ===
        private bool isInteractive = false;

        // === Verify tab state ===
        private int verifyStep = 1;
        private bool verifyScanning = false;
        private string manualQrPayload = string.Empty;
        private VerifyResultDto? verifyResult;
        private bool actionLoading = false;
        private DotNetObjectReference<Scan>? _dotNetRef;

        // === Today tab state ===
        private TodaySessionsResultDto? todaySessions;
        private string todayStatusFilter = string.Empty;
        private int todayPage = 1;
        private const int TodayPageSize = 20;

        // === Detail modal ===
        private bool showDetailModal = false;
        private SessionDetailResultDto? sessionDetail;

        // #126-fix2: canIssue checks plate number only — photos are in JS-side storage.
        // Photo availability is verified at issue time via JS interop (getCapturedPhoto).
        private bool canIssue => !string.IsNullOrWhiteSpace(plateNumber);

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                isInteractive = true;
                _dotNetRef = DotNetObjectReference.Create(this);
                StateHasChanged();

                // #126-fix: Start circuit keep-alive ping (15s) to prevent idle disconnect.
                await JS.InvokeVoidAsync("vananGuardCamera.startKeepAlive", _dotNetRef);

                // #130: Fetch photo compression config from Gateway (public URL for browser).
                // JS stores config in _photoConfig global, used by captureAndStore + compressPhoto.
                var publicUrl = GuardApi.PublicGatewayBaseUrl;
                if (!string.IsNullOrEmpty(publicUrl))
                {
                    await JS.InvokeVoidAsync("vananGuardCamera.loadPhotoConfig", publicUrl);
                }

                // #130-fix: Preload Tesseract OCR worker on page load (not lazy on first capture).
                // Eliminates ~3s delay on first plate recognition. Fire-and-forget — don't block UI.
                _ = JS.InvokeVoidAsync("vananGuardCamera.preloadOcrWorker");

                // #126-fix2: Restore plate number from sessionStorage (photos restored by JS on DOMContentLoaded).
                var savedPlateNumber = await JS.InvokeAsync<string?>("vananGuardCamera.loadState", "plateNumber");
                if (!string.IsNullOrEmpty(savedPlateNumber))
                {
                    plateNumber = savedPlateNumber;
                    StateHasChanged();
                }
            }
        }

        /// <summary>#126-fix: No-op method invoked by JS keep-alive ping every 15s.</summary>
        [JSInvokable]
        public Task KeepAlivePingAsync()
        {
            return Task.CompletedTask;
        }

        private async Task SwitchTab(string tab)
        {
            activeTab = tab;
            errorMessage = string.Empty;
            successMessage = string.Empty;
            if (tab == "today")
            {
                await LoadTodayAsync();
            }
            else if (tab == "verify" && verifyScanning)
            {
                await StopVerifyScan();
            }
            StateHasChanged();
        }

        // === ISSUE: Camera + photo capture ===
        // #126-fix2: Camera/capture/OCR are now pure JS (guard-camera.js).
        // Blazor only reads captured photos when user clicks "Tạo QR".
        // No Blazor camera methods needed — all handled by JS onclick handlers.

        // === ISSUE: Create QR ===

        private async Task IssueQrAsync()
        {
            issuing = true;
            errorMessage = string.Empty;
            successMessage = string.Empty;
            try
            {
                // #130-fix: 45s overall timeout — prevents "loading mãi" if any step hangs.
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));

                // #126-fix2: Read plate number + phone from DOM input via JS.
                // OCR fills the input via JS (setInputValue), which dispatches a change event
                // so Blazor @bind syncs. But as a safety net, always read from DOM at issue time.
                var domPlate = await JS.InvokeAsync<string?>("vananGuardCamera.getInputValue", "plateInput") ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(domPlate)) plateNumber = domPlate;
                var domPhone = await JS.InvokeAsync<string?>("vananGuardCamera.getInputValue", "customerPhoneInput") ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(domPhone)) customerPhone = domPhone;

                if (string.IsNullOrWhiteSpace(plateNumber))
                {
                    errorMessage = "Chưa nhập biển số. Hãy chụp ảnh và nhận diện, hoặc nhập thủ công.";
                    return;
                }

                // #130-fix: Check photo existence WITHOUT transferring base64 over SignalR.
                // Previous code called getCapturedPhoto (returns ~200-650KB base64 string) →
                // exceeded SignalR MaximumReceiveMessageSize (32KB default) → circuit disconnect
                // → "Kết nối bị gián đoạn" → issuing stuck true → nút xoay mãi.
                var hasPlate = await JS.InvokeAsync<bool>("vananGuardCamera.hasCapturedPhoto", "plate");
                var hasCustomer = await JS.InvokeAsync<bool>("vananGuardCamera.hasCapturedPhoto", "customer");

                if (!hasPlate)
                {
                    errorMessage = "Chưa chụp ảnh biển số. Hãy mở camera và chụp trước.";
                    return;
                }
                // #130: Ảnh khách là TÙY CHỌN — chỉ biển số mới bắt buộc.

                // 1. Get JWT + Gateway base URL for direct browser→Gateway fetch.
                // #130: JS sends photo to Gateway /api/guard/upload-photo via HTTP fetch.
                // Gateway uploads to R2 server-side — no R2 CORS needed, no base64 over SignalR.
                string jwtToken;
                string gatewayBaseUrl;
                try
                {
                    jwtToken = await GuardApi.GetJwtTokenAsync();
                    // #130: Use PUBLIC Gateway URL (https://api2.{domain}) for browser JS fetch.
                    // GatewayBaseUrl returns VPC internal IP (http://10.148.0.2:80) which browser
                    // CANNOT reach from internet → "Failed to fetch".
                    gatewayBaseUrl = GuardApi.PublicGatewayBaseUrl;
                    if (string.IsNullOrEmpty(gatewayBaseUrl))
                    {
                        Logger.LogWarning("Gateway:PublicBaseUrl not configured — falling back to internal URL (will fail in browser)");
                        gatewayBaseUrl = GuardApi.GatewayBaseUrl;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to get JWT for photo upload");
                    errorMessage = "Không lấy được token xác thực. Vui lòng thử lại.";
                    return;
                }

                // 2. Upload photos via Gateway API (server-side R2 upload, no CORS).
                // JS returns { success, key } — key is a small string, safe for SignalR.
                var plateResult = await JS.InvokeAsync<UploadResult>("vananGuardCamera.uploadCapturedPhoto", "plate", jwtToken, gatewayBaseUrl);
                if (!plateResult.Success)
                {
                    Logger.LogWarning("Plate photo upload failed: {Error}", plateResult.Error);
                    errorMessage = $"Upload ảnh biển số thất bại: {plateResult.Error}";
                    return;
                }
                // #130: Ảnh khách tùy chọn — chỉ upload nếu đã chụp.
                string? customerPhotoKey = null;
                if (hasCustomer)
                {
                    var customerResult = await JS.InvokeAsync<UploadResult>("vananGuardCamera.uploadCapturedPhoto", "customer", jwtToken, gatewayBaseUrl);
                    if (!customerResult.Success)
                    {
                        Logger.LogWarning("Customer photo upload failed: {Error}", customerResult.Error);
                        errorMessage = $"Upload ảnh khách thất bại: {customerResult.Error}";
                        return;
                    }
                    customerPhotoKey = customerResult.Key;
                }

                // 3. Issue QR session with uploaded photo keys
                IssueResultDto result;
                try
                {
                    result = await GuardApi.IssueAsync(new IssueRequestDto
                    {
                        PlateNumber = plateNumber.Trim(),
                        PlatePhotoKey = plateResult.Key,
                        CustomerPhotoKey = customerPhotoKey,
                        CustomerPhone = string.IsNullOrWhiteSpace(customerPhone) ? null : customerPhone.Trim()
                    }, cts.Token);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Issue QR failed for plate {Plate}", plateNumber);
                    errorMessage = $"Không tạo được phiên QR: {ex.Message}";
                    return;
                }

                issuedSessionId = result.SessionId;
                issuedShortCode = result.ShortCode;

                // 4. Generate QR image (client-side via vendored qrcode.js — no CDN dependency)
                // #130-fix3 (2026-08-18, Bug 1): Wrap JSON payload in URL so Zalo/external scanners
                // can open it as a deep link → opens KhachLink /qr/claim?data={base64(json)}.
                // Hash in DB is still SHA256(JSON) — Gateway extracts JSON from URL before hashing.
                var khachLinkUrl = Configuration.GetValue<string>("ExternalUrls:KhachLink") ?? "https://app2.khachvip.online";
                var qrUrl = $"{khachLinkUrl.TrimEnd('/')}/qr/claim?data={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(result.QrPayload))}";
                qrImageBase64 = await JS.InvokeAsync<string?>("vananGuardCamera.generateQrImage", qrUrl, 300) ?? string.Empty;

                issueStep = 2;
                // #130-fix: If QR image generation failed, show warning but still advance to step 2
                // so the short code is visible as fallback. Don't show "success" if QR image is missing.
                if (string.IsNullOrEmpty(qrImageBase64))
                {
                    successMessage = "Đã cấp QR thành công nhưng không hiển thị được ảnh QR. Khách có thể dùng mã ngắn bên dưới.";
                    Logger.LogWarning("QR image generation returned empty for session {SessionId}", result.SessionId);
                }
                else
                {
                    successMessage = "Đã cấp QR thành công!";
                }

                // 5. Clear JS-side photo storage + sessionStorage (issue complete).
                await JS.InvokeVoidAsync("vananGuardCamera.clearState");
            }
            catch (OperationCanceledException)
            {
                errorMessage = "Tạo QR quá thời gian (45s). Vui lòng thử lại.";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error issuing QR for plate {Plate}", plateNumber);
                errorMessage = $"Lỗi: {ex.Message}";
            }
            finally
            {
                issuing = false;
                // #130-fix: Defensive StateHasChanged — if circuit disconnected, this throws.
                // Catch silently to avoid "loading mãi" (UI stuck because finally itself failed).
                try { StateHasChanged(); }
                catch (Exception) { /* Circuit disconnected — reconnect UI will show. */ }
            }
        }

        private void ResetIssue()
        {
            issueStep = 1;
            plateNumber = string.Empty;
            customerPhone = string.Empty;
            qrImageBase64 = string.Empty;
            issuedShortCode = string.Empty;
            errorMessage = string.Empty;
            successMessage = string.Empty;
            StateHasChanged();
            // #126-fix2: Clear JS-side photo storage + sessionStorage + reset DOM (preview images, buttons).
            _ = JS.InvokeVoidAsync("vananGuardCamera.clearState");
            _ = JS.InvokeVoidAsync("vananGuardCamera._clearDOMPreview");
        }

        private async Task PrintTicketAsync()
        {
            // Sprint 5: Navigate to print ticket page (opens in new tab via forceLoad)
            if (issuedSessionId != Guid.Empty)
            {
                NavigationManager.NavigateTo($"/guard/print/{issuedSessionId}", forceLoad: true);
            }
            await Task.CompletedTask;
        }

        // === VERIFY: QR scan ===

        private async Task StartVerifyScan()
        {
            verifyScanning = true;
            errorMessage = string.Empty;
            StateHasChanged();
            var ok = await JS.InvokeAsync<bool>("vananQrScanner.startScanner", "guard-qr-reader", _dotNetRef);
            if (!ok)
            {
                verifyScanning = false;
                errorMessage = "Không bắt đầu được quét QR. Kiểm tra camera.";
                StateHasChanged();
            }
        }

        private async Task StopVerifyScan()
        {
            await JS.InvokeVoidAsync("vananQrScanner.stopScanner");
            verifyScanning = false;
        }

        /// <summary>JSInvokable callback from qr-scanner.js when QR is detected.</summary>
        [JSInvokable]
        public async Task OnQrScanned(string decodedText)
        {
            await StopVerifyScan();
            await DoVerifyAsync(decodedText);
        }

        /// <summary>JSInvokable callback from qr-scanner.js on error.</summary>
        [JSInvokable]
        public Task OnQrError(string error)
        {
            verifyScanning = false;
            errorMessage = $"Lỗi quét QR: {error}";
            StateHasChanged();
            return Task.CompletedTask;
        }

        private async Task VerifyManualAsync()
        {
            await DoVerifyAsync(manualQrPayload.Trim());
        }

        private async Task DoVerifyAsync(string qrPayload)
        {
            errorMessage = string.Empty;
            try
            {
                verifyResult = await GuardApi.VerifyAsync(qrPayload);
                verifyStep = 2;
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex, "Verify failed for QR payload");
                errorMessage = "Không tìm thấy phiên QR. Mã có thể không hợp lệ hoặc từ địa điểm khác.";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Verify error");
                errorMessage = $"Lỗi: {ex.Message}";
            }
            finally
            {
                StateHasChanged();
            }
        }

        private async Task CheckoutAsync()
        {
            if (verifyResult == null) return;
            actionLoading = true;
            try
            {
                await GuardApi.CheckoutAsync(verifyResult.SessionId);
                successMessage = "Đã check-out thành công!";
                ResetVerify();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Checkout failed for session {Id}", verifyResult.SessionId);
                errorMessage = $"Lỗi check-out: {ex.Message}";
            }
            finally
            {
                actionLoading = false;
                StateHasChanged();
            }
        }

        private async Task FlagAsync()
        {
            if (verifyResult == null) return;
            actionLoading = true;
            try
            {
                await GuardApi.FlagAsync(verifyResult.SessionId, "Bất khớp — guard báo nghi");
                successMessage = "Đã báo nghi phiên này.";
                ResetVerify();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Flag failed for session {Id}", verifyResult.SessionId);
                errorMessage = $"Lỗi báo nghi: {ex.Message}";
            }
            finally
            {
                actionLoading = false;
                StateHasChanged();
            }
        }

        private void ResetVerify()
        {
            verifyStep = 1;
            verifyResult = null;
            manualQrPayload = string.Empty;
            errorMessage = string.Empty;
            StateHasChanged();
        }

        // === TODAY: Stats + list ===

        private async Task LoadTodayAsync()
        {
            try
            {
                todaySessions = await GuardApi.GetTodaySessionsAsync(
                    string.IsNullOrWhiteSpace(todayStatusFilter) ? null : todayStatusFilter,
                    todayPage, TodayPageSize);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading today sessions");
                errorMessage = $"Lỗi tải danh sách: {ex.Message}";
            }
            finally
            {
                StateHasChanged();
            }
        }

        private async Task OnStatusFilterChanged(ChangeEventArgs e)
        {
            todayStatusFilter = e.Value?.ToString() ?? string.Empty;
            todayPage = 1;
            await LoadTodayAsync();
        }

        private async Task PrevPage()
        {
            if (todayPage > 1)
            {
                todayPage--;
                await LoadTodayAsync();
            }
        }

        private async Task NextPage()
        {
            todayPage++;
            await LoadTodayAsync();
        }

        private async Task ShowSessionDetail(Guid sessionId)
        {
            try
            {
                sessionDetail = await GuardApi.GetSessionAsync(sessionId);
                showDetailModal = true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading session detail {Id}", sessionId);
                errorMessage = $"Lỗi tải chi tiết: {ex.Message}";
            }
            finally
            {
                StateHasChanged();
            }
        }

        // === Helpers ===

        private static string TranslateStatus(string status) => status switch
        {
            "Issued" => "Đã cấp",
            "Claimed" => "Đã nhận",
            "CheckedOut" => "Đã ra",
            "Flagged" => "Báo nghi",
            "Voided" => "Đã hủy",
            _ => status
        };

        public void Dispose()
        {
            _ = JS.InvokeVoidAsync("vananGuardCamera.stopKeepAlive");
            _ = JS.InvokeVoidAsync("vananGuardCamera.stopCamera");
            _ = JS.InvokeVoidAsync("vananQrScanner.stopScanner");
            _dotNetRef?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
