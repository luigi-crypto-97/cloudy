//
//  MapView.swift
//  Cloudy — Schermata mappa principale
//
//  Sostituisce MainMapPage.xaml/cs (94KB di MAUI). Usa SwiftUI Map
//  con annotations native + custom CloudBubble.
//
//  Performance pattern:
//   - Una sola TimelineView a livello di mappa che produce `phase 0..1` per
//     tutte le nuvole. Niente per-cloud Animation sul main thread.
//   - Annotation re-rendering è lasciato a SwiftUI diffing (Identifiable).
//   - Fog links disegnati come Canvas overlay separato (z-index sotto markers).
//

import SwiftUI
import MapKit

struct MapView: View {

    @Environment(MapStore.self) private var store
    @Environment(AppRouter.self) private var router

    @State private var camera: MapCameraPosition = .region(MapStore.milanDefault)
    @State private var selectedVenue: VenueMarker?
    @State private var showsFilters: Bool = false

    var body: some View {
        ZStack(alignment: .top) {
            mapLayer
                .ignoresSafeArea(edges: .top)

            // Top floating header (search + filtri)
            topBar
                .padding(.horizontal, Theme.Spacing.lg)
                .padding(.top, 6)

            // Legend / status nella parte bassa
            VStack {
                Spacer()
                statusBar
                    .padding(.horizontal, Theme.Spacing.lg)
                    .padding(.bottom, 110)  // sopra la tab bar
            }
        }
        .sheet(item: $selectedVenue) { venue in
            VenueDetailSheet(venue: venue)
                .presentationDetents([.fraction(0.45), .large])
                .presentationDragIndicator(.visible)
                .presentationBackground(.thinMaterial)
        }
        .sheet(isPresented: $showsFilters) {
            MapFiltersSheet()
                .presentationDetents([.medium])
                .presentationDragIndicator(.visible)
        }
        .task {
            // Fetch iniziale Milano default.
            await store.refresh()
        }
    }

    // MARK: - Map layer

    private var mapLayer: some View {
        // Una sola TimelineView per il pulse globale: 0..1 ogni 2.4s
        TimelineView(.animation(minimumInterval: 1.0 / 30.0, paused: false)) { ctx in
            let t = ctx.date.timeIntervalSinceReferenceDate
            let phase = (t.truncatingRemainder(dividingBy: 2.4)) / 2.4

            Map(position: $camera, interactionModes: .all, selection: .constant(nil as VenueMarker?)) {
                // Fog links (overlay). MapKit disegna MapPolyline.
                ForEach(store.fogLinks, id: \.id) { link in
                    MapPolyline(coordinates: [link.from, link.to])
                        .stroke(
                            Theme.Palette.cloudWhite.opacity(0.55 * link.strength),
                            style: StrokeStyle(lineWidth: 14 * link.strength, lineCap: .round)
                        )
                }

                // Nuvole-marker
                ForEach(store.markers) { marker in
                    Annotation(
                        marker.name,
                        coordinate: marker.coordinate,
                        anchor: .center
                    ) {
                        Button {
                            selectedVenue = marker
                            Haptics.tap()
                        } label: {
                            CloudBubble(
                                intensity: marker.bubbleIntensity,
                                peopleCount: marker.peopleEstimate,
                                densityLevel: marker.densityLevel,
                                isSelected: selectedVenue?.id == marker.id,
                                phase: phase
                            )
                        }
                        .buttonStyle(.plain)
                    }
                    .annotationTitles(.hidden)
                }
            }
            .mapStyle(.standard(elevation: .realistic, pointsOfInterest: .excludingAll))
            .mapControls {
                MapCompass()
                MapUserLocationButton()
            }
            .onMapCameraChange(frequency: .onEnd) { ctx in
                store.onViewportChanged(ctx.region)
            }
        }
    }

    // MARK: - Top bar

    private var topBar: some View {
        HStack(spacing: 10) {
            // Cloudy logo
            HStack(spacing: 6) {
                Image(systemName: "cloud.fill")
                    .font(.system(size: 18, weight: .bold))
                    .foregroundStyle(Theme.Palette.honey)
                Text("Cloudy")
                    .font(Theme.Font.title(20, weight: .heavy))
                    .foregroundStyle(Theme.Palette.ink)
            }
            .padding(.horizontal, 14)
            .padding(.vertical, 10)
            .background(.thinMaterial, in: Capsule())
            .liftedShadow()

            Spacer(minLength: 4)

            // Filters button
            Button {
                showsFilters = true
                Haptics.tap()
            } label: {
                Image(systemName: "slider.horizontal.3")
                    .font(.system(size: 18, weight: .bold))
                    .foregroundStyle(Theme.Palette.ink)
                    .padding(12)
                    .background(.thinMaterial, in: Circle())
                    .liftedShadow()
            }
        }
    }

    // MARK: - Status bar

    private var statusBar: some View {
        Group {
            if store.isLoading {
                HStack(spacing: 8) {
                    LoadingDots()
                    Text("Aggiorno la mappa…")
                        .font(Theme.Font.body(13, weight: .semibold))
                        .foregroundStyle(Theme.Palette.inkSoft)
                }
                .padding(.horizontal, 16)
                .padding(.vertical, 10)
                .background(.thinMaterial, in: Capsule())
                .liftedShadow()
                .transition(.opacity)
            } else if let err = store.errorMessage {
                HStack(spacing: 8) {
                    Image(systemName: "exclamationmark.triangle.fill")
                        .foregroundStyle(.orange)
                    Text(err)
                        .font(Theme.Font.body(13))
                        .foregroundStyle(Theme.Palette.ink)
                        .lineLimit(2)
                    Button("Riprova") {
                        Task { await store.refresh() }
                    }
                    .buttonStyle(.honeyCompact)
                }
                .padding(12)
                .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 16))
                .liftedShadow()
            } else if !store.markers.isEmpty {
                Text(summary)
                    .font(Theme.Font.body(13, weight: .semibold))
                    .foregroundStyle(Theme.Palette.inkSoft)
                    .padding(.horizontal, 14).padding(.vertical, 8)
                    .background(.thinMaterial, in: Capsule())
            }
        }
        .animation(.spring(response: 0.4), value: store.isLoading)
        .animation(.spring(response: 0.4), value: store.errorMessage)
    }

    private var summary: String {
        let total = store.markers.reduce(0) { $0 + $1.peopleEstimate }
        return "\(store.markers.count) luoghi · \(total) persone attive ora"
    }
}
