window.cloudyAdminMap = (() => {
  const maps = new Map();

  function markerHtml(marker) {
    const label = marker.type === "user"
      ? (marker.initial || "U")
      : Math.max(0, Math.min(99, marker.energy || 0));
    const type = marker.type || "venue";
    return `<div class="leaflet-cloudy-marker ${type}">${label}</div>`;
  }

  function render(elementId, markers) {
    const element = document.getElementById(elementId);
    if (!element) return;
    if (typeof L === "undefined") {
      setTimeout(() => render(elementId, markers), 120);
      return;
    }

    let mapState = maps.get(elementId);
    if (mapState && mapState.element !== element) {
      try { mapState.map.remove(); } catch (_) {}
      maps.delete(elementId);
      mapState = null;
    }

    if (!mapState) {
      const fallback = [45.4642, 9.19];
      const map = L.map(element, {
        zoomControl: true,
        attributionControl: true,
        scrollWheelZoom: false
      }).setView(fallback, 12);

      L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: '&copy; OpenStreetMap contributors'
      }).addTo(map);

      mapState = { element, map, layer: L.layerGroup().addTo(map) };
      maps.set(elementId, mapState);
    }

    const cleanMarkers = (markers || [])
      .filter(x => Number.isFinite(x.latitude) && Number.isFinite(x.longitude));

    mapState.layer.clearLayers();

    if (cleanMarkers.length === 0) {
      mapState.map.setView([45.4642, 9.19], 11);
      return;
    }

    const bounds = [];
    cleanMarkers.forEach(marker => {
      const latLng = [marker.latitude, marker.longitude];
      bounds.push(latLng);
      const icon = L.divIcon({
        html: markerHtml(marker),
        className: "leaflet-cloudy-wrapper",
        iconSize: marker.type === "user" ? [38, 38] : [44, 44],
        iconAnchor: marker.type === "user" ? [19, 19] : [22, 22]
      });

      L.marker(latLng, { icon })
        .bindPopup(`<strong>${escapeHtml(marker.name || "Segnale")}</strong><br>${escapeHtml(marker.subtitle || "")}`)
        .addTo(mapState.layer);
    });

    mapState.map.fitBounds(bounds, { padding: [36, 36], maxZoom: 15 });
    setTimeout(() => mapState.map.invalidateSize(), 50);
  }

  function escapeHtml(value) {
    return String(value)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#039;");
  }

  return { render };
})();
