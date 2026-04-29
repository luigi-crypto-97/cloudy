//
//  MapView.swift
//  Cloudy — Schermata mappa principale
//
//  Sostituisce MainMapPage.xaml/cs (94KB di MAUI). Usa SwiftUI Map
//  con annotations native + DensityCanvasView.
//
//  Performance pattern:
//   - La densita viene disegnata in un singolo Canvas, non con shape multiple.
//   - I marker sono cerchi minimali, stabili e tappabili.
//

import SwiftUI
import MapKit

struct MapView: View {

    @Environment(MapStore.self) private var store
    @Environment(AppRouter.self) private var router
    @Environment(LiveLocationStore.self) private var liveLocation
    @Environment(AuthStore.self) private var auth

    @State private var camera: MapCameraPosition = .region(MapStore.milanDefault)
    @State private var selectedVenue: VenueMarker?
    @State private var showsFilters: Bool = false
    @State private var showsFlareLaunch: Bool = false
    @State private var localFlares: [LocalFlare] = []
    @State private var currentUserProfile: EditableUserProfile?
    @State private var venueStories: [VenueStory] = []
    @State private var activeFlares: [FlareSignal] = []
    @State private var selectedFlare: FlareSignal?
    @State private var selectedVenueStoryGroup: VenueStoryGroup?
    @State private var visibleRegion: MKCoordinateRegion = MapStore.milanDefault
    @State private var didCenterOnInitialLocation = false

    var body: some View {
        ZStack(alignment: .top) {
            mapLayer
                .ignoresSafeArea(edges: .top)

            // Top floating header (search + filtri)
            topBar
                .padding(.horizontal, Theme.Spacing.lg)
                .padding(.top, 2)

            // Legend / status nella parte bassa
            VStack {
                Spacer()
                statusBar
                    .padding(.horizontal, Theme.Spacing.lg)
                    .padding(.bottom, 110)  // sopra la tab bar
            }

            // FAB Flare in basso a destra
            VStack {
                Spacer()
                HStack {
                    Spacer()
                    Button {
                        Haptics.tap()
                        showsFlareLaunch = true
                    } label: {
                        Image(systemName: "flame.fill")
                            .font(.system(size: 22, weight: .bold))
                            .foregroundStyle(.white)
                            .frame(width: 56, height: 56)
                            .background(Circle().fill(Theme.Palette.blue500))
                            .shadow(color: Theme.Palette.blue500.opacity(0.18), radius: 20, x: 0, y: 8)
                    }
                    .padding(.trailing, Theme.Spacing.lg)
                    .padding(.bottom, 170)
                }
            }
        }
        .sheet(isPresented: $showsFlareLaunch) {
            FlareLaunchView(
                coordinate: liveLocation.currentLocation?.coordinate ?? store.lastViewport?.center ?? MapStore.milanDefault.center,
                onSent: { message, coordinate in
                    launchLocalFlare(message: message, coordinate: coordinate)
                }
            )
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
        .sheet(item: $selectedFlare) { flare in
            FlareResponseSheet(
                flare: flare,
                canDelete: flare.userId == authUserId,
                onDeleted: {
                    selectedFlare = nil
                    Task { await loadMapSocialOverlays() }
                },
                onSent: {
                Task { await loadMapSocialOverlays() }
                }
            )
        }
        .fullScreenCover(item: $selectedVenueStoryGroup) { group in
            StoryViewerView(stories: storyViewerStories(for: group))
        }
        .task {
            visibleRegion = store.lastViewport ?? MapStore.milanDefault
            liveLocation.start()
            if let coordinate = liveLocation.currentLocation?.coordinate {
                setCamera(center: coordinate, span: MKCoordinateSpan(latitudeDelta: 0.018, longitudeDelta: 0.018), animated: false)
            }
            store.onViewportChanged(visibleRegion)
            await loadMapSocialOverlays()
        }
        .task(id: authUserId) {
            await loadCurrentUserProfile()
        }
        .onChange(of: liveLocation.currentLocation?.coordinate.latitude) {
            centerOnInitialLocationIfNeeded()
        }
        .onChange(of: liveLocation.currentLocation?.coordinate.longitude) {
            centerOnInitialLocationIfNeeded()
        }
    }

    // MARK: - Map layer

    private var mapLayer: some View {
        ZStack {
            Map(position: $camera, interactionModes: .all, selection: .constant(nil as VenueMarker?)) {
                // Fog links (overlay). MapKit disegna MapPolyline.
                ForEach(store.fogLinks, id: \.id) { link in
                    MapPolyline(coordinates: [link.from, link.to])
                        .stroke(
                            Theme.Palette.blue100.opacity(0.32 * link.strength),
                            style: StrokeStyle(lineWidth: 14 * link.strength, lineCap: .round)
                        )
                }

                ForEach(activeAreas) { area in
                    if area.polygon.count >= 3 {
                        MapPolygon(coordinates: area.polygon.map(\.coordinate))
                            .foregroundStyle(Theme.Palette.blue50.opacity(0.20))
                            .stroke(Theme.Palette.blue500.opacity(0.16), lineWidth: 1)
                    }
                    Annotation(area.label, coordinate: area.centroid, anchor: .center) {
                        Button {
                            zoomInto(area)
                            Haptics.tap()
                        } label: {
                            AreaDensityBubble(area: area)
                        }
                        .buttonStyle(.plain)
                    }
                    .annotationTitles(.hidden)
                }

                ForEach(activeVenueMarkers) { marker in
                    Annotation(
                        marker.name,
                        coordinate: marker.coordinate,
                        anchor: .center
                    ) {
                        Button {
                            selectedVenue = marker
                            Haptics.tap()
                        } label: {
                            VenueDotMarker(
                                peopleCount: activityWeight(for: marker),
                                densityLevel: marker.densityLevel,
                                energyScore: marker.partyPulse.energyScore,
                                isSelected: selectedVenue?.id == marker.id
                            )
                        }
                        .buttonStyle(.plain)
                    }
                    .annotationTitles(.hidden)
                }

                ForEach(inactiveVenueMarkers) { marker in
                    Annotation(
                        marker.name,
                        coordinate: marker.coordinate,
                        anchor: .center
                    ) {
                        Button {
                            selectedVenue = marker
                            Haptics.tap()
                        } label: {
                            VenueDotMarker(
                                peopleCount: 0,
                                densityLevel: marker.densityLevel,
                                energyScore: marker.partyPulse.energyScore,
                                isSelected: selectedVenue?.id == marker.id
                            )
                        }
                        .buttonStyle(.plain)
                    }
                    .annotationTitles(.hidden)
                }

                ForEach(friendPresenceAnnotations) { presence in
                    Annotation(presence.name, coordinate: presence.coordinate, anchor: .center) {
                        FriendPresenceMapMarker(presence: presence)
                    }
                    .annotationTitles(.hidden)
                }

                ForEach(localFlares) { flare in
                    Annotation("Flare", coordinate: flare.coordinate, anchor: .center) {
                        FlareMapBurst(message: flare.message)
                    }
                    .annotationTitles(.hidden)
                }

                ForEach(activeFlares) { flare in
                    Annotation("Flare", coordinate: flare.coordinate, anchor: .center) {
                        Button {
                            selectedFlare = flare
                            Haptics.tap()
                        } label: {
                            FlareMapBurst(message: flare.message)
                        }
                        .buttonStyle(.plain)
                    }
                    .annotationTitles(.hidden)
                }

                ForEach(venueStoryGroups) { group in
                    Annotation(group.venueName, coordinate: group.coordinate, anchor: .center) {
                        Button {
                            selectedVenueStoryGroup = group
                            Haptics.tap()
                        } label: {
                            VenueStoryMarker(count: group.stories.count)
                        }
                        .buttonStyle(.plain)
                    }
                    .annotationTitles(.hidden)
                }

                if let location = liveLocation.currentLocation {
                    Annotation("Tu", coordinate: location.coordinate, anchor: .center) {
                        UserLocationMarker(
                            avatarUrl: currentUserAvatarURL,
                            initials: currentUserInitials
                        )
                    }
                    .annotationTitles(.hidden)
                }
            }
            .mapStyle(.standard(elevation: .realistic, pointsOfInterest: .excludingAll))
            .mapControls {
                MapCompass()
            }
            .onMapCameraChange(frequency: .continuous) { ctx in
                visibleRegion = ctx.region
            }
            .onMapCameraChange(frequency: .onEnd) { ctx in
                visibleRegion = ctx.region
                store.onViewportChanged(ctx.region)
                Task { await loadMapSocialOverlays(region: ctx.region) }
            }

            DensityCanvasView(clusters: densityClusters, region: visibleRegion)
                .opacity(0.72)
                .blendMode(.multiply)
        }
    }

    // MARK: - Top bar

    private var topBar: some View {
        HStack(alignment: .top, spacing: 10) {
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

            VStack(spacing: 8) {
                Button {
                    centerOnUser()
                    Haptics.tap()
                } label: {
                    Image(systemName: "location.viewfinder")
                        .font(.system(size: 18, weight: .bold))
                        .foregroundStyle(Theme.Palette.ink)
                        .padding(12)
                        .background(.thinMaterial, in: Circle())
                        .liftedShadow()
                }
                .accessibilityLabel(Text("Centra posizione"))

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

                Button {
                    liveLocation.toggle()
                    Haptics.tap()
                } label: {
                    Image(systemName: liveLocationIcon)
                        .font(.system(size: 18, weight: .bold))
                        .foregroundStyle(liveLocationTint)
                        .padding(12)
                        .background(.thinMaterial, in: Circle())
                        .liftedShadow()
                }
                .accessibilityLabel(Text("Posizione live"))
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
            } else if !store.markers.isEmpty || !store.areas.isEmpty {
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
        let visiblePlaces = store.usesAreaLayer ? store.areas.reduce(0) { $0 + $1.venueCount } : store.markers.count
        let total = store.usesAreaLayer
            ? store.areas.reduce(0) { $0 + $1.peopleCount }
            : store.markers.reduce(0) { $0 + $1.peopleEstimate }
        if case .active = liveLocation.state, let venueName = liveLocation.lastVenueName {
            return "\(visiblePlaces) luoghi · \(total) persone · live a \(venueName)"
        }
        return "\(visiblePlaces) luoghi · \(total) persone attive ora"
    }

    private var liveLocationIcon: String {
        switch liveLocation.state {
        case .active: return "location.fill"
        case .requestingPermission: return "location.circle"
        case .denied, .failed: return "location.slash.fill"
        case .off: return "location"
        }
    }

    private var liveLocationTint: Color {
        switch liveLocation.state {
        case .active: return Theme.Palette.mint500
        case .denied, .failed: return Theme.Palette.densityHigh
        default: return Theme.Palette.ink
        }
    }

    private var authUserId: UUID? {
        if case .loggedIn(let user) = auth.state {
            return user.userId
        }
        return nil
    }

    private var currentUserInitials: String {
        if let displayName = currentUserProfile?.displayName, let first = displayName.first {
            return String(first).uppercased()
        }
        if case .loggedIn(let user) = auth.state {
            let name = user.displayName ?? user.nickname
            return String(name.prefix(1)).uppercased()
        }
        return "?"
    }

    private var currentUserAvatarURL: URL? {
        guard let raw = currentUserProfile?.avatarUrl else { return nil }
        return APIClient.shared.mediaURL(from: raw)
    }

    private func zoomInto(_ area: VenueMapArea) {
        let span = MKCoordinateSpan(latitudeDelta: 0.018, longitudeDelta: 0.018)
        setCamera(center: area.centroid, span: span, animated: true)
    }

    private func centerOnUser() {
        guard let coordinate = liveLocation.currentLocation?.coordinate else {
            liveLocation.start()
            return
        }
        setCamera(center: coordinate, span: MKCoordinateSpan(latitudeDelta: 0.010, longitudeDelta: 0.010), animated: true)
    }

    private func centerOnInitialLocationIfNeeded() {
        guard !didCenterOnInitialLocation, let coordinate = liveLocation.currentLocation?.coordinate else {
            return
        }
        didCenterOnInitialLocation = true
        setCamera(center: coordinate, span: MKCoordinateSpan(latitudeDelta: 0.018, longitudeDelta: 0.018), animated: true)
        store.onViewportChanged(visibleRegion)
        Task { await loadMapSocialOverlays(region: visibleRegion) }
    }

    private func setCamera(center: CLLocationCoordinate2D, span: MKCoordinateSpan, animated: Bool) {
        let region = MKCoordinateRegion(center: center, span: span)
        visibleRegion = region
        let update = { camera = .region(region) }
        if animated {
            withAnimation(.cloudySmooth, update)
        } else {
            update()
        }
    }

    private func launchLocalFlare(message: String, coordinate: CLLocationCoordinate2D) {
        let flare = LocalFlare(
            latitude: coordinate.latitude,
            longitude: coordinate.longitude,
            message: message
        )
        localFlares.append(flare)
        Task {
            try? await Task.sleep(nanoseconds: 7_000_000_000)
            localFlares.removeAll { $0.id == flare.id }
        }
    }

    private func loadCurrentUserProfile() async {
        guard authUserId != nil else {
            currentUserProfile = nil
            return
        }
        currentUserProfile = try? await API.myEditableProfile()
    }

    private func loadMapSocialOverlays(region: MKCoordinateRegion? = nil) async {
        async let fetchedFlares = API.flares()
        let viewport = region ?? store.lastViewport
        do {
            if let viewport {
                async let fetchedStories = API.venueStories(
                    minLat: viewport.center.latitude - viewport.span.latitudeDelta / 2,
                    minLng: viewport.center.longitude - viewport.span.longitudeDelta / 2,
                    maxLat: viewport.center.latitude + viewport.span.latitudeDelta / 2,
                    maxLng: viewport.center.longitude + viewport.span.longitudeDelta / 2
                )
                venueStories = try await fetchedStories
            } else {
                venueStories = try await API.venueStories()
            }
            activeFlares = try await fetchedFlares
        } catch {
            activeFlares = []
        }
    }

    private var densityClusters: [DensityCanvasCluster] {
        let markerClusters = store.markers
            .filter { activityWeight(for: $0) > 0 }
            .map { marker in
            DensityCanvasCluster(
                id: "m-\(marker.id.uuidString)",
                coordinate: marker.coordinate,
                weight: Double(max(activityWeight(for: marker), marker.bubbleIntensity))
            )
        }
        let areaClusters = store.areas
            .filter { $0.peopleCount > 0 }
            .map { area in
            DensityCanvasCluster(
                id: "a-\(area.id)",
                coordinate: area.centroid,
                weight: Double(max(area.peopleCount / 6, area.venueCount))
            )
        }
        return markerClusters + areaClusters
    }

    private var activeAreas: [VenueMapArea] {
        store.areas.filter { $0.peopleCount > 0 || $0.activeCheckIns > 0 || $0.activeIntentions > 0 || $0.openTables > 0 }
    }

    private var activeVenueMarkers: [VenueMarker] {
        store.markers.filter { activityWeight(for: $0) > 0 }
    }

    private var inactiveVenueMarkers: [VenueMarker] {
        guard isVenueZoomLevel else { return [] }
        return store.markers.filter { activityWeight(for: $0) == 0 }
    }

    private var friendPresenceAnnotations: [FriendPresenceAnnotation] {
        let currentUserId = authUserId
        var seen = Set<UUID>()
        return activeVenueMarkers.flatMap { marker in
            marker.presencePreview
                .filter { presence in
                    guard let currentUserId else { return true }
                    return presence.userId != currentUserId
                }
                .prefix(4)
                .enumerated()
                .compactMap { index, presence in
                    guard !seen.contains(presence.userId) else { return nil }
                    seen.insert(presence.userId)
                    return FriendPresenceAnnotation(
                    venueId: marker.venueId,
                    userId: presence.userId,
                    name: presence.displayName,
                    avatarUrl: presence.avatarUrl,
                    coordinate: offsetCoordinate(marker.coordinate, index: index),
                    initials: String(presence.displayName.prefix(1)).uppercased()
                )
            }
        }
        .prefix(80)
        .map { $0 }
    }

    private func offsetCoordinate(_ coordinate: CLLocationCoordinate2D, index: Int) -> CLLocationCoordinate2D {
        let offsets: [(Double, Double)] = [(0, 0), (0.000055, -0.000055), (-0.000055, 0.000055), (0.000065, 0.000065)]
        let offset = offsets[index % offsets.count]
        return CLLocationCoordinate2D(
            latitude: coordinate.latitude + offset.0,
            longitude: coordinate.longitude + offset.1
        )
    }

    private var isVenueZoomLevel: Bool {
        visibleRegion.span.latitudeDelta <= 0.025 && visibleRegion.span.longitudeDelta <= 0.025
    }

    private func activityWeight(for marker: VenueMarker) -> Int {
        marker.peopleEstimate + marker.activeCheckIns + marker.activeIntentions + marker.openTables
    }

    private var venueStoryGroups: [VenueStoryGroup] {
        Dictionary(grouping: venueStories, by: \.venueId)
            .compactMap { venueId, stories in
                guard let first = stories.first else { return nil }
                return VenueStoryGroup(
                    venueId: venueId,
                    venueName: first.venueName,
                    coordinate: first.coordinate,
                    stories: stories.sorted { $0.createdAtUtc < $1.createdAtUtc }
                )
            }
            .sorted { $0.stories.count > $1.stories.count }
    }

    private func storyViewerStories(for group: VenueStoryGroup) -> [UserStory] {
        group.stories
            .sorted { $0.createdAtUtc < $1.createdAtUtc }
            .map { story in
                UserStory(
                    id: story.id,
                    userId: story.userId,
                    nickname: story.nickname,
                    displayName: story.displayName,
                    avatarUrl: story.avatarUrl,
                    mediaUrl: story.mediaUrl,
                    caption: story.caption,
                    venueId: story.venueId,
                    venueName: story.venueName,
                    likeCount: story.likeCount,
                    commentCount: story.commentCount,
                    hasLiked: story.hasLiked,
                    createdAtUtc: story.createdAtUtc,
                    expiresAtUtc: story.expiresAtUtc
                )
            }
    }
}

private struct FriendPresenceAnnotation: Identifiable {
    let venueId: UUID
    let userId: UUID
    let name: String
    let avatarUrl: String?
    let coordinate: CLLocationCoordinate2D
    let initials: String

    var id: String { "\(venueId.uuidString)-\(userId.uuidString)" }
}

private struct VenueStoryGroup: Identifiable {
    let venueId: UUID
    let venueName: String
    let coordinate: CLLocationCoordinate2D
    let stories: [VenueStory]

    var id: UUID { venueId }
}

private struct VenueStoryMarker: View {
    let count: Int

    var body: some View {
        ZStack(alignment: .topTrailing) {
            Circle()
                .fill(Theme.Palette.blue500)
                .frame(width: 40, height: 40)
                .overlay(
                    Image(systemName: "photo.stack.fill")
                        .font(.system(size: 16, weight: .heavy))
                        .foregroundStyle(.white)
                )
                .overlay(Circle().stroke(.white, lineWidth: 3))
                .shadow(color: Theme.Palette.blue500.opacity(0.22), radius: 14, x: 0, y: 6)

            if count > 1 {
                Text(count > 9 ? "9+" : "\(count)")
                    .font(.system(size: 10, weight: .black, design: .rounded))
                    .foregroundStyle(.white)
                    .frame(minWidth: 18, minHeight: 18)
                    .background(Capsule().fill(Theme.Palette.blue700))
                    .offset(x: 5, y: -5)
            }
        }
        .accessibilityLabel("Storie del luogo")
    }
}

private struct FlareResponseSheet: View {
    let flare: FlareSignal
    let canDelete: Bool
    var onDeleted: () -> Void
    var onSent: () -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var reply = "Io ci sono"
    @State private var isSending = false
    @State private var friends: [SocialConnection] = []
    @State private var selectedRelayFriendIds: Set<UUID> = []
    @State private var relayTask: Task<Void, Never>?
    @State private var relayPending = false
    @State private var error: String?

    var body: some View {
        NavigationStack {
            VStack(alignment: .leading, spacing: Theme.Spacing.lg) {
                Text(flare.message)
                    .font(Theme.Font.display(24))
                Text("Scade \(flare.expiresAtUtc, format: .relative(presentation: .named))")
                    .font(Theme.Font.caption(12))
                    .foregroundStyle(Theme.Palette.inkSoft)
                TextField("Risposta", text: $reply, axis: .vertical)
                    .lineLimit(1...3)
                    .padding(12)
                    .background(Theme.Palette.surfaceAlt, in: RoundedRectangle(cornerRadius: Theme.Radius.md))
                if let error {
                    Text(error).font(Theme.Font.caption(12)).foregroundStyle(Theme.Palette.densityHigh)
                }
                relayChainSection
                if canDelete {
                    Button(role: .destructive) {
                        Task { await deleteFlare() }
                    } label: {
                        HStack { Image(systemName: "trash"); Text("Cancella flare") }
                            .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.ghost)
                    .disabled(isSending)
                }
                Button {
                    Task { await send() }
                } label: {
                    HStack { Image(systemName: "paperplane.fill"); Text("Rispondi") }
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(.honey)
                .disabled(isSending || reply.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                Spacer()
            }
            .padding(Theme.Spacing.lg)
            .background(Theme.Palette.surfaceAlt.ignoresSafeArea())
            .navigationTitle("Flare")
            .navigationBarTitleDisplayMode(.inline)
            .task { await loadFriends() }
            .onDisappear {
                relayTask?.cancel()
                relayTask = nil
            }
        }
    }

    private var relayChainSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text("Viral chain")
                        .font(Theme.Font.title(16, weight: .heavy))
                    Text("Rilancia a massimo 3 amici. Parte tra 3 secondi, puoi annullare.")
                        .font(Theme.Font.caption(11, weight: .semibold))
                        .foregroundStyle(Theme.Palette.inkMuted)
                }
                Spacer()
                Image(systemName: "link")
                    .font(.system(size: 17, weight: .heavy))
                    .foregroundStyle(Theme.Palette.blue500)
            }

            if friends.isEmpty {
                Text("Aggiungi amici per far viaggiare i flare.")
                    .font(Theme.Font.caption(12, weight: .semibold))
                    .foregroundStyle(Theme.Palette.inkSoft)
            } else {
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: 8) {
                        ForEach(friends.prefix(12)) { friend in
                            RelayFriendChip(
                                friend: friend,
                                isSelected: selectedRelayFriendIds.contains(friend.userId)
                            ) {
                                toggleRelayFriend(friend.userId)
                            }
                        }
                    }
                }
            }

            Button {
                relayPending ? cancelRelay() : startRelayCountdown()
            } label: {
                HStack {
                    Image(systemName: relayPending ? "arrow.uturn.backward.circle.fill" : "bolt.horizontal.circle.fill")
                    Text(relayPending ? "Annulla rilancio" : "Rilancia a \(selectedRelayFriendIds.count) amici")
                }
                .frame(maxWidth: .infinity)
            }
            .buttonStyle(CloudyButtonStyle(variant: relayPending ? .ghost : .secondary))
            .disabled(isSending || (!relayPending && selectedRelayFriendIds.isEmpty))
        }
        .padding(14)
        .background(Theme.Palette.surface, in: RoundedRectangle(cornerRadius: 20, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 20, style: .continuous)
                .stroke(Theme.Palette.blue100.opacity(0.8), lineWidth: 1)
        )
    }

    private func send() async {
        let body = reply.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !body.isEmpty else { return }
        isSending = true
        defer { isSending = false }
        do {
            _ = try await API.respondToFlare(flareId: flare.flareId, body: body)
            Haptics.success()
            onSent()
            dismiss()
        } catch {
            Haptics.error()
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func loadFriends() async {
        friends = (try? await API.socialHub().friends) ?? []
    }

    private func toggleRelayFriend(_ userId: UUID) {
        if selectedRelayFriendIds.contains(userId) {
            selectedRelayFriendIds.remove(userId)
        } else if selectedRelayFriendIds.count < 3 {
            selectedRelayFriendIds.insert(userId)
        } else {
            Haptics.error()
        }
    }

    private func startRelayCountdown() {
        guard !selectedRelayFriendIds.isEmpty else { return }
        Haptics.tap()
        relayPending = true
        error = nil
        let targets = Array(selectedRelayFriendIds)
        relayTask?.cancel()
        relayTask = Task {
            try? await Task.sleep(nanoseconds: 3_000_000_000)
            guard !Task.isCancelled else { return }
            await relay(to: targets)
        }
    }

    private func cancelRelay() {
        relayTask?.cancel()
        relayTask = nil
        relayPending = false
        Haptics.tap()
    }

    private func relay(to targets: [UUID]) async {
        isSending = true
        defer {
            isSending = false
            relayPending = false
        }
        do {
            _ = try await API.relayFlare(flareId: flare.flareId, targetUserIds: targets)
            selectedRelayFriendIds.removeAll()
            Haptics.success()
            onSent()
        } catch {
            Haptics.error()
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func deleteFlare() async {
        isSending = true
        defer { isSending = false }
        do {
            _ = try await API.deleteFlare(flareId: flare.flareId)
            Haptics.success()
            onDeleted()
            dismiss()
        } catch {
            Haptics.error()
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }
}

private struct RelayFriendChip: View {
    let friend: SocialConnection
    let isSelected: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            HStack(spacing: 7) {
                StoryAvatar(
                    url: APIClient.shared.mediaURL(from: friend.avatarUrl),
                    size: 28,
                    hasStory: false,
                    initials: String((friend.displayName ?? friend.nickname).prefix(1)).uppercased()
                )
                Text(friend.displayName ?? friend.nickname)
                    .font(Theme.Font.caption(12, weight: .heavy))
                    .lineLimit(1)
            }
            .foregroundStyle(isSelected ? .white : Theme.Palette.ink)
            .padding(.leading, 4)
            .padding(.trailing, 10)
            .padding(.vertical, 4)
            .background(
                Capsule()
                    .fill(isSelected ? Theme.Palette.blue500 : Theme.Palette.surfaceAlt)
            )
        }
        .buttonStyle(.plain)
    }
}

private struct UserLocationMarker: View {
    let avatarUrl: URL?
    let initials: String

    var body: some View {
        ZStack {
            Circle()
                .fill(Theme.Palette.blue500.opacity(0.14))
                .frame(width: 58, height: 58)
            StoryAvatar(url: avatarUrl, size: 42, hasStory: false, initials: initials)
                .overlay(
                    Circle()
                        .stroke(.white, lineWidth: 3)
                )
                .shadow(color: Theme.Palette.blue500.opacity(0.18), radius: 10, x: 0, y: 5)
            Circle()
                .fill(Theme.Palette.blue500)
                .frame(width: 12, height: 12)
                .overlay(Circle().stroke(.white, lineWidth: 2))
                .offset(x: 18, y: 18)
        }
        .accessibilityLabel(Text("La tua posizione"))
    }
}

private struct FriendPresenceMapMarker: View {
    let presence: FriendPresenceAnnotation

    var body: some View {
        StoryAvatar(
            url: APIClient.shared.mediaURL(from: presence.avatarUrl),
            size: 36,
            hasStory: false,
            initials: presence.initials
        )
        .overlay(
            Circle()
                .stroke(Theme.Palette.mint500, lineWidth: 3)
        )
        .overlay(alignment: .bottomTrailing) {
            Circle()
                .fill(Theme.Palette.mint500)
                .frame(width: 10, height: 10)
                .overlay(Circle().stroke(.white, lineWidth: 1.5))
        }
        .shadow(color: Theme.Palette.blue500.opacity(0.16), radius: 10, x: 0, y: 5)
        .accessibilityLabel(Text("\(presence.name) e qui"))
    }
}

private struct VenueDotMarker: View {
    let peopleCount: Int
    let densityLevel: String
    let energyScore: Int
    let isSelected: Bool

    private var color: Color {
        if energyScore >= 82 { return Theme.Palette.coral500 }
        if energyScore >= 62 { return Theme.Palette.densityHigh }
        switch densityLevel.lowercased() {
        case "low", "very_low": return Theme.Palette.densityLow
        case "medium": return Theme.Palette.densityMedium
        case "high": return Theme.Palette.densityHigh
        case "very_high": return Theme.Palette.densityPeak
        default: return Theme.Palette.blue400
        }
    }

    private var size: CGFloat {
        min(48, max(32, 30 + CGFloat(peopleCount) * 0.18))
    }

    var body: some View {
        ZStack {
            Circle()
                .fill(Theme.Palette.surface)
                .frame(width: size, height: size)
                .shadow(color: Theme.Palette.blue500.opacity(0.08), radius: 12, x: 0, y: 6)
            if energyScore >= 62 {
                Circle()
                    .stroke(color.opacity(0.18), lineWidth: 7)
                    .frame(width: size + 12, height: size + 12)
            }
            Circle()
                .fill(color.opacity(0.20))
                .frame(width: size - 6, height: size - 6)
            Circle()
                .stroke(isSelected ? Theme.Palette.blue500 : color.opacity(0.72), lineWidth: isSelected ? 2.5 : 1.2)
                .frame(width: size, height: size)
            if peopleCount > 0 {
                Text("\(peopleCount)")
                    .font(Theme.Font.heroNumber(peopleCount > 99 ? 12 : 14).monospacedDigit())
                    .foregroundStyle(Theme.Palette.ink)
                    .contentTransition(.numericText())
            } else {
                Image(systemName: "mappin")
                    .font(.system(size: 13, weight: .bold))
                    .foregroundStyle(Theme.Palette.blue500)
            }
            if energyScore >= 38 {
                Image(systemName: "bolt.fill")
                    .font(.system(size: 9, weight: .black))
                    .foregroundStyle(.white)
                    .frame(width: 18, height: 18)
                    .background(Circle().fill(color))
                    .overlay(Circle().stroke(.white, lineWidth: 1.5))
                    .offset(x: size * 0.36, y: -size * 0.34)
            }
        }
        .accessibilityLabel(Text(peopleCount > 0 ? "\(peopleCount) persone, energia \(energyScore)" : "Locale"))
    }
}

private struct LocalFlare: Identifiable, Hashable {
    let id = UUID()
    let latitude: Double
    let longitude: Double
    let message: String

    var coordinate: CLLocationCoordinate2D {
        CLLocationCoordinate2D(latitude: latitude, longitude: longitude)
    }
}

private struct FlareMapBurst: View {
    let message: String

    var body: some View {
        VStack(spacing: 5) {
            Image(systemName: "sparkles")
                .font(.system(size: 20, weight: .heavy))
                .foregroundStyle(.white)
                .frame(width: 46, height: 46)
                .background(Circle().fill(Theme.Palette.blue500))
                .shadow(color: Theme.Palette.blue500.opacity(0.16), radius: 14, x: 0, y: 6)
            if !message.isEmpty {
                Text(message)
                    .font(Theme.Font.caption(11, weight: .bold))
                    .foregroundStyle(Theme.Palette.ink)
                    .lineLimit(1)
                    .padding(.horizontal, 9)
                    .padding(.vertical, 5)
                    .background(.thinMaterial, in: Capsule())
                    .frame(maxWidth: 170)
            }
        }
    }
}
