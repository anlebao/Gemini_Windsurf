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
            throw new Error('html5-qrcode library is not loaded');
        }

        html5QrCode = new Html5Qrcode("qr-reader");
        
        const config = {
            fps: 10,
            qrbox: { width: 250, height: 250 },
            aspectRatio: 1.0
        };

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
    } catch (error) {
        console.error('Failed to start QR scanner:', error);
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