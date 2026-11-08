// Directory SSR — Leaflet map helper
// Adapt from KhachLink/wwwroot/js/leaflet.js — simplified for Directory (no markers clustering)

window.directoryMap = (function () {
    let map = null;

    // Fix Leaflet default marker icon 404s — wwwroot/lib/leaflet/ ships only
    // leaflet.css + leaflet.js (no images/ folder). Point default icon to CDN.
    delete L.Icon.Default.prototype._getIconUrl;
    L.Icon.Default.mergeOptions({
        iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
        iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
        shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png'
    });

    // 2026-11-08: tile chain giống UI.Platform realtime.js (CARTO Voyager → Esri → OSM)
    // — OSM đơn bị chặn trên một số mạng VN → map xám. Copy self-contained vì
    // Directory không load realtime.js.
    const TILE_PROVIDERS = [
        { url: 'https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png', attribution: '&copy; OpenStreetMap contributors &copy; CARTO', subdomains: 'abcd', maxZoom: 19 },
        { url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}', attribution: 'Tiles &copy; Esri', maxZoom: 18 },
        { url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', attribution: '&copy; OpenStreetMap contributors', subdomains: 'abc', maxZoom: 19 }
    ];

    function addTileLayer(m, index) {
        const p = TILE_PROVIDERS[index];
        const layer = L.tileLayer(p.url, {
            attribution: p.attribution,
            subdomains: p.subdomains,
            maxZoom: p.maxZoom
        });
        layer.on('tileerror', () => {
            if (m._tileSwitchLock || m._tileProviderIndex >= TILE_PROVIDERS.length - 1) return;
            m._tileSwitchLock = true;
            m._tileProviderIndex++;
            m.removeLayer(layer);
            addTileLayer(m, m._tileProviderIndex);
            setTimeout(() => { m._tileSwitchLock = false; }, 1500);
        });
        layer.on('load', () => { m._tilesLoaded = true; });
        layer.addTo(m);
        m._tileLayer = layer;
    }

    function init(elementId) {
        if (map) { map.remove(); map = null; }
        map = L.map(elementId).setView([10.7626, 106.6603], 12); // HCM default
        map._tileProviderIndex = 0;
        map._tileSwitchLock = false;
        map._tilesLoaded = false;
        addTileLayer(map, 0);
        // Watchdog: tile request treo không bắn tileerror → ép switch sau 8s.
        setTimeout(() => {
            if (!map._tilesLoaded && map._tileProviderIndex < TILE_PROVIDERS.length - 1) {
                map._tileProviderIndex++;
                map.removeLayer(map._tileLayer);
                addTileLayer(map, map._tileProviderIndex);
            }
        }, 8000);
    }

    function render(elementId, stores, userLat, userLng) {
        init(elementId);
        if (userLat && userLng) {
            L.marker([userLat, userLng]).addTo(map).bindPopup('V\u1ecb tr\u00ed c\u1ee7a b\u1ea1n');
            map.setView([userLat, userLng], 14);
        }
        stores.forEach(s => {
            if (s.latitude && s.longitude) {
                L.marker([s.latitude, s.longitude]).addTo(map)
                    .bindPopup('<strong>' + s.name + '</strong><br>' + (s.address || ''));
            }
        });
    }

    function getUserLocation() {
        return new Promise((resolve, reject) => {
            if (!navigator.geolocation) { reject('No geolocation'); return; }
            navigator.geolocation.getCurrentPosition(
                pos => resolve({ lat: pos.coords.latitude, lng: pos.coords.longitude }),
                err => reject(err),
                { timeout: 10000 }
            );
        });
    }

    return { render: render, getUserLocation: getUserLocation };
})();
