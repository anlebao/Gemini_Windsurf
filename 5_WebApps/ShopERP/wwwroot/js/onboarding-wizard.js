/**
 * Onboarding Wizard S4 (2026-10-25): geolocation + Leaflet map interop
 * cho /admin/tenants tab "Onboarding OCR".
 * Leaflet đã load global (App.razor — _content/VanAn.UI.Platform/lib/leaflet).
 */
window.vananOnboardingWizard = (function () {
    'use strict';

    let _dotNetRef = null;

    /** Blazor đăng ký DotNetObjectReference để JS gọi lại (OnOcrResult/OnOcrError). */
    function register(ref) {
        _dotNetRef = ref;
        window.__ocrDotNetRef = ref;
    }

    /** Đọc 1 ảnh (file input) → gọi vananOnboardingOcr.<method>(file) → trả kết quả về Blazor. */
    async function processFile(file, methodName) {
        if (!file) return;
        try {
            const result = await window.vananOnboardingOcr[methodName](file);
            await _dotNetRef.invokeMethodAsync('OnOcrResult', methodName, JSON.stringify(result));
        } catch (e) {
            console.error('[OnboardingWizard] OCR fail:', e);
            await _dotNetRef.invokeMethodAsync('OnOcrError', methodName, e.message);
        }
    }

    /** Đọc nhiều ảnh menu → extractMenu → trả kết quả về Blazor. */
    async function processMenuFiles(files) {
        const list = Array.from(files || []);
        if (!list.length) return;
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

    /** Init/refresh Leaflet map với marker + vòng tròn accuracy. Trả map instance.
     *  RV fix 2026-10-26: L.map(el) lần 2 trên cùng element ném "Map container is
     *  already initialized" (Leaflet giữ _leaflet_id — innerHTML='' không xóa được).
     *  → map.remove() trước khi tạo lại. */
    function initMap(elementId, lat, lng, accuracy) {
        const el = document.getElementById(elementId);
        if (!el || typeof L === 'undefined') return null;
        if (_map) { _map.remove(); _map = null; }
        el.innerHTML = '';
        _map = L.map(elementId, { zoomControl: true }).setView([lat, lng], 17);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenStreetMap contributors'
        }).addTo(_map);
        L.marker([lat, lng]).addTo(_map);
        if (accuracy > 0) {
            L.circle([lat, lng], { radius: accuracy, color: '#2563eb', fillOpacity: 0.15 }).addTo(_map);
        }
        // Invalidate size sau khi hiện tab (map trong tab ẩn bị sai kích thước)
        setTimeout(() => _map?.invalidateSize(), 100);
        return _map;
    }

    return { register, processFile, processMenuFiles, getCurrentPosition, initMap };
})();