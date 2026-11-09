/**
 * Onboarding Wizard (P1 — 2026-11-09): geolocation + Leaflet/MapLibre map
 * interop + camera capture cho /onboard self-serve merchant wizard (KhachLink WASM).
 * Leaflet + window.vananMap đã load global trong index.html
 * (/_content/VanAn.UI.Platform/lib/leaflet + js/realtime.js).
 *
 * Khác bản ShopERP: mỗi File đưa vào OCR cũng được gửi bytes về Blazor qua
 * OnPhotoFile(kind, bytes, fileName, contentType) để upload R2 (folder=onboarding).
 */
window.vananOnboardingWizard = (function () {
    'use strict';

    let _dotNetRef = null;

    /** Blazor đăng ký DotNetObjectReference để JS gọi lại (OnOcrResult/OnOcrError). */
    function register(ref) {
        _dotNetRef = ref;
        window.__ocrDotNetRef = ref;
    }

    /** Gửi bytes ảnh về Blazor để upload R2 — fire-and-forget, lỗi không chặn OCR. */
    async function _sendPhotoBytes(file, kind) {
        try {
            if (!file || !_dotNetRef) return;
            const buf = await file.arrayBuffer();
            const bytes = new Uint8Array(buf);
            await _dotNetRef.invokeMethodAsync('OnPhotoFile', kind, bytes, file.name || 'photo.jpg', file.type || 'image/jpeg');
        } catch (e) {
            console.warn('[OnboardingWizard] photo bytes notify fail:', e);
        }
    }

    /** Đọc 1 ảnh (file input) → gọi vananOnboardingOcr.<method>(file) → trả kết quả về Blazor.
     *  Đồng thời gửi bytes ảnh cho Blazor upload R2 (self-serve provenance). */
    async function processFile(file, methodName) {
        if (!file) return;
        const kind = methodName === 'extractGpdk' ? 'gpdk' : methodName === 'extractSignName' ? 'sign' : 'doc';
        await _sendPhotoBytes(file, kind);
        try {
            const result = await window.vananOnboardingOcr[methodName](file);
            await _dotNetRef.invokeMethodAsync('OnOcrResult', methodName, JSON.stringify(result));
        } catch (e) {
            console.error('[OnboardingWizard] OCR fail:', e);
            await _dotNetRef.invokeMethodAsync('OnOcrError', methodName, e.message);
        }
    }

    /** Đọc nhiều ảnh menu → extractMenu → trả kết quả về Blazor. Upload từng ảnh lên R2. */
    async function processMenuFiles(files) {
        const list = Array.from(files || []);
        if (!list.length) return;
        for (const f of list) { await _sendPhotoBytes(f, 'menu'); }
        try {
            const result = await window.vananOnboardingOcr.extractMenu(list);
            await _dotNetRef.invokeMethodAsync('OnOcrResult', 'extractMenu', JSON.stringify(result));
        } catch (e) {
            console.error('[OnboardingWizard] Menu OCR fail:', e);
            await _dotNetRef.invokeMethodAsync('OnOcrError', 'extractMenu', e.message);
        }
    }

    /** Promise-based navigator.geolocation. Resolve {lat, lng, accuracy}. */
    function getCurrentPosition() {
        return new Promise((resolve, reject) => {
            if (!navigator.geolocation) {
                reject(new Error('Trình duyệt không hỗ trợ định vị. Nhập tọa độ tay.'));
                return;
            }
            navigator.geolocation.getCurrentPosition(
                (pos) => resolve({
                    lat: pos.coords.latitude,
                    lng: pos.coords.longitude,
                    accuracy: pos.coords.accuracy || 0
                }),
                (err) => reject(new Error(`GPS lỗi (${err.code}): ${err.message}`)),
                { enableHighAccuracy: true, timeout: 15000, maximumAge: 30000 }
            );
        });
    }

    let _map = null;
    let _mapEngine = null;

    /** GeoJSON polygon xấp xỉ vòng tròn accuracy cho MapLibre (không có L.circle). */
    function _accuracyGeoJson(lat, lng, radiusMeters) {
        const pts = 64, ring = [];
        const dLat = radiusMeters / 111320;
        const dLng = radiusMeters / (111320 * Math.cos(lat * Math.PI / 180));
        for (let i = 0; i <= pts; i++) {
            const a = (i / pts) * Math.PI * 2;
            ring.push([lng + dLng * Math.cos(a), lat + dLat * Math.sin(a)]);
        }
        return { type: 'Feature', geometry: { type: 'Polygon', coordinates: [ring] } };
    }

    /** Init/refresh map với marker + vòng tròn accuracy. Trả map instance.
     *  2026-11-08: MapLibre + OpenFreeMap (vector ~Google look, free) trước,
     *  fallback Leaflet Voyager chain khi lib/style fail.
     *  RV fix 2026-10-26: L.map(el) lần 2 trên cùng element ném "Map container is
     *  already initialized" (Leaflet giữ _leaflet_id — innerHTML='' không xóa được).
     *  → map.remove() trước khi tạo lại. */
    async function initMap(elementId, lat, lng, accuracy) {
        const el = document.getElementById(elementId);
        if (!el) return null;
        if (_map) { _map.remove(); _map = null; _mapEngine = null; }
        el.innerHTML = '';

        // MapLibre + OpenFreeMap (vector, ~Google look) — engine chính.
        if (window.vananMap?.createMapLibre) {
            const mlMap = await window.vananMap.createMapLibre(elementId, lat, lng, 17);
            if (mlMap) {
                _map = mlMap;
                _mapEngine = 'maplibre';
                new maplibregl.Marker({ color: '#ea4335' }).setLngLat([lng, lat]).addTo(mlMap);
                if (accuracy > 0) {
                    // addSource/addLayer cần style đã load → chờ 'load'.
                    mlMap.once('load', () => {
                        mlMap.addSource('ocr-acc', { type: 'geojson', data: _accuracyGeoJson(lat, lng, accuracy) });
                        mlMap.addLayer({ id: 'ocr-acc-fill', type: 'fill', source: 'ocr-acc', paint: { 'fill-color': '#2563eb', 'fill-opacity': 0.15 } });
                        mlMap.addLayer({ id: 'ocr-acc-line', type: 'line', source: 'ocr-acc', paint: { 'line-color': '#2563eb', 'line-width': 1.5 } });
                    });
                }
                return _map;
            }
        }

        // Fallback Leaflet (Voyager-first chain).
        if (typeof L === 'undefined') return null;
        _map = L.map(elementId, { zoomControl: true }).setView([lat, lng], 17);
        _mapEngine = 'leaflet';
        // 2026-11-08: dùng tile chain chung (CARTO Voyager → Esri → OSM + fallback) từ
        // realtime.js thay vì OSM đơn — OSM bị chặn trên một số mạng VN → map xám.
        if (window.vananMap?.attachTiles) {
            window.vananMap.attachTiles(_map);
        } else {
            L.tileLayer('https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png', {
                attribution: '&copy; OpenStreetMap contributors &copy; CARTO',
                subdomains: 'abcd', maxZoom: 19
            }).addTo(_map);
        }
        L.marker([lat, lng]).addTo(_map);
        if (accuracy > 0) {
            L.circle([lat, lng], { radius: accuracy, color: '#2563eb', fillOpacity: 0.15 }).addTo(_map);
        }
        // Invalidate size sau khi hiện tab (map trong tab ẩn bị sai kích thước)
        setTimeout(() => _map?.invalidateSize(), 100);
        return _map;
    }

    // ── Camera scan mode (2026-10-26 user directive) ──────────────────────
    let _stream = null;
    let _menuShots = [];

    /** Mở camera sau (facingMode environment) — bám vào video element. */
    async function openCamera(videoId) {
        closeCamera();
        const video = document.getElementById(videoId);
        if (!video) throw new Error('Không tìm thấy video element');
        if (!navigator.mediaDevices?.getUserMedia) throw new Error('Trình duyệt không hỗ trợ camera');
        _stream = await navigator.mediaDevices.getUserMedia({
            video: { facingMode: 'environment' },
            audio: false
        });
        video.srcObject = _stream;
        await video.play().catch(() => {});
        return true;
    }

    function closeCamera() {
        if (_stream) { _stream.getTracks().forEach((t) => t.stop()); _stream = null; }
        const video = document.getElementById('ocrCameraVideo');
        if (video) video.srcObject = null;
        _menuShots = [];
    }

    /** Chụp 1 frame từ video → File (để OCR nhận qua bridge processFile). */
    function capturePhoto(videoId) {
        const video = document.getElementById(videoId);
        if (!video || !video.videoWidth) return Promise.resolve(null);
        const canvas = document.createElement('canvas');
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        canvas.getContext('2d').drawImage(video, 0, 0);
        return new Promise((resolve) => {
            canvas.toBlob((blob) => {
                if (!blob) { resolve(null); return; }
                resolve(new File([blob], 'capture.jpg', { type: 'image/jpeg' }));
            }, 'image/jpeg', 0.92);
        });
    }

    /** Chụp 1 ảnh đơn (GPKD/biển hiệu) → OCR ngay. mode: 'gpdk' | 'sign'. */
    async function captureAndOcr(mode) {
        try {
            const file = await capturePhoto('ocrCameraVideo');
            if (!file) { await _dotNetRef.invokeMethodAsync('OnOcrError', mode, 'Chụp thất bại — thử lại.'); return; }
            const method = mode === 'gpdk' ? 'extractGpdk' : 'extractSignName';
            await processFile(file, method);
        } catch (e) {
            console.error('[OnboardingWizard] captureAndOcr fail:', e);
            await _dotNetRef.invokeMethodAsync('OnOcrError', mode, e.message);
        }
    }

    /** Chụp 1 shot menu — tích lũy (chưa OCR — chờ "Xong" để gộp dedupe header). */
    async function captureMenuShot() {
        try {
            const file = await capturePhoto('ocrCameraVideo');
            if (!file) { await _dotNetRef.invokeMethodAsync('OnOcrError', 'extractMenu', 'Chụp thất bại — thử lại.'); return; }
            _menuShots.push(file);
            await _dotNetRef.invokeMethodAsync('OnMenuShotCount', _menuShots.length);
        } catch (e) {
            console.error('[OnboardingWizard] captureMenuShot fail:', e);
            await _dotNetRef.invokeMethodAsync('OnOcrError', 'extractMenu', e.message);
        }
    }

    /** Kết thúc chụp menu → OCR TOÀN BỘ shots (dedupe header/footer chéo ảnh). */
    async function finishMenuCapture() {
        if (!_menuShots.length) {
            await _dotNetRef.invokeMethodAsync('OnOcrError', 'extractMenu', 'Chưa có ảnh nào — chụp ít nhất 1 shot.');
            return;
        }
        const shots = _menuShots;
        _menuShots = [];
        await processMenuFiles(shots);
    }

    return { register, processFile, processMenuFiles, getCurrentPosition, initMap, openCamera, closeCamera, captureAndOcr, captureMenuShot, finishMenuCapture };
})();