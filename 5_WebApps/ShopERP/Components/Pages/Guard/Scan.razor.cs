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
            if (!canIssue) return;
            issuing = true;
            errorMessage = string.Empty;
            successMessage = string.Empty;
            try
            {
                // #126-fix2: Read captured photos from JS-side storage (survives circuit disconnect).
                var platePhotoDataUrl = await JS.InvokeAsync<string?>("vananGuardCamera.getCapturedPhoto", "plate");
                var customerPhotoDataUrl = await JS.InvokeAsync<string?>("vananGuardCamera.getCapturedPhoto", "customer");

                if (string.IsNullOrEmpty(platePhotoDataUrl))
                {
                    errorMessage = "Chưa chụp ảnh biển số. Hãy mở camera và chụp trước.";
                    return;
                }
                if (string.IsNullOrEmpty(customerPhotoDataUrl))
                {
                    errorMessage = "Chưa chụp ảnh khách. Hãy mở camera và chụp trước.";
                    return;
                }

                // 1. Get presigned upload URLs
                var presign = await GuardApi.PresignUploadAsync();
                if (string.IsNullOrEmpty(presign.PlatePhotoUploadUrl) || string.IsNullOrEmpty(presign.CustomerPhotoUploadUrl))
                {
                    errorMessage = "Không lấy được URL upload ảnh.";
                    return;
                }

                // 2. Upload photos to R2 via presigned PUT
                var plateOk = await JS.InvokeAsync<bool>("vananGuardCamera.uploadToPresignedUrl", platePhotoDataUrl, presign.PlatePhotoUploadUrl);
                var customerOk = await JS.InvokeAsync<bool>("vananGuardCamera.uploadToPresignedUrl", customerPhotoDataUrl, presign.CustomerPhotoUploadUrl);
                if (!plateOk || !customerOk)
                {
                    errorMessage = "Upload ảnh thất bại. Vui lòng thử lại.";
                    return;
                }

                // 3. Issue QR session
                var result = await GuardApi.IssueAsync(new IssueRequestDto
                {
                    PlateNumber = plateNumber.Trim(),
                    PlatePhotoKey = presign.PlatePhotoKey,
                    CustomerPhotoKey = presign.CustomerPhotoKey,
                    CustomerPhone = string.IsNullOrWhiteSpace(customerPhone) ? null : customerPhone.Trim()
                });

                issuedSessionId = result.SessionId;
                issuedShortCode = result.ShortCode;

                // 4. Generate QR image (client-side via qrcode.js)
                qrImageBase64 = await JS.InvokeAsync<string?>("vananGuardCamera.generateQrImage", result.QrPayload, 300) ?? string.Empty;

                issueStep = 2;
                successMessage = "Đã cấp QR thành công!";

                // 5. Clear JS-side photo storage + sessionStorage (issue complete).
                await JS.InvokeVoidAsync("vananGuardCamera.clearState");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error issuing QR for plate {Plate}", plateNumber);
                errorMessage = $"Lỗi: {ex.Message}";
            }
            finally
            {
                issuing = false;
                StateHasChanged();
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
