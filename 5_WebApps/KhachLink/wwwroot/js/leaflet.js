// CC-S2 (Sprint 2): Leaflet map JS interop for Blazor WASM.
// Functions: initMap, addMarker, updateMarker, drawRoute, removeMap.
// Leaflet is vendored at /lib/leaflet/leaflet.js (loaded in index.html).

let _maps = {};
let _markers = {};

window.leafletMap = {
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

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenStreetMap contributors',
            maxZoom: 19
        }).addTo(map);

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
