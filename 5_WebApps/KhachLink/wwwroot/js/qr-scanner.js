// QR Scanner — BarcodeDetector native (Chrome Android) + jsQR fallback (iOS/other browsers)
// Falls back to html5-qrcode if both unavailable.
// Performance: native BarcodeDetector is 3-5x faster than html5-qrcode (no canvas copy per frame).

let scanStream = null;
let scanVideo = null;
let scanCanvas = null;
let scanCtx = null;
let scanRAF = null;
let scanRunning = false;
let scanMode = 'native'; // 'native' | 'jsqr' | 'html5'
let html5QrCode = null;
let lastDetectedTime = 0;

// Check camera permission
async function checkCameraPermission() {
    try {
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

// Detect best scan mode available
function detectScanMode() {
    if ('BarcodeDetector' in window) return 'native';
    if (typeof jsQR !== 'undefined') return 'jsqr';
    if (typeof Html5Qrcode !== 'undefined') return 'html5';
    return 'none';
}

// Start QR scanner — picks fastest available mode
async function startQRScanner(dotNetRef) {
    const mode = detectScanMode();
    if (mode === 'none') {
        dotNetRef.invokeMethodAsync('OnQRError', 'Trình duyệt không hỗ trợ quét QR. Vui lòng dùng Chrome/Android hoặc cập nhật trình duyệt.');
        return;
    }
    scanMode = mode;
    console.log('QR scan mode:', scanMode);

    // Wait for DOM element
    let container = document.getElementById('qr-reader');
    if (!container) {
        for (let i = 0; i < 10; i++) {
            await new Promise(resolve => setTimeout(resolve, 100));
            container = document.getElementById('qr-reader');
            if (container) break;
        }
    }
    if (!container) {
        dotNetRef.invokeMethodAsync('OnQRError', 'Không tìm thấy vùng hiển thị camera');
        return;
    }

    try {
        if (scanMode === 'native' || scanMode === 'jsqr') {
            await startNativeOrJsQR(container, dotNetRef);
        } else {
            await startHtml5Qrcode(container, dotNetRef);
        }
    } catch (error) {
        console.error('Failed to start QR scanner:', error);
        try { dotNetRef.invokeMethodAsync('OnQRError', error.message || 'Không thể khởi động camera'); } catch (e) { /* ignore */ }
        throw error;
    }
}

// Native BarcodeDetector or jsQR — both use <video> + canvas, just different decode methods
async function startNativeOrJsQR(container, dotNetRef) {
    // Stop any existing stream
    await stopQRScanner();

    scanVideo = document.createElement('video');
    scanVideo.style.cssText = 'width:100%;height:100%;object-fit:cover;border-radius:8px;display:block;';
    scanVideo.setAttribute('playsinline', 'true');
    scanVideo.setAttribute('muted', 'true');
    container.innerHTML = '';
    container.appendChild(scanVideo);

    // Request camera — prefer 1280x720 for fast decode, fallback to default
    const constraints = {
        video: {
            facingMode: { ideal: 'environment' },
            width: { ideal: 1280 },
            height: { ideal: 720 }
        }
    };

    try {
        scanStream = await navigator.mediaDevices.getUserMedia(constraints);
    } catch (e) {
        // Fallback: any camera
        scanStream = await navigator.mediaDevices.getUserMedia({ video: true });
    }

    scanVideo.srcObject = scanStream;
    await scanVideo.play();

    scanRunning = true;
    scanCanvas = document.createElement('canvas');
    scanCtx = scanCanvas.getContext('2d', { willReadFrequently: true });

    // Native BarcodeDetector instance
    let detector = null;
    if (scanMode === 'native') {
        try {
            detector = new BarcodeDetector({ formats: ['qr_code'] });
        } catch (e) {
            console.warn('BarcodeDetector init failed, falling back to jsQR:', e);
            scanMode = typeof jsQR !== 'undefined' ? 'jsqr' : 'html5';
            if (scanMode === 'html5') {
                await stopQRScanner();
                return startHtml5Qrcode(container, dotNetRef);
            }
        }
    }

    // Scan loop — requestAnimationFrame for smooth, high-FPS scanning
    const scanFrame = async () => {
        if (!scanRunning || !scanVideo || scanVideo.readyState < 2) {
            if (scanRunning) scanRAF = requestAnimationFrame(scanFrame);
            return;
        }

        // Throttle: max 30fps to avoid CPU overload
        const now = performance.now();
        if (now - lastDetectedTime < 33) {
            scanRAF = requestAnimationFrame(scanFrame);
            return;
        }
        lastDetectedTime = now;

        try {
            let decodedText = null;

            if (scanMode === 'native' && detector) {
                // Native: detect from video element directly (no canvas copy!)
                const codes = await detector.detect(scanVideo);
                if (codes && codes.length > 0) {
                    decodedText = codes[0].rawValue;
                }
            } else if (scanMode === 'jsqr') {
                // jsQR: copy video frame to canvas, then decode
                const w = scanVideo.videoWidth || 640;
                const h = scanVideo.videoHeight || 480;
                // Downscale for speed: max 640px wide
                const scale = Math.min(1, 640 / w);
                scanCanvas.width = w * scale;
                scanCanvas.height = h * scale;
                scanCtx.drawImage(scanVideo, 0, 0, scanCanvas.width, scanCanvas.height);
                const imageData = scanCtx.getImageData(0, 0, scanCanvas.width, scanCanvas.height);
                const code = jsQR(imageData.data, imageData.width, imageData.height, { inversionAttempts: 'dontInvert' });
                if (code) decodedText = code.data;
            }

            if (decodedText) {
                scanRunning = false;
                dotNetRef.invokeMethodAsync('OnQRDetected', decodedText);
                await stopQRScanner();
                return;
            }
        } catch (e) {
            // Ignore frame errors — keep scanning
        }

        if (scanRunning) scanRAF = requestAnimationFrame(scanFrame);
    };

    scanRAF = requestAnimationFrame(scanFrame);
}

// Fallback: html5-qrcode library (slowest but most compatible)
async function startHtml5Qrcode(container, dotNetRef) {
    await stopQRScanner();

    if (typeof Html5Qrcode === 'undefined') {
        dotNetRef.invokeMethodAsync('OnQRError', 'Thư viện html5-qrcode chưa tải được. Vui lòng tải lại trang.');
        return;
    }

    container.innerHTML = '';
    html5QrCode = new Html5Qrcode('qr-reader');

    const isMobile = window.innerWidth < 768;
    const config = {
        fps: 15,
        qrbox: { width: isMobile ? 240 : 280, height: isMobile ? 240 : 280 },
        aspectRatio: isMobile ? 1.0 : 1.333
    };

    const onDetected = (decodedText) => {
        dotNetRef.invokeMethodAsync('OnQRDetected', decodedText);
    };
    const onError = (errorMessage) => { console.log('QR scan error:', errorMessage); };

    try {
        await html5QrCode.start({ facingMode: 'environment' }, config, onDetected, onError);
    } catch (e) {
        await html5QrCode.start({ facingMode: 'user' }, config, onDetected, onError);
    }
}

// Stop QR scanner — cleanup all resources
async function stopQRScanner() {
    scanRunning = false;
    if (scanRAF) { cancelAnimationFrame(scanRAF); scanRAF = null; }

    if (scanStream) {
        scanStream.getTracks().forEach(t => t.stop());
        scanStream = null;
    }
    if (scanVideo) {
        try { scanVideo.pause(); } catch (e) { /* ignore */ }
        scanVideo.srcObject = null;
        scanVideo = null;
    }
    scanCanvas = null;
    scanCtx = null;

    if (html5QrCode) {
        try {
            await html5QrCode.stop();
            html5QrCode.clear();
        } catch (e) { /* ignore */ }
        html5QrCode = null;
    }
}

// Make functions globally available
window.checkCameraPermission = checkCameraPermission;
window.requestCameraPermission = requestCameraPermission;
window.startQRScanner = startQRScanner;
window.stopQRScanner = stopQRScanner;
