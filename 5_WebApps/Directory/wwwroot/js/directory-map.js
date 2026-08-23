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

    function init(elementId) {
        if (map) { map.remove(); map = null; }
        map = L.map(elementId).setView([10.7626, 106.6603], 12); // HCM default
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenStreetMap'
        }).addTo(map);
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
