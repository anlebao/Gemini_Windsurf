// Realtime Platform P4 (2026-09-17): shared JS for UI.Platform realtime components.
// Served at _content/VanAn.UI.Platform/js/realtime.js on every host that references the RCL.
//
//   window.vananRealtime — GPS + scroll helpers (ported from KhachLink pwa.js:606-633)
//   window.vananMap      — Leaflet map interop (ported from KhachLink leaflet.js, renamed so
//                          both namespaces can coexist while old pages still reference leafletMap)
//
// Leaflet itself is vendored at _content/VanAn.UI.Platform/lib/leaflet/ and must load first.

window.vananRealtime = {
    // W17-T5: Get current GPS position.
    // Rejects with a proper Error carrying a user-friendly message based on
    // GeolocationPositionError.code. Rejecting with the raw error object makes
    // Blazor's JSRuntime surface "[object GeolocationPositionError]" as the
    // exception message.
    getCurrentPosition() {
        return new Promise((resolve, reject) => {
            if (!navigator.geolocation) {
                reject(new Error('Trình duyệt không hỗ trợ định vị GPS.'));
                return;
            }
            navigator.geolocation.getCurrentPosition(
                (pos) => resolve({ lat: pos.coords.latitude, lng: pos.coords.longitude }),
                (err) => {
                    let msg = 'Không lấy được vị trí GPS.';
                    // GeolocationPositionError.code: 1=PERMISSION_DENIED, 2=POSITION_UNAVAILABLE, 3=TIMEOUT
                    switch (err && err.code) {
                        case 1: msg = 'Quyền truy cập vị trí bị từ chối. Vui lòng cấp quyền GPS cho trang web.'; break;
                        case 2: msg = 'Không xác định được vị trí. Vui lòng bật GPS và thử lại ở nơi có tín hiệu tốt hơn.'; break;
                        case 3: msg = 'Hết thời gian chờ GPS. Vui lòng thử lại.'; break;
                    }
                    reject(new Error(msg));
                },
                { timeout: 8000, maximumAge: 60000, enableHighAccuracy: false }
            );
        });
    },

    // Scroll a container to bottom (for chat auto-scroll)
    scrollToBottom(elementId) {
        const el = document.getElementById(elementId);
        if (el) el.scrollTop = el.scrollHeight;
    }
};

// === Leaflet map interop (window.vananMap.*) ===

let _maps = {};
let _markers = {};

// P6 RV (2026-09-18): tile.openstreetmap.org is UNREACHABLE from some networks
// (ERR_CONNECTION_REFUSED — e.g. Vietnam) → a perfectly initialised map renders as a
// gray frame with zero tiles. The old Google iframe worked because Google Maps embed
// is accessible there. Fix: try OSM first, and on tile errors switch to the next
// provider (CARTO / Esri — both reachable). Switching is per-map + burst-locked so a
// host-level refusal (10 errors at once) only advances one provider.
const TILE_PROVIDERS = [
    { url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', attribution: '&copy; OpenStreetMap contributors', subdomains: 'abc', maxZoom: 19 },
    { url: 'https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png', attribution: '&copy; OpenStreetMap contributors &copy; CARTO', subdomains: 'abcd', maxZoom: 19 },
    { url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}', attribution: 'Tiles &copy; Esri', maxZoom: 18 }
];

function addTileLayer(map, index) {
    const p = TILE_PROVIDERS[index];
    const layer = L.tileLayer(p.url, {
        attribution: p.attribution,
        subdomains: p.subdomains,
        maxZoom: p.maxZoom
    });
    layer.on('tileerror', () => {
        if (map._vananTileSwitchLock) return;
        map._vananTileSwitchLock = true;
        if (map._vananTileProviderIndex < TILE_PROVIDERS.length - 1) {
            map._vananTileProviderIndex++;
            map.removeLayer(layer);
            addTileLayer(map, map._vananTileProviderIndex);
            console.warn('[vananMap] tile provider failed — switched to provider #' + (map._vananTileProviderIndex + 1));
        }
        // Burst lock: a host-level refusal fires many tileerror events at once.
        setTimeout(() => { map._vananTileSwitchLock = false; }, 1500);
    });
    layer.addTo(map);
    map._vananTileLayer = layer;
}

window.vananMap = {
    initMap: function (elementId, centerLat, centerLng, zoom) {
        const el = document.getElementById(elementId);
        if (!el) return;

        // Clean up existing map if any
        if (_maps[elementId]) {
            _maps[elementId].remove();
            delete _maps[elementId];
            delete _markers[elementId];
        }

        const map = L.map(elementId, {
            zoomControl: true,
            attributionControl: true
        }).setView([centerLat, centerLng], zoom || 14);

        map._vananTileProviderIndex = 0;
        map._vananTileSwitchLock = false;
        addTileLayer(map, 0);

        _maps[elementId] = map;
        _markers[elementId] = {};
    },

    addMarker: function (elementId, key, lat, lng, label, color) {
        const map = _maps[elementId];
        if (!map) return;

        // Custom icon color via SVG pin
        const iconHtml = '<div style="background:' + (color || '#3388ff') + ';width:24px;height:24px;border-radius:50% 50% 50% 0;transform:rotate(-45deg);border:2px solid white;box-shadow:0 2px 4px rgba(0,0,0,0.3);"></div>';
        const icon = L.divIcon({
            html: iconHtml,
            className: 'custom-marker',
            iconSize: [24, 24],
            iconAnchor: [12, 24]
        });

        const marker = L.marker([lat, lng], { icon: icon }).addTo(map);
        if (label) marker.bindPopup(label);
        _markers[elementId][key] = marker;
    },

    updateMarker: function (elementId, key, lat, lng) {
        const map = _maps[elementId];
        const marker = _markers[elementId] && _markers[elementId][key];
        if (!map || !marker) return;

        marker.setLatLng([lat, lng]);
        map.panTo([lat, lng], { animate: true });
    },

    // Add the marker if it does not exist yet, otherwise move it.
    // Used on every Blazor render so live GPS updates actually move the pin
    // (previously markers were only added once, on the component's first render).
    // No panning here — avoids fighting the user while they drag the map.
    upsertMarker: function (elementId, key, lat, lng, label, color) {
        const map = _maps[elementId];
        if (!map) return;

        const existing = _markers[elementId] && _markers[elementId][key];
        if (existing) {
            const cur = existing.getLatLng();
            if (cur.lat !== lat || cur.lng !== lng) existing.setLatLng([lat, lng]);
            return;
        }
        window.vananMap.addMarker(elementId, key, lat, lng, label, color);
    },

    // Checkout delivery pin (2026-09-20): draggable marker + click-to-pin. Writes the pinned
    // lat/lng into hidden inputs (latInputId/lngInputId) so Blazor reads them at submit.
    pinLocation: function (elementId, latInputId, lngInputId, initialLat, initialLng, zoom) {
        window.vananMap.initMap(elementId, initialLat, initialLng, zoom || 15);
        const map = _maps[elementId];
        if (!map) return;

        const latInput = document.getElementById(latInputId);
        const lngInput = document.getElementById(lngInputId);

        const marker = L.marker([initialLat, initialLng], { draggable: true }).addTo(map);
        _markers[elementId]['pin'] = marker;

        const sync = function (ll) {
            if (latInput) latInput.value = ll.lat.toFixed(6);
            if (lngInput) lngInput.value = ll.lng.toFixed(6);
        };
        sync(marker.getLatLng());

        marker.on('dragend', function () { sync(marker.getLatLng()); });
        map.on('click', function (e) {
            marker.setLatLng(e.latlng);
            sync(e.latlng);
        });
    },

    // 2026-09-21: move the delivery pin created by pinLocation (e.g. when GPS arrives
    // after the map already rendered) without re-initialising the map. No-op if the
    // map/pin doesn't exist yet.
    setPinLocation: function (elementId, latInputId, lngInputId, lat, lng) {
        const map = _maps[elementId];
        const marker = map && _markers[elementId] && _markers[elementId]['pin'];
        if (!map || !marker) return;
        marker.setLatLng([lat, lng]);
        map.panTo([lat, lng], { animate: true });
        const latInput = document.getElementById(latInputId);
        const lngInput = document.getElementById(lngInputId);
        if (latInput) latInput.value = Number(lat).toFixed(6);
        if (lngInput) lngInput.value = Number(lng).toFixed(6);
    },

    drawRoute: function (elementId, fromLat, fromLng, toLat, toLng) {
        const map = _maps[elementId];
        if (!map) return;

        const latlngs = [[fromLat, fromLng], [toLat, toLng]];
        L.polyline(latlngs, { color: '#3388ff', weight: 3, opacity: 0.7, dashArray: '8,8' }).addTo(map);
        map.fitBounds(latlngs, { padding: [50, 50] });
    },

    fitBounds: function (elementId, points) {
        const map = _maps[elementId];
        if (!map || !points || points.length === 0) return;

        const latlngs = points.map(p => [p.lat, p.lng]);
        map.fitBounds(latlngs, { padding: [50, 50] });
    },

    removeMap: function (elementId) {
        if (_maps[elementId]) {
            _maps[elementId].remove();
            delete _maps[elementId];
            delete _markers[elementId];
        }
    }
};
