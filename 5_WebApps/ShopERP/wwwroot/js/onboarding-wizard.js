/**
 * Onboarding Wizard S4 (2026-10-25): geolocation + Leaflet map interop
 * cho /admin/tenants tab "Onboarding OCR".
 * Leaflet đã load global (App.razor — _content/VanAn.UI.Platform/lib/leaflet).
 */
window.vananOnboardingWizard = (function () {
    'use strict';

    let _dotNetRef = null;
    let _uploadBaseUrl = null;   // P3: public Gateway URL cho browser→Gateway image upload

    /** Blazor đăng ký DotNetObjectReference để JS gọi lại (OnOcrResult/OnOcrError). */
    function register(ref) {
        _dotNetRef = ref;
        window.__ocrDotNetRef = ref;
    }

    /** P3 (2026-11-09): set public Gateway base URL (Gateway:PublicBaseUrl) —
     *  browser fetch trực tiếp POST /api/v1/images/upload?folder=onboarding (AllowAnonymous). */
    function configureUpload(publicGatewayBaseUrl) {
        _uploadBaseUrl = (publicGatewayBaseUrl || '').replace(/\/+$/, '');
    }

    /** Upload 1 ảnh → Cloudinary qua Gateway. Trả URL hoặc null (fail không chặn OCR). */
    async function _uploadPhoto(file) {
        if (!_uploadBaseUrl || !file) return null;
        try {
            const form = new FormData();
            form.append('file', file, file.name || 'capture.jpg');
            const resp = await fetch(_uploadBaseUrl + '/api/v1/images/upload?folder=onboarding', {
                method: 'POST',
                body: form
            });
            if (!resp.ok) {
                console.warn('[OnboardingWizard] photo upload failed:', resp.status);
                return null;
            }
            const data = await resp.json();
            return data && data.url ? data.url : null;
        } catch (e) {
            console.warn('[OnboardingWizard] photo upload error:', e);
            return null;
        }
    }

    /** Đọc 1 ảnh (file input/camera) → OCR + upload song song → trả kết quả + photoUrls về Blazor. */
    async function processFile(file, methodName) {
        if (!file) return;
        try {
            const [result, photoUrl] = await Promise.all([
                window.vananOnboardingOcr[methodName](file),
                _uploadPhoto(file)
            ]);
            await _dotNetRef.invokeMethodAsync('OnOcrResult', methodName, JSON.stringify(result),
                photoUrl ? JSON.stringify([photoUrl]) : null);
        } catch (e) {
            console.error('[OnboardingWizard] OCR fail:', e);
            await _dotNetRef.invokeMethodAsync('OnOcrError', methodName, e.message);
        }
    }

    /** Đọc nhiều ảnh menu → extractMenu + upload tất cả → trả kết quả + photoUrls về Blazor. */
    async function processMenuFiles(files) {
        const list = Array.from(files || []);
        if (!list.length) return;
        try {
            const [result, urls] = await Promise.all([
                window.vananOnboardingOcr.extractMenu(list),
                Promise.all(list.map(_uploadPhoto))
            ]);
            const photoUrls = urls.filter((u) => !!u);
            await _dotNetRef.invokeMethodAsync('OnOcrResult', 'extractMenu', JSON.stringify(result),
                photoUrls.length ? JSON.stringify(photoUrls) : null);
        } catch (e) {
            console.error('[OnboardingWizard] Menu OCR fail:', e);
            await _dotNetRef.invokeMethodAsync('OnOcrError', 'extractMenu', e.message);
        }
    }

    // ── P3: draft autosave (sessionStorage) ──────────────────────────────
    const DRAFT_KEY = 'ocr-wizard-draft';
    function saveDraft(json) {
        try { sessionStorage.setItem(DRAFT_KEY, json); } catch (e) { /* quota/private mode — best-effort */ }
    }
    function loadDraft() {
        try { return sessionStorage.getItem(DRAFT_KEY); } catch (e) { return null; }
    }
    function clearDraft() {
        try { sessionStorage.removeItem(DRAFT_KEY); } catch (e) { /* best-effort */ }
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
                // P3: click bản đồ → set tọa độ (Blazor OnMapClick)
                mlMap.on('click', (e) => {
                    if (_dotNetRef && e.lngLat) {
                        _dotNetRef.invokeMethodAsync('OnMapClick', e.lngLat.lat, e.lngLat.lng);
                    }
                });
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
        // P3: click bản đồ → set tọa độ (Blazor OnMapClick)
        _map.on('click', (e) => {
            if (_dotNetRef && e.latlng) {
                _dotNetRef.invokeMethodAsync('OnMapClick', e.latlng.lat, e.latlng.lng);
            }
        });
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

    return { register, configureUpload, processFile, processMenuFiles, getCurrentPosition, initMap, openCamera, closeCamera, captureAndOcr, captureMenuShot, finishMenuCapture, saveDraft, loadDraft, clearDraft };
})();