// TenantManagement.razor map picker — Leaflet (free, no API key required).
// Loaded lazily from CDN on first init. Exposes window.vananTenantMap.
(function () {
    let map = null;
    let marker = null;
    let dotNetRef = null;
    let leafletLoaded = false;

    function loadCss(href) {
        if (document.querySelector(`link[href="${href}"]`)) return Promise.resolve();
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = href;
        document.head.appendChild(link);
        return new Promise((resolve) => { link.onload = resolve; });
    }

    function loadScript(src) {
        if (document.querySelector(`script[src="${src}"]`)) return Promise.resolve();
        const s = document.createElement('script');
        s.src = src;
        s.async = false;
        document.head.appendChild(s);
        return new Promise((resolve) => { s.onload = resolve; });
    }

    async function ensureLeaflet() {
        if (leafletLoaded && window.L) return;
        await Promise.all([
            loadCss('https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'),
            loadScript('https://unpkg.com/leaflet@1.9.4/dist/leaflet.js')
        ]);
        leafletLoaded = true;
    }

    function notifyDotNet(lat, lng) {
        if (dotNetRef) {
            try {
                dotNetRef.invokeMethodAsync('OnMapMarkerMoved', lat, lng);
            } catch (e) {
                console.warn('[vananTenantMap] notifyDotNet failed:', e);
            }
        }
    }

    window.vananTenantMap = {
        async init(elementId, lat, lng, dotNetReference) {
            await ensureLeaflet();
            dotNetRef = dotNetReference;

            // Destroy any existing map instance to avoid "Map container already initialized"
            this.destroy();

            const el = document.getElementById(elementId);
            if (!el) {
                console.warn('[vananTenantMap] element not found:', elementId);
                return;
            }

            map = L.map(elementId, { scrollWheelZoom: true }).setView([lat, lng], 13);

            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '&copy; OpenStreetMap contributors',
                maxZoom: 19
            }).addTo(map);

            marker = L.marker([lat, lng], { draggable: true }).addTo(map);

            marker.on('dragend', (e) => {
                const pos = e.target.getLatLng();
                notifyDotNet(pos.lat, pos.lng);
            });

            map.on('click', (e) => {
                marker.setLatLng(e.latlng);
                notifyDotNet(e.latlng.lat, e.latlng.lng);
            });

            // Force map to recalculate size after modal animation
            setTimeout(() => { if (map) map.invalidateSize(); }, 250);
            setTimeout(() => { if (map) map.invalidateSize(); }, 500);
        },

        setMarker(lat, lng) {
            if (!map || !marker) return;
            marker.setLatLng([lat, lng]);
            map.panTo([lat, lng]);
        },

        clear() {
            if (!map || !marker) return;
            // Move marker off-screen (Leaflet can't fully hide a marker without removing it)
            // We keep the marker but reset to default HCM center so user can re-pick.
            const defaultLat = 10.7769, defaultLng = 106.7009;
            marker.setLatLng([defaultLat, defaultLng]);
            map.setView([defaultLat, defaultLng], 13);
        },

        async getCurrentLocation() {
            return new Promise((resolve, reject) => {
                if (!navigator.geolocation) {
                    reject(new Error('Geolocation not supported'));
                    return;
                }
                navigator.geolocation.getCurrentPosition(
                    (pos) => resolve({ Latitude: pos.coords.latitude, Longitude: pos.coords.longitude }),
                    (err) => reject(err),
                    { enableHighAccuracy: true, timeout: 10000 }
                );
            });
        },

        destroy() {
            if (map) {
                map.remove();
                map = null;
                marker = null;
            }
        }
    };
})();
