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
        private int issueStep = 1;
        private string plateNumber = string.Empty;
        private string customerPhone = string.Empty;
        private bool cameraActive_plate = false;
        private bool cameraActive_customer = false;
        private string? platePhotoPreview;
        private string? customerPhotoPreview;
        private string? platePhotoDataUrl;
        private string? customerPhotoDataUrl;
        private string? platePhotoKey;
        private string? customerPhotoKey;
        private bool issuing = false;
        private string qrImageBase64 = string.Empty;
        private string issuedShortCode = string.Empty;
        private Guid issuedSessionId;

        // === OCR (license plate auto-fill) ===
        private bool ocrLoading = false;
        private string ocrHint = string.Empty;

        // === Blazor interactivity guard (Gate 2 Category C) ===
        // Disable camera buttons until Blazor SignalR is fully interactive — prevents
        // click-during-prerender race that causes page reload before capture runs.
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

        private bool canIssue =>
            !string.IsNullOrWhiteSpace(plateNumber)
            && !string.IsNullOrEmpty(platePhotoKey)
            && !string.IsNullOrEmpty(customerPhotoKey);

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                isInteractive = true;
                _dotNetRef = DotNetObjectReference.Create(this);
                StateHasChanged();

                // #126-fix: Start circuit keep-alive ping (15s) to prevent idle disconnect.
                await JS.InvokeVoidAsync("vananGuardCamera.startKeepAlive", _dotNetRef);

                // #126-fix: Restore state from sessionStorage (survive circuit disconnect reload).
                // Photos + plate number + camera state are restored BEFORE user sees anything,
                // so no visible flicker. Camera auto-reopens if it was active before reload.
                await RestoreStateFromSessionAsync();
            }
        }

        /// <summary>#126-fix: No-op method invoked by JS keep-alive ping every 15s.</summary>
        [JSInvokable]
        public Task KeepAlivePingAsync()
        {
            // Empty — just keeps SignalR circuit active. No StateHasChanged (would cause re-render).
            return Task.CompletedTask;
        }

        /// <summary>#126-fix: Restore persisted state from sessionStorage after page reload.</summary>
        private async Task RestoreStateFromSessionAsync()
        {
            try
            {
                var savedPlatePhoto = await JS.InvokeAsync<string?>("vananGuardCamera.loadState", "platePhoto");
                var savedCustomerPhoto = await JS.InvokeAsync<string?>("vananGuardCamera.loadState", "customerPhoto");
                var savedPlateNumber = await JS.InvokeAsync<string?>("vananGuardCamera.loadState", "plateNumber");
                var savedCustomerPhone = await JS.InvokeAsync<string?>("vananGuardCamera.loadState", "customerPhone");
                var savedOcrHint = await JS.InvokeAsync<string?>("vananGuardCamera.loadState", "ocrHint");
                var savedCameraPlate = await JS.InvokeAsync<string?>("vananGuardCamera.loadState", "cameraPlateActive");
                var savedCameraCustomer = await JS.InvokeAsync<string?>("vananGuardCamera.loadState", "cameraCustomerActive");

                bool restored = false;

                if (!string.IsNullOrEmpty(savedPlatePhoto))
                {
                    platePhotoDataUrl = savedPlatePhoto;
                    platePhotoPreview = savedPlatePhoto;
                    restored = true;
                }
                if (!string.IsNullOrEmpty(savedCustomerPhoto))
                {
                    customerPhotoDataUrl = savedCustomerPhoto;
                    customerPhotoPreview = savedCustomerPhoto;
                    restored = true;
                }
                if (!string.IsNullOrEmpty(savedPlateNumber))
                {
                    plateNumber = savedPlateNumber;
                    restored = true;
                }
                if (!string.IsNullOrEmpty(savedCustomerPhone))
                {
                    customerPhone = savedCustomerPhone;
                    restored = true;
                }
                if (!string.IsNullOrEmpty(savedOcrHint))
                {
                    ocrHint = savedOcrHint;
                    restored = true;
                }

                StateHasChanged();

                // Auto-reopen camera silently if it was active before reload and no photo captured yet.
                // Use a small delay to let the <video> element render first.
                if (savedCameraPlate == "1" && string.IsNullOrEmpty(platePhotoDataUrl))
                {
                    _ = Task.Delay(300).ContinueWith(async _ =>
                    {
                        await InvokeAsync(async () => await StartPlateCamera());
                    });
                    restored = true;
                }
                if (savedCameraCustomer == "1" && string.IsNullOrEmpty(customerPhotoDataUrl))
                {
                    _ = Task.Delay(500).ContinueWith(async _ =>
                    {
                        await InvokeAsync(async () => await StartCustomerCamera());
                    });
                    restored = true;
                }

                if (restored)
                {
                    Logger.LogInformation("Guard Scan state restored from sessionStorage after page reload");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to restore Guard Scan state from sessionStorage");
            }
        }

        /// <summary>#126-fix: Persist current state to sessionStorage.</summary>
        private async Task PersistStateToSessionAsync()
        {
            try
            {
                await JS.InvokeVoidAsync("vananGuardCamera.saveState", "platePhoto", platePhotoDataUrl ?? "");
                await JS.InvokeVoidAsync("vananGuardCamera.saveState", "customerPhoto", customerPhotoDataUrl ?? "");
                await JS.InvokeVoidAsync("vananGuardCamera.saveState", "plateNumber", plateNumber ?? "");
                await JS.InvokeVoidAsync("vananGuardCamera.saveState", "customerPhone", customerPhone ?? "");
                await JS.InvokeVoidAsync("vananGuardCamera.saveState", "ocrHint", ocrHint ?? "");
                await JS.InvokeVoidAsync("vananGuardCamera.saveState", "cameraPlateActive", cameraActive_plate ? "1" : "0");
                await JS.InvokeVoidAsync("vananGuardCamera.saveState", "cameraCustomerActive", cameraActive_customer ? "1" : "0");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to persist Guard Scan state to sessionStorage");
            }
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

        private async Task StartPlateCamera()
        {
            if (!isInteractive) return;
            try
            {
                var ok = await JS.InvokeAsync<bool>("vananGuardCamera.startCamera", "plateVideo", "environment");
                cameraActive_plate = ok;
                if (!ok) errorMessage = "Không mở được camera. Kiểm tra quyền truy cập camera.";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "StartPlateCamera failed");
                errorMessage = $"Lỗi mở camera: {ex.Message}";
            }
            StateHasChanged();
        }

        private async Task StartCustomerCamera()
        {
            if (!isInteractive) return;
            try
            {
                var ok = await JS.InvokeAsync<bool>("vananGuardCamera.startCamera", "customerVideo", "user");
                cameraActive_customer = ok;
                if (!ok) errorMessage = "Không mở được camera. Kiểm tra quyền truy cập camera.";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "StartCustomerCamera failed");
                errorMessage = $"Lỗi mở camera: {ex.Message}";
            }
            StateHasChanged();
        }

        private async Task CapturePlatePhoto()
        {
            if (!isInteractive) return;
            try
            {
                var dataUrl = await JS.InvokeAsync<string?>("vananGuardCamera.capturePhoto", "plateVideo");
                if (string.IsNullOrEmpty(dataUrl))
                {
                    errorMessage = "Chụp ảnh thất bại. Đảm bảo camera đã mở và có hình.";
                    StateHasChanged();
                    return;
                }
                platePhotoDataUrl = dataUrl;
                platePhotoPreview = dataUrl;
                await StopPlateCamera();
                ocrHint = string.Empty;
                StateHasChanged();
                await PersistStateToSessionAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "CapturePlatePhoto failed");
                errorMessage = $"Lỗi chụp ảnh: {ex.Message}";
                StateHasChanged();
            }
        }

        /// <summary>OCR button — runs Tesseract.js separately from capture to avoid circuit timeout.</summary>
        private async Task RecognizePlateAsync()
        {
            if (string.IsNullOrEmpty(platePhotoDataUrl)) return;
            ocrLoading = true;
            ocrHint = string.Empty;
            StateHasChanged();
            try
            {
                var plate = await JS.InvokeAsync<string?>("vananGuardCamera.recognizePlate", platePhotoDataUrl);
                if (!string.IsNullOrWhiteSpace(plate))
                {
                    plateNumber = plate;
                    ocrHint = $"Đã nhận diện: {plate} — kiểm tra lại trước khi tạo QR.";
                }
                else
                {
                    ocrHint = "Không nhận diện được — nhập thủ công.";
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Plate OCR failed");
                ocrHint = "OCR lỗi — nhập biển số thủ công.";
            }
            finally
            {
                ocrLoading = false;
                StateHasChanged();
                await PersistStateToSessionAsync();
            }
        }

        private async Task CaptureCustomerPhoto()
        {
            if (!isInteractive) return;
            try
            {
                var dataUrl = await JS.InvokeAsync<string?>("vananGuardCamera.capturePhoto", "customerVideo");
                if (string.IsNullOrEmpty(dataUrl))
                {
                    errorMessage = "Chụp ảnh thất bại. Đảm bảo camera đã mở và có hình.";
                    StateHasChanged();
                    return;
                }
                customerPhotoDataUrl = dataUrl;
                customerPhotoPreview = dataUrl;
                await StopCustomerCamera();
                StateHasChanged();
                await PersistStateToSessionAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "CaptureCustomerPhoto failed");
                errorMessage = $"Lỗi chụp ảnh: {ex.Message}";
                StateHasChanged();
            }
        }

        private async Task StopPlateCamera()
        {
            await JS.InvokeVoidAsync("vananGuardCamera.stopCamera");
            cameraActive_plate = false;
        }

        private async Task StopCustomerCamera()
        {
            await JS.InvokeVoidAsync("vananGuardCamera.stopCamera");
            cameraActive_customer = false;
        }

        // === ISSUE: Create QR ===

        private async Task IssueQrAsync()
        {
            if (!canIssue) return;
            issuing = true;
            errorMessage = string.Empty;
            successMessage = string.Empty;
            try
            {
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
            platePhotoPreview = null;
            customerPhotoPreview = null;
            platePhotoDataUrl = null;
            customerPhotoDataUrl = null;
            platePhotoKey = null;
            customerPhotoKey = null;
            qrImageBase64 = string.Empty;
            issuedShortCode = string.Empty;
            ocrHint = string.Empty;
            ocrLoading = false;
            errorMessage = string.Empty;
            successMessage = string.Empty;
            StateHasChanged();
            // #126-fix: Clear persisted state on explicit reset (user chose to start over).
            _ = JS.InvokeVoidAsync("vananGuardCamera.clearState");
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
