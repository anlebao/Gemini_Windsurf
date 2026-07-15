// QR Scanner JavaScript functions using html5-qrcode library
let html5QrCode = null;

// Check camera permission
async function checkCameraPermission() {
    try {
        // Check if we can access camera
        const stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } });
        stream.getTracks().forEach(track => track.stop());
        return true;
    } catch (error) {
        console.log('Camera permission check failed:', error);
        return false;
    }
}

// Request camera permission
async function requestCameraPermission() {
    try {
        const stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } });
        stream.getTracks().forEach(track => track.stop());
        return true;
    } catch (error) {
        console.error('Camera permission request failed:', error);
        throw error;
    }
}

// Start QR scanner
async function startQRScanner(dotNetRef) {
    try {
        if (typeof Html5Qrcode === 'undefined') {
            throw new Error('Thư viện html5-qrcode chưa tải được. Vui lòng tải lại trang.');
        }

        // Clear any existing scanner
        if (html5QrCode) {
            try {
                await html5QrCode.stop();
                html5QrCode.clear();
            } catch (e) { /* ignore */ }
            html5QrCode = null;
        }

        // Wait for DOM element to be rendered (Blazor Server timing fix)
        let container = document.getElementById('qr-reader');
        if (!container) {
            for (let i = 0; i < 10; i++) {
                await new Promise(resolve => setTimeout(resolve, 100));
                container = document.getElementById('qr-reader');
                if (container) break;
            }
        }
        if (!container) {
            throw new Error('Không tìm thấy vùng hiển thị camera');
        }

        html5QrCode = new Html5Qrcode("qr-reader");

        // Mobile-optimized config — smaller scan box for small screens
        const isMobile = window.innerWidth < 768;
        const qrboxSize = isMobile ? 200 : 250;

        const config = {
            fps: 10,
            qrbox: { width: qrboxSize, height: qrboxSize },
            aspectRatio: isMobile ? 1.0 : 1.333
        };

        // Try environment camera first, fallback to any camera
        let started = false;
        try {
            await html5QrCode.start(
                { facingMode: 'environment' },
                config,
                (decodedText) => {
                    // QR code detected successfully
                    dotNetRef.invokeMethodAsync('OnQRDetected', decodedText);
                },
                (errorMessage) => {
                    // Ignore scanning errors during normal operation
                    console.log('QR scan error:', errorMessage);
                }
            );
            started = true;
        } catch (envError) {
            console.warn('Environment camera failed, trying default camera:', envError);
        }

        if (!started) {
            await html5QrCode.start(
                { facingMode: 'user' },
                config,
                (decodedText) => {
                    dotNetRef.invokeMethodAsync('OnQRDetected', decodedText);
                },
                (errorMessage) => {
                    console.log('QR scan error:', errorMessage);
                }
            );
        }
    } catch (error) {
        console.error('Failed to start QR scanner:', error);
        // Notify Blazor of the error so UI can show it
        try {
            dotNetRef.invokeMethodAsync('OnQRError', error.message || 'Không thể khởi động camera');
        } catch (e) { /* ignore */ }
        throw error;
    }
}

// Stop QR scanner
async function stopQRScanner() {
    try {
        if (html5QrCode) {
            await html5QrCode.stop();
            html5QrCode.clear();
            html5QrCode = null;
        }
    } catch (error) {
        console.error('Failed to stop QR scanner:', error);
        throw error;
    }
}

// Make functions globally available
window.checkCameraPermission = checkCameraPermission;
window.requestCameraPermission = requestCameraPermission;
window.startQRScanner = startQRScanner;
window.stopQRScanner = stopQRScanner;