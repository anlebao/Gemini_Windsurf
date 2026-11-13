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
    },

    // SF-W3: scroll a container to top (newest-first chat surfaces — AI widget
    // renders newest message at top, so "scroll to latest" = scrollTop 0).
    scrollToTop(elementId) {
        const el = document.getElementById(elementId);
        if (el) el.scrollTop = 0;
    }
};


// Fix Leaflet default marker icon 404s — the RCL lib/leaflet/ folder ships only
// leaflet.css + leaflet.js; the default icon resolves to images/marker-icon*.png
// relative to the CSS <link> path and 404s when the images/ folder is missing
// (user report: _content/VanAn.UI.Platform/lib/leaflet/images/marker-icon-2x.png).
// Point the default icon at the RCL's vendored images (same-origin — no external
// CDN dependency, VN networks block unpkg/OSM per #185 lesson). Mirrors the
// Directory app's directory-map.js pattern.
if (window.L && L.Icon && L.Icon.Default) {
    delete L.Icon.Default.prototype._getIconUrl;
    L.Icon.Default.mergeOptions({
        iconRetinaUrl: '/_content/VanAn.UI.Platform/lib/leaflet/images/marker-icon-2x.png',
        iconUrl: '/_content/VanAn.UI.Platform/lib/leaflet/images/marker-icon.png',
        shadowUrl: '/_content/VanAn.UI.Platform/lib/leaflet/images/marker-shadow.png'
    });
}
// === Leaflet map interop (window.vananMap.*) ===

let _maps = {};
let _markers = {};
let _gmaps = {};
let _gmarkers = {};
let _gmapsLoaderPromise = null;
let _mlmaps = {};
let _mlmarkers = {};
let _maplibreLoaderPromise = null;

// MapLibre GL JS + OpenFreeMap (2026-11-08 — user-chosen free Google-look):
// vector tiles, no API key, no billing. Style 'liberty' = closest free style
// to Google Maps. Lazy-loaded from CDN once; failures → Leaflet fallback.
function loadMapLibre() {
    if (window.maplibregl) return Promise.resolve();
    if (_maplibreLoaderPromise) return _maplibreLoaderPromise;
    _maplibreLoaderPromise = new Promise(function (resolve, reject) {
        const css = document.createElement('link');
        css.rel = 'stylesheet';
        css.href = '/_content/VanAn.UI.Platform/lib/maplibre/maplibre-gl.css';
        document.head.appendChild(css);
        const s = document.createElement('script');
        s.src = '/_content/VanAn.UI.Platform/lib/maplibre/maplibre-gl.js';
        s.async = true;
        s.onload = resolve;
        s.onerror = function () {
            _maplibreLoaderPromise = null;
            reject(new Error('MapLibre script load failed'));
        };
        document.head.appendChild(s);
    });
    return _maplibreLoaderPromise;
}

// Google Maps JS API (2026-11-08 — checkout pin map upgrade): interactive map
// with the real Google look + draggable pin. Requires a Google Cloud key with
// "Maps JavaScript API" enabled (deploy-time GoogleMaps:JsApiKey). Lazy-loaded
// once; auth/script failures let the caller fall back to the Leaflet chain.
function loadGoogleMaps(apiKey) {
    if (window.google && window.google.maps && window.google.maps.Map) {
        return Promise.resolve();
    }
    if (_gmapsLoaderPromise) return _gmapsLoaderPromise;
    _gmapsLoaderPromise = new Promise(function (resolve, reject) {
        // gm_authFailure: bad key / referrer restriction / billing off → fallback.
        window.gm_authFailure = function () {
            _gmapsLoaderPromise = null;
            reject(new Error('Google Maps auth failure'));
        };
        const s = document.createElement('script');
        s.src = 'https://maps.googleapis.com/maps/api/js?key=' + encodeURIComponent(apiKey) + '&language=vi&region=VN';
        s.async = true;
        s.onload = resolve;
        s.onerror = function () {
            _gmapsLoaderPromise = null;
            reject(new Error('Google Maps script load failed'));
        };
        document.head.appendChild(s);
    });
    return _gmapsLoaderPromise;
}

// P6 RV (2026-09-18): tile.openstreetmap.org is UNREACHABLE from some networks
// (ERR_CONNECTION_REFUSED — e.g. Vietnam) → a perfectly initialised map renders as a
// gray frame with zero tiles. The old Google iframe worked because Google Maps embed
// is accessible there. Fix: try OSM first, and on tile errors switch to the next
// provider (CARTO / Esri — both reachable). Switching is per-map + burst-locked so a
// host-level refusal (10 errors at once) only advances one provider.
// 2026-11-07: CARTO Voyager moved to #1 — prettier style AND more reliable on
// VN networks; OSM standard demoted to last resort. (User feedback: map
// "không ổn định, không đẹp" — OSM-first was the main cause of both.)
const TILE_PROVIDERS = [
    { url: 'https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png', attribution: '&copy; OpenStreetMap contributors &copy; CARTO', subdomains: 'abcd', maxZoom: 19 },
    { url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}', attribution: 'Tiles &copy; Esri', maxZoom: 18 },
    { url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', attribution: '&copy; OpenStreetMap contributors', subdomains: 'abc', maxZoom: 19 }
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
    layer.on('load', () => { map._vananTilesLoaded = true; });
    layer.addTo(map);
    map._vananTileLayer = layer;
}

// Issue #185: a HANGING tile request never fires `tileerror` — the current provider is
// refused (or blackholed) on some networks (e.g. OSM/CARTO in Vietnam), so the map stays a
// gray frame forever and the fallback chain never advances. The watchdog in initMap forces
// the switch when no tile has loaded within the timeout.
function forceSwitchTileProvider(map) {
    if (map._vananTileSwitchLock) return;
    map._vananTileSwitchLock = true;
    if (map._vananTileProviderIndex < TILE_PROVIDERS.length - 1) {
        map._vananTileProviderIndex++;
        map._vananTilesLoaded = false;
        map.removeLayer(map._vananTileLayer);
        addTileLayer(map, map._vananTileProviderIndex);
        console.warn('[vananMap] tile watchdog — no tiles loaded, switched to provider #' + (map._vananTileProviderIndex + 1));
    }
    setTimeout(() => { map._vananTileSwitchLock = false; }, 1500);
}

// Sync Leaflet init — fallback engine for initMap and the pin flows that need
// a synchronous Leaflet map (pinLocation reads _maps right after init).
function initLeafletMap(elementId, centerLat, centerLng, zoom) {
    const el = document.getElementById(elementId);
    if (!el) return null;
    if (_maps[elementId]) {
        _maps[elementId].remove();
        delete _maps[elementId];
        delete _markers[elementId];
    }
    const map = L.map(elementId, {
        zoomControl: true,
        attributionControl: true
    }).setView([centerLat, centerLng], zoom || 14);
    window.vananMap.attachTiles(map);
    _maps[elementId] = map;
    _markers[elementId] = {};
    // Issue #185: blank / 0-height map when initialised in an animating/hidden
    // container — re-measure once the DOM settles.
    setTimeout(function () { map.invalidateSize(); }, 0);
    setTimeout(function () { map.invalidateSize(); }, 300);
    return map;
}

window.vananMap = {
    // 2026-11-08: MapLibre + OpenFreeMap is now the default engine (vector,
    // ~Google look, free); Leaflet chain remains the fallback. Async — Blazor
    // InvokeVoidAsync awaits the returned promise, so marker calls that follow
    // always see a ready map.
    initMap: async function (elementId, centerLat, centerLng, zoom) {
        const mlMap = await window.vananMap.createMapLibre(elementId, centerLat, centerLng, zoom || 14);
        if (mlMap) return;
        initLeafletMap(elementId, centerLat, centerLng, zoom);
    },

    // Attach the shared tile-provider chain (CARTO Voyager → Esri → OSM + tileerror
    // fallback + hang watchdog) to a Leaflet map created OUTSIDE vananMap.initMap
    // (e.g. onboarding-wizard.js, tenant-map.js). Keeps one source of truth for
    // providers so every map in the ecosystem gets the same reliability fix.
    attachTiles: function (map) {
        map._vananTileProviderIndex = 0;
        map._vananTileSwitchLock = false;
        map._vananTilesLoaded = false;
        addTileLayer(map, 0);

        // Issue #185: a HANGING tile request never fires `tileerror`, so the fallback chain
        // never advances and the map stays a gray frame. If no tile has loaded within 8s,
        // force-switch to the next provider (see forceSwitchTileProvider).
        clearTimeout(map._vananTileWatchdog);
        map._vananTileWatchdog = setTimeout(function () {
            if (map._vananTilesLoaded) return;
            forceSwitchTileProvider(map);
        }, 8000);
    },

    addMarker: function (elementId, key, lat, lng, label, color) {
        // MapLibre engine — colored pin + click popup (parity with Leaflet divIcon).
        const mlmap = _mlmaps[elementId];
        if (mlmap) {
            const marker = new maplibregl.Marker({ color: color || '#3388ff' })
                .setLngLat([lng, lat])
                .addTo(mlmap);
            if (label) marker.setPopup(new maplibregl.Popup({ offset: 25 }).setText(label));
            _mlmarkers[elementId][key] = marker;
            return;
        }

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
        const mlmap = _mlmaps[elementId];
        const mlm = mlmap && _mlmarkers[elementId] && _mlmarkers[elementId][key];
        if (mlmap && mlm) {
            mlm.setLngLat([lng, lat]);
            mlmap.easeTo({ center: [lng, lat] });
            return;
        }

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
        const mlmap = _mlmaps[elementId];
        if (mlmap) {
            const existing = _mlmarkers[elementId] && _mlmarkers[elementId][key];
            if (existing) {
                const cur = existing.getLngLat();
                if (cur.lat !== lat || cur.lng !== lng) existing.setLngLat([lng, lat]);
                return;
            }
            window.vananMap.addMarker(elementId, key, lat, lng, label, color);
            return;
        }

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
        // Explicit Leaflet pin path — used as fallback by gmapsPinLocation /
        // maplibrePinLocation. Must stay sync (reads _maps right after init).
        initLeafletMap(elementId, initialLat, initialLng, zoom || 15);
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
    // map/pin doesn't exist yet. Polymorphic: works for both Leaflet and Google pins.
    setPinLocation: function (elementId, latInputId, lngInputId, lat, lng) {
        const latInput = document.getElementById(latInputId);
        const lngInput = document.getElementById(lngInputId);

        // Google Maps pin (gmapsPinLocation) — same outward contract.
        const gmap = _gmaps[elementId];
        const gmarker = gmap && _gmarkers[elementId];
        if (gmap && gmarker) {
            const ll = { lat: Number(lat), lng: Number(lng) };
            gmarker.setPosition(ll);
            gmap.panTo(ll);
            if (latInput) latInput.value = Number(lat).toFixed(6);
            if (lngInput) lngInput.value = Number(lng).toFixed(6);
            return;
        }

        // MapLibre pin (maplibrePinLocation) — same contract, [lng,lat] order.
        const mlmap = _mlmaps[elementId];
        const mlmarker = mlmap && _mlmarkers[elementId] && _mlmarkers[elementId]['pin'];
        if (mlmap && mlmarker) {
            mlmarker.setLngLat([Number(lng), Number(lat)]);
            mlmap.easeTo({ center: [Number(lng), Number(lat)] });
            if (latInput) latInput.value = Number(lat).toFixed(6);
            if (lngInput) lngInput.value = Number(lng).toFixed(6);
            return;
        }

        const map = _maps[elementId];
        const marker = map && _markers[elementId] && _markers[elementId]['pin'];
        if (!map || !marker) return;
        marker.setLatLng([lat, lng]);
        map.panTo([lat, lng], { animate: true });
        if (latInput) latInput.value = Number(lat).toFixed(6);
        if (lngInput) lngInput.value = Number(lng).toFixed(6);
    },

    // Google Maps JS API version of pinLocation (2026-11-08): identical contract —
    // draggable pin + click-to-pin, writes lat/lng to hidden inputs — but renders
    // real Google Maps when GoogleMaps:JsApiKey is configured. Any load/auth
    // failure falls back to the Leaflet chain (never leaves a broken map).
    gmapsPinLocation: async function (elementId, latInputId, lngInputId, initialLat, initialLng, zoom, apiKey) {
        const el = document.getElementById(elementId);
        if (!el) return;
        try {
            await loadGoogleMaps(apiKey);
        } catch (e) {
            console.warn('[vananMap] Google Maps unavailable — falling back to Leaflet:', e);
            window.vananMap.pinLocation(elementId, latInputId, lngInputId, initialLat, initialLng, zoom);
            return;
        }

        // Clean up previous instances on this element (Leaflet / Google / MapLibre).
        if (_maps[elementId]) {
            _maps[elementId].remove();
            delete _maps[elementId];
            delete _markers[elementId];
        }
        if (_gmaps[elementId] || _mlmaps[elementId]) {
            if (_mlmaps[elementId]) _mlmaps[elementId].remove();
            el.innerHTML = '';
            delete _gmaps[elementId];
            delete _gmarkers[elementId];
            delete _mlmaps[elementId];
            delete _mlmarkers[elementId];
        }

        const start = { lat: Number(initialLat), lng: Number(initialLng) };
        const map = new google.maps.Map(el, {
            center: start,
            zoom: zoom || 15,
            mapTypeControl: false,
            streetViewControl: false,
            fullscreenControl: false,
            clickableIcons: false,
            gestureHandling: 'greedy'
        });
        const marker = new google.maps.Marker({ position: start, map: map, draggable: true });
        _gmaps[elementId] = map;
        _gmarkers[elementId] = marker;

        const latInput = document.getElementById(latInputId);
        const lngInput = document.getElementById(lngInputId);
        const sync = function (ll) {
            const la = typeof ll.lat === 'function' ? ll.lat() : ll.lat;
            const ln = typeof ll.lng === 'function' ? ll.lng() : ll.lng;
            if (latInput) latInput.value = Number(la).toFixed(6);
            if (lngInput) lngInput.value = Number(ln).toFixed(6);
        };
        sync(marker.getPosition());
        marker.addListener('dragend', function () { sync(marker.getPosition()); });
        map.addListener('click', function (e) {
            marker.setPosition(e.latLng);
            sync(e.latLng);
        });
    },

    // MapLibre GL + OpenFreeMap map factory (2026-11-08): vector map with a
    // near-Google look (style 'liberty') — free, no API key, no billing.
    // Returns the maplibregl.Map instance (registered in _mlmaps) or null on
    // any load/init failure — callers decide their own fallback (usually the
    // Leaflet attachTiles chain). Other pin-location JS (tenant-map.js,
    // onboarding-wizard.js) uses this so every pretty map shares one loader.
    createMapLibre: async function (elementId, centerLat, centerLng, zoom) {
        try {
            await loadMapLibre();
        } catch (e) {
            console.warn('[vananMap] MapLibre unavailable:', e);
            return null;
        }
        const el = document.getElementById(elementId);
        if (!el) return null;

        // Clean up previous instances on this element (Leaflet / Google / MapLibre).
        if (_maps[elementId]) {
            _maps[elementId].remove();
            delete _maps[elementId];
            delete _markers[elementId];
        }
        if (_gmaps[elementId] || _mlmaps[elementId]) {
            if (_mlmaps[elementId]) _mlmaps[elementId].remove();
            el.innerHTML = '';
            delete _gmaps[elementId];
            delete _gmarkers[elementId];
            delete _mlmaps[elementId];
            delete _mlmarkers[elementId];
        }

        try {
            const map = new maplibregl.Map({
                container: el,
                style: 'https://tiles.openfreemap.org/styles/liberty',
                center: [Number(centerLng), Number(centerLat)],
                zoom: zoom || 15,
                attributionControl: { compact: true }
            });
            _mlmaps[elementId] = map;
            _mlmarkers[elementId] = {};
            // Container may render while still animating/hidden — re-measure.
            setTimeout(function () { map.resize(); }, 0);
            setTimeout(function () { map.resize(); }, 300);
            return map;
        } catch (e) {
            console.warn('[vananMap] MapLibre init failed:', e);
            return null;
        }
    },

    // MapLibre GL + OpenFreeMap version of pinLocation: same contract —
    // draggable pin + click-to-pin, writes lat/lng to hidden inputs (+ optional
    // onPin(lat,lng) callback for callers that notify .NET instead of inputs).
    // Load/style failures → Leaflet chain fallback (never broken map).
    maplibrePinLocation: async function (elementId, latInputId, lngInputId, initialLat, initialLng, zoom, onPin) {
        const map = await window.vananMap.createMapLibre(elementId, initialLat, initialLng, zoom);
        if (!map) {
            window.vananMap.pinLocation(elementId, latInputId, lngInputId, initialLat, initialLng, zoom);
            return;
        }

        const startLng = Number(initialLng), startLat = Number(initialLat);
        const marker = new maplibregl.Marker({ draggable: true, color: '#ea4335' })
            .setLngLat([startLng, startLat])
            .addTo(map);
        _mlmarkers[elementId]['pin'] = marker;

        const latInput = latInputId ? document.getElementById(latInputId) : null;
        const lngInput = lngInputId ? document.getElementById(lngInputId) : null;
        const sync = function (ll) {
            if (latInput) latInput.value = Number(ll.lat).toFixed(6);
            if (lngInput) lngInput.value = Number(ll.lng).toFixed(6);
            if (onPin) onPin(Number(ll.lat), Number(ll.lng));
        };
        sync(marker.getLngLat());
        marker.on('dragend', function () { sync(marker.getLngLat()); });
        map.on('click', function (e) {
            marker.setLngLat(e.lngLat);
            sync(e.lngLat);
        });
        map.addControl(new maplibregl.NavigationControl({ showCompass: false }));
    },

    drawRoute: function (elementId, fromLat, fromLng, toLat, toLng) {
        // MapLibre engine — GeoJSON line layer + fitBounds (parity with polyline).
        const mlmap = _mlmaps[elementId];
        if (mlmap) {
            const line = { type: 'Feature', geometry: { type: 'LineString', coordinates: [[fromLng, fromLat], [toLng, toLat]] } };
            const draw = function () {
                const src = mlmap.getSource('vanan-route');
                if (src) {
                    src.setData(line);
                } else {
                    mlmap.addSource('vanan-route', { type: 'geojson', data: line });
                    mlmap.addLayer({
                        id: 'vanan-route-line', type: 'line', source: 'vanan-route',
                        paint: { 'line-color': '#3388ff', 'line-width': 3, 'line-opacity': 0.7, 'line-dasharray': [2, 2] }
                    });
                }
            };
            // addSource needs the style loaded — defer if still fetching.
            if (mlmap.isStyleLoaded()) draw(); else mlmap.once('load', draw);
            mlmap.fitBounds(
                [[Math.min(fromLng, toLng), Math.min(fromLat, toLat)],
                 [Math.max(fromLng, toLng), Math.max(fromLat, toLat)]],
                { padding: 50 });
            return;
        }

        const map = _maps[elementId];
        if (!map) return;

        const latlngs = [[fromLat, fromLng], [toLat, toLng]];
        L.polyline(latlngs, { color: '#3388ff', weight: 3, opacity: 0.7, dashArray: '8,8' }).addTo(map);
        map.fitBounds(latlngs, { padding: [50, 50] });
    },

    fitBounds: function (elementId, points) {
        const mlmap = _mlmaps[elementId];
        if (mlmap) {
            if (!points || points.length === 0) return;
            const b = new maplibregl.LngLatBounds();
            points.forEach(p => b.extend([p.lng, p.lat]));
            mlmap.fitBounds(b, { padding: 50 });
            return;
        }

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
        if (_gmaps[elementId] || _mlmaps[elementId]) {
            const el = document.getElementById(elementId);
            if (_mlmaps[elementId]) _mlmaps[elementId].remove();
            if (el) el.innerHTML = '';
            delete _gmaps[elementId];
            delete _gmarkers[elementId];
            delete _mlmaps[elementId];
            delete _mlmarkers[elementId];
        }
    }
};
