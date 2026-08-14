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
                await LoadTodayAsync();
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
                StateHasChanged();

                // #126 OCR: auto-recognize plate text from the captured photo (client-side Tesseract.js).
                ocrLoading = true;
                ocrHint = string.Empty;
                StateHasChanged();
                try
                {
                    var plate = await JS.InvokeAsync<string?>("vananGuardCamera.recognizePlate", platePhotoDataUrl);
                    if (!string.IsNullOrWhiteSpace(plate))
                    {
                        plateNumber = plate;
                        ocrHint = $"Đã nhận diện biển số: {plate} — vui lòng kiểm tra lại trước khi tạo QR.";
                    }
                    else
                    {
                        ocrHint = "Không nhận diện được biển số — vui lòng nhập thủ công.";
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Plate OCR failed");
                    ocrHint = "Nhận diện OCR lỗi — vui lòng nhập biển số thủ công.";
                }
                finally
                {
                    ocrLoading = false;
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "CapturePlatePhoto failed");
                errorMessage = $"Lỗi chụp ảnh: {ex.Message}";
                StateHasChanged();
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
            _ = JS.InvokeVoidAsync("vananGuardCamera.stopCamera");
            _ = JS.InvokeVoidAsync("vananQrScanner.stopScanner");
            _dotNetRef?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
