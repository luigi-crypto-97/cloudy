//
//  MapStore.swift
//  Cloudy — Map state & performance-critical logic
//
//  Sostituisce MainMapViewModel + MainMapPage.Renderer + MainMapPage.Clouds.
//  Decisioni chiave per la performance:
//   1. Fetch debounced: invece di rifare la fetch ad ogni cambio viewport,
//      attendiamo 350ms di pausa.
//   2. Fog links calcolati su Task.detached (background), risultato pubblicato
//      sul main solo a calcolo finito.
//   3. Cloud pulse animato con TimelineView pure-function (no Timer / Animation
//      loop sul main thread). Una sola animazione per tutta la mappa.
//   4. Cache della firma layer per evitare update inutili.
//

import Foundation
import Observation
import CoreLocation
import MapKit
import CloudyCore

// MARK: - Public structs

struct FogLink: Identifiable, Hashable {
    let id: String
    let from: CLLocationCoordinate2D
    let to: CLLocationCoordinate2D
    let strength: Double  // 0..1 → spessore nuvola

    func hash(into hasher: inout Hasher) { hasher.combine(id) }
    static func == (lhs: FogLink, rhs: FogLink) -> Bool { lhs.id == rhs.id }
}

@MainActor
@Observable
final class MapStore {

    // MARK: - Public state

    var markers: [VenueMarker] = []
    var fogLinks: [FogLink] = []
    var isLoading: Bool = false
    var errorMessage: String?

    // Filters
    var query: String = ""
    var category: String = "all"
    var openNowOnly: Bool = false

    var lastViewport: MKCoordinateRegion?

    // MARK: - Private

    private var fetchTask: Task<Void, Never>?
    private var fogTask: Task<Void, Never>?
    private var lastSignature: String = ""

    // MARK: - Defaults

    static let milanDefault = MKCoordinateRegion(
        center: CLLocationCoordinate2D(latitude: 45.4642, longitude: 9.1900),
        span: MKCoordinateSpan(latitudeDelta: 0.06, longitudeDelta: 0.06)
    )

    // MARK: - Public API

    /// Chiamato dal `Map` quando l'utente sposta/zooma. Debounce 350ms.
    func onViewportChanged(_ region: MKCoordinateRegion) {
        lastViewport = region
        fetchTask?.cancel()
        fetchTask = Task { [weak self] in
            try? await Task.sleep(nanoseconds: 350_000_000)
            if Task.isCancelled { return }
            await self?.fetchMarkers(in: region)
        }
    }

    /// Forza un refresh immediato (pull-to-refresh).
    func refresh() async {
        guard let region = lastViewport ?? Self.milanDefault as MKCoordinateRegion? else { return }
        await fetchMarkers(in: region)
    }

    func applyFilters(query: String?, category: String?, openNow: Bool?) {
        if let query { self.query = query }
        if let category { self.category = category }
        if let openNow { self.openNowOnly = openNow }
        if let region = lastViewport {
            Task { await fetchMarkers(in: region) }
        }
    }

    // MARK: - Fetch

    private func fetchMarkers(in region: MKCoordinateRegion) async {
        let bounds = region.bounds()
        let signature = "\(bounds.minLat),\(bounds.minLng),\(bounds.maxLat),\(bounds.maxLng)|\(query)|\(category)|\(openNowOnly)"
        if signature == lastSignature, !markers.isEmpty {
            return
        }
        lastSignature = signature

        isLoading = true
        errorMessage = nil
        defer { isLoading = false }

        do {
            let result = try await API.venueMap(
                minLat: bounds.minLat, minLng: bounds.minLng,
                maxLat: bounds.maxLat, maxLng: bounds.maxLng,
                query: query.isEmpty ? nil : query,
                category: category == "all" ? nil : category,
                openNow: openNowOnly
            )
            markers = result
            recomputeFogLinks(from: result)
        } catch is CancellationError {
            return
        } catch {
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    // MARK: - Fog links (background)

    /// Sostituisce il vecchio `BuildFogLinks` O(n²) eseguito sul main thread.
    /// La logica vera vive in `CloudyCore.FogLinkBuilder` (cross-platform e testata).
    /// Qui adattiamo `VenueMarker` al protocollo `VenueClusterInput` ed
    /// eseguiamo il calcolo su Task.detached, pubblicando solo il risultato finale.
    private func recomputeFogLinks(from sample: [VenueMarker]) {
        fogTask?.cancel()
        let snapshot = sample
        fogTask = Task.detached(priority: .utility) { [weak self] in
            // Map a CloudyCore types
            let inputs = snapshot.map { ClusterInputAdapter(marker: $0) }
            let coreLinks = FogLinkBuilder.build(from: inputs)
            if Task.isCancelled { return }

            let mapped = coreLinks.map { core in
                FogLink(
                    id: core.id,
                    from: CLLocationCoordinate2D(latitude: core.from.lat, longitude: core.from.lng),
                    to: CLLocationCoordinate2D(latitude: core.to.lat, longitude: core.to.lng),
                    strength: core.strength
                )
            }
            await MainActor.run { [weak self] in
                self?.fogLinks = mapped
            }
        }
    }
}

// MARK: - Adapter VenueMarker -> CloudyCore.VenueClusterInput

private struct ClusterInputAdapter: VenueClusterInput, Sendable {
    let marker: VenueMarker
    var clusterId: String { marker.venueId.uuidString }
    var location: LatLon { LatLon(lat: marker.latitude, lng: marker.longitude) }
    var bubbleIntensity: Int { marker.bubbleIntensity }
}

// MARK: - Region helpers

private struct MapBounds {
    let minLat: Double; let minLng: Double
    let maxLat: Double; let maxLng: Double
}

private extension MKCoordinateRegion {
    func bounds() -> MapBounds {
        let halfLat = span.latitudeDelta / 2
        let halfLng = span.longitudeDelta / 2
        return MapBounds(
            minLat: center.latitude - halfLat,
            minLng: center.longitude - halfLng,
            maxLat: center.latitude + halfLat,
            maxLng: center.longitude + halfLng
        )
    }
}
