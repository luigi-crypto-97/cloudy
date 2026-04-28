//
//  TablesView.swift
//  Cloudy — Tavoli sociali (card swipe stile Bumble)
//
//  Mostra inviti a tavoli + tavoli aperti vicino. Le card hanno gesture
//  drag-rotate-swipe come Bumble.
//

import SwiftUI
import MapKit

@MainActor
@Observable
final class TablesStore {
    var myTables: [SocialTableSummary] = []
    var hub: SocialHub?
    var isLoading: Bool = false
    var error: String?

    func load() async {
        isLoading = true
        error = nil
        defer { isLoading = false }
        do {
            async let m = API.myTables()
            async let h = API.socialHub()
            self.myTables = try await m
            self.hub = try await h
        } catch {
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }
}

struct TablesView: View {
    @State private var store = TablesStore()
    @State private var swipingIndex: Int = 0
    @State private var showCreateTable = false

    var body: some View {
        NavigationStack {
            ZStack {
                Theme.Palette.surfaceAlt.ignoresSafeArea()

                ScrollView {
                    VStack(alignment: .leading, spacing: Theme.Spacing.xl) {

                        header

                        invitesSection

                        myTablesSection
                    }
                    .padding(.horizontal, Theme.Spacing.lg)
                    .padding(.top, Theme.Spacing.md)
                    .padding(.bottom, 130)
                }
            }
            .navigationTitle("Tavoli")
            .navigationBarTitleDisplayMode(.large)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        showCreateTable = true
                        Haptics.tap()
                    } label: {
                        Image(systemName: "plus")
                            .font(.system(size: 17, weight: .heavy))
                    }
                    .accessibilityLabel("Crea tavolo")
                }
            }
            .sheet(isPresented: $showCreateTable) {
                CreateTableFlowView(onCreated: {
                    Task { await store.load() }
                })
            }
            .refreshable { await store.load() }
            .task { await store.load() }
        }
    }

    // MARK: - Sections

    private var header: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("Trova il tuo tavolo")
                .font(Theme.Font.display(28))
                .foregroundStyle(Theme.Palette.ink)
            Text("Inviti, tavoli aperti vicino a te, e i tuoi piani.")
                .font(Theme.Font.body())
                .foregroundStyle(Theme.Palette.inkSoft)
        }
    }

    @ViewBuilder
    private var invitesSection: some View {
        if let invites = store.hub?.tableInvites, !invites.isEmpty {
            VStack(alignment: .leading, spacing: Theme.Spacing.md) {
                Text("Inviti")
                    .font(Theme.Font.title(18))
                    .foregroundStyle(Theme.Palette.ink)
                inviteStack(invites: invites)
            }
        }
    }

    private func inviteStack(invites: [SocialTableInvite]) -> some View {
        let pending = Array(invites.dropFirst(swipingIndex))
        return ZStack {
            ForEach(Array(pending.prefix(3).enumerated()).reversed(), id: \.offset) { (offset, invite) in
                InviteCard(
                    invite: invite,
                    onAccept: {
                        Task {
                            _ = try? await API.joinTable(tableId: invite.tableId)
                            await store.load()
                        }
                        advance()
                    },
                    onReject: { advance() }
                )
                .scaleEffect(1 - CGFloat(offset) * 0.04)
                .offset(y: CGFloat(offset) * 8)
                .zIndex(Double(3 - offset))
                .animation(.spring(response: 0.4), value: swipingIndex)
            }
            if pending.isEmpty {
                CloudyEmptyState(
                    icon: "checkmark.circle.fill",
                    title: "Tutto fatto",
                    message: "Hai gestito tutti gli inviti. Bel lavoro."
                )
                .frame(height: 280)
            }
        }
        .frame(height: 320)
    }

    private func advance() {
        Haptics.tap()
        withAnimation { swipingIndex += 1 }
    }

    @ViewBuilder
    private var myTablesSection: some View {
        VStack(alignment: .leading, spacing: Theme.Spacing.md) {
            Text("I tuoi tavoli")
                .font(Theme.Font.title(18))

            if store.isLoading && store.myTables.isEmpty {
                ProgressView().padding()
            } else if store.myTables.isEmpty {
                CloudyEmptyState(
                    icon: "person.3",
                    title: "Nessun tavolo attivo",
                    message: "Quando crei o partecipi a un tavolo lo trovi qui."
                )
            } else {
                ForEach(store.myTables) { t in
                    NavigationLink {
                        TableThreadView(tableId: t.tableId)
                    } label: {
                        MyTableRow(table: t)
                    }
                    .buttonStyle(.plain)
                }
            }
        }
    }
}

// MARK: - Create table

private struct CreateTableFlowView: View {
    var onCreated: () -> Void

    @Environment(\.dismiss) private var dismiss
    @Environment(AuthStore.self) private var auth
    @Environment(LiveLocationStore.self) private var liveLocation

    @State private var venueQuery = ""
    @State private var venues: [VenueMarker] = []
    @State private var selectedVenue: VenueMarker?
    @State private var title = ""
    @State private var description = ""
    @State private var startsAt = Date().addingTimeInterval(60 * 60)
    @State private var capacity = 4
    @State private var joinPolicy = "auto"
    @State private var friends: [SocialConnection] = []
    @State private var selectedFriendIds: Set<UUID> = []
    @State private var isLoadingVenues = false
    @State private var isSaving = false
    @State private var error: String?

    var body: some View {
        NavigationStack {
            Form {
                Section("Luogo") {
                    TextField("Cerca locale", text: $venueQuery)
                        .textInputAutocapitalization(.words)
                        .onSubmit { Task { await searchVenues() } }
                    Button(isLoadingVenues ? "Cerco…" : "Cerca") {
                        Task { await searchVenues() }
                    }
                    .disabled(isLoadingVenues)

                    ForEach(venues.prefix(8)) { venue in
                        Button {
                            selectedVenue = venue
                            if title.isEmpty {
                                title = "Tavolo da \(venue.name)"
                            }
                        } label: {
                            HStack {
                                VStack(alignment: .leading, spacing: 2) {
                                    Text(venue.name)
                                        .foregroundStyle(Theme.Palette.ink)
                                    Text(venue.city)
                                        .font(Theme.Font.caption(12))
                                        .foregroundStyle(Theme.Palette.inkMuted)
                                }
                                Spacer()
                                if selectedVenue?.venueId == venue.venueId {
                                    Image(systemName: "checkmark.circle.fill")
                                        .foregroundStyle(Theme.Palette.blue500)
                                }
                            }
                        }
                    }
                }

                Section("Tavolo") {
                    TextField("Tema del tavolo", text: $title)
                    TextField("Nota opzionale", text: $description, axis: .vertical)
                    DatePicker("Data e ora", selection: $startsAt, in: Date()..., displayedComponents: [.date, .hourAndMinute])
                    Stepper("Posti: \(capacity)", value: $capacity, in: 2...20)
                    Picker("Ingresso", selection: $joinPolicy) {
                        Text("Libero finché ci sono posti").tag("auto")
                        Text("Con approvazione").tag("approval")
                    }
                }

                if !friends.isEmpty {
                    Section("Invita amici") {
                        ForEach(friends) { friend in
                            Toggle(isOn: binding(for: friend.userId)) {
                                HStack(spacing: 10) {
                                    StoryAvatar(
                                        url: APIClient.shared.mediaURL(from: friend.avatarUrl),
                                        size: 34,
                                        initials: String((friend.displayName ?? friend.nickname).prefix(1)).uppercased()
                                    )
                                    Text(friend.displayName ?? friend.nickname)
                                }
                            }
                        }
                    }
                }

                if let error {
                    Section {
                        Text(error).foregroundStyle(Theme.Palette.densityHigh)
                    }
                }
            }
            .navigationTitle("Nuovo tavolo")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Annulla") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button(isSaving ? "Creo…" : "Crea") {
                        Task { await create() }
                    }
                    .disabled(isSaving || selectedVenue == nil || cleanTitle.isEmpty)
                }
            }
            .task {
                async let venueLoad: Void = searchVenues()
                async let friendLoad: Void = loadFriends()
                _ = await (venueLoad, friendLoad)
            }
        }
    }

    private var cleanTitle: String {
        title.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private var currentUserId: UUID? {
        if case .loggedIn(let user) = auth.state {
            return user.userId
        }
        return nil
    }

    private func binding(for userId: UUID) -> Binding<Bool> {
        Binding(
            get: { selectedFriendIds.contains(userId) },
            set: { isSelected in
                if isSelected {
                    selectedFriendIds.insert(userId)
                } else {
                    selectedFriendIds.remove(userId)
                }
            }
        )
    }

    private func searchVenues() async {
        isLoadingVenues = true
        defer { isLoadingVenues = false }
        let center = liveLocation.currentLocation?.coordinate ?? MapStore.milanDefault.center
        let delta = 0.22
        do {
            venues = try await API.venueMap(
                minLat: center.latitude - delta,
                minLng: center.longitude - delta,
                maxLat: center.latitude + delta,
                maxLng: center.longitude + delta,
                query: venueQuery.trimmingCharacters(in: .whitespacesAndNewlines).nilIfEmpty,
                centerLat: center.latitude,
                centerLng: center.longitude,
                maxDistanceKm: 30
            )
            if selectedVenue == nil {
                selectedVenue = venues.first
            }
        } catch {
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func loadFriends() async {
        friends = (try? await API.socialHub().friends) ?? []
    }

    private func create() async {
        guard let currentUserId, let selectedVenue else { return }
        isSaving = true
        error = nil
        defer { isSaving = false }
        do {
            let table = try await API.createTable(CreateSocialTableRequest(
                hostUserId: currentUserId,
                venueId: selectedVenue.venueId,
                title: cleanTitle,
                description: description.trimmingCharacters(in: .whitespacesAndNewlines).nilIfEmpty,
                startsAtUtc: startsAt,
                capacity: capacity,
                joinPolicy: joinPolicy
            ))
            for userId in selectedFriendIds {
                _ = try? await API.inviteToTable(tableId: table.tableId, targetUserId: userId)
            }
            Haptics.success()
            onCreated()
            dismiss()
        } catch {
            Haptics.error()
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }
}

private extension String {
    var nilIfEmpty: String? {
        isEmpty ? nil : self
    }
}

// MARK: - Invite swipe card (Bumble-style)

struct InviteCard: View {
    let invite: SocialTableInvite
    let onAccept: () -> Void
    let onReject: () -> Void

    @State private var dragOffset: CGSize = .zero

    private var rotation: Angle {
        .degrees(Double(dragOffset.width / 18))
    }
    private var swipeProgress: Double {
        Double(dragOffset.width) / 140.0
    }

    var body: some View {
        ZStack(alignment: .top) {
            // Card
            VStack(alignment: .leading, spacing: Theme.Spacing.md) {
                HStack(spacing: 12) {
                    StoryAvatar(
                        url: URL(string: invite.hostAvatarUrl ?? ""),
                        size: 56,
                        hasStory: false,
                        initials: String((invite.hostDisplayName ?? invite.hostNickname).prefix(1)).uppercased()
                    )
                    VStack(alignment: .leading, spacing: 2) {
                        Text(invite.hostDisplayName ?? invite.hostNickname)
                            .font(Theme.Font.body(15, weight: .bold))
                        Text("ti ha invitato")
                            .font(Theme.Font.caption(12))
                            .foregroundStyle(Theme.Palette.inkSoft)
                    }
                    Spacer()
                }

                Text(invite.title)
                    .font(Theme.Font.display(24))
                    .foregroundStyle(Theme.Palette.ink)
                    .lineLimit(2)

                HStack(spacing: 8) {
                    CloudyPill(text: invite.venueName, icon: "mappin.circle.fill", tone: .neutral)
                    CloudyPill(text: invite.venueCategory.capitalized, tone: .neutral)
                }

                Text(invite.startsAtUtc, format: .dateTime.weekday(.wide).day().month().hour().minute())
                    .font(Theme.Font.body(14, weight: .semibold))
                    .foregroundStyle(Theme.Palette.honeyDeep)

                Spacer()

                HStack(spacing: 12) {
                    Button(action: onReject) {
                        HStack { Image(systemName: "xmark"); Text("Rifiuta") }
                            .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.ghost)

                    Button(action: onAccept) {
                        HStack { Image(systemName: "checkmark"); Text("Accetta") }
                            .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.honey)
                }
            }
            .padding(Theme.Spacing.lg)
            .frame(maxWidth: .infinity, minHeight: 300)
            .background(
                RoundedRectangle(cornerRadius: Theme.Radius.xl, style: .continuous)
                    .fill(Theme.Palette.surface)
            )
            .cardShadow()

            // Swipe overlays
            if swipeProgress > 0.1 {
                badge(text: "ACCETTO", color: Theme.Palette.densityLow, side: .leading)
                    .opacity(min(1, swipeProgress * 1.3))
            } else if swipeProgress < -0.1 {
                badge(text: "PASSO", color: Theme.Palette.densityHigh, side: .trailing)
                    .opacity(min(1, -swipeProgress * 1.3))
            }
        }
        .offset(dragOffset)
        .rotationEffect(rotation)
        .gesture(
            DragGesture()
                .onChanged { v in dragOffset = v.translation }
                .onEnded { v in
                    if v.translation.width > 110 {
                        withAnimation(.spring()) { dragOffset = CGSize(width: 600, height: v.translation.height) }
                        Task {
                            try? await Task.sleep(nanoseconds: 200_000_000)
                            onAccept()
                            dragOffset = .zero
                        }
                    } else if v.translation.width < -110 {
                        withAnimation(.spring()) { dragOffset = CGSize(width: -600, height: v.translation.height) }
                        Task {
                            try? await Task.sleep(nanoseconds: 200_000_000)
                            onReject()
                            dragOffset = .zero
                        }
                    } else {
                        withAnimation(.spring()) { dragOffset = .zero }
                    }
                }
        )
    }

    private func badge(text: String, color: Color, side: HorizontalAlignment) -> some View {
        HStack {
            if side == .trailing { Spacer() }
            Text(text)
                .font(Theme.Font.title(20, weight: .heavy))
                .foregroundStyle(color)
                .padding(.horizontal, 14).padding(.vertical, 8)
                .background(
                    RoundedRectangle(cornerRadius: 8)
                        .stroke(color, lineWidth: 3)
                )
                .rotationEffect(.degrees(side == .leading ? -16 : 16))
                .padding(.top, 24)
                .padding(.horizontal, 24)
            if side == .leading { Spacer() }
        }
    }
}

// MARK: - My table row

struct MyTableRow: View {
    let table: SocialTableSummary
    var body: some View {
        SectionCard {
            HStack(spacing: 12) {
                ZStack {
                    Circle().fill(Theme.Gradients.honeyCTA).frame(width: 48, height: 48)
                    Image(systemName: table.isHost ? "crown.fill" : "person.3.fill")
                        .foregroundStyle(.white)
                }
                VStack(alignment: .leading, spacing: 2) {
                    Text(table.title)
                        .font(Theme.Font.body(15, weight: .bold))
                    Text(table.venueName)
                        .font(Theme.Font.caption(12))
                        .foregroundStyle(Theme.Palette.inkSoft)
                    Text(table.startsAtUtc, format: .relative(presentation: .named))
                        .font(Theme.Font.caption(11))
                        .foregroundStyle(Theme.Palette.inkMuted)
                }
                Spacer()
                CloudyPill(
                    text: "\(table.acceptedCount)/\(table.capacity)",
                    icon: "person.2.fill",
                    tone: .honey
                )
            }
        }
    }
}
