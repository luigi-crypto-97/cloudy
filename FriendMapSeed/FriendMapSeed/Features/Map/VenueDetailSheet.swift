//
//  VenueDetailSheet.swift
//  Cloudy — Bottom sheet di dettaglio venue (Bumble-like card)
//

import SwiftUI

struct VenueDetailSheet: View {
    let venue: VenueMarker
    @Environment(\.dismiss) private var dismiss
    @Environment(AuthStore.self) private var auth
    @State private var actionMessage: String?
    @State private var isSubmittingAction = false
    @State private var showsCreateStory = false
    @State private var showsCreateTable = false
    @State private var showsVenueChat = false
    @State private var venueStories: [VenueStory] = []
    @State private var selectedStoryRoute: VenueStoryViewerRoute?

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Theme.Spacing.lg) {

                // Hero
                ZStack(alignment: .topTrailing) {
                    AsyncImage(url: URL(string: venue.coverImageUrl ?? "")) { phase in
                        switch phase {
                        case .success(let img):
                            img.resizable().scaledToFill()
                        default:
                            ZStack {
                                Theme.Gradients.honeyCTA
                                Image(systemName: iconForCategory(venue.category))
                                    .font(.system(size: 56, weight: .semibold))
                                    .foregroundStyle(.white.opacity(0.9))
                            }
                        }
                    }
                    .frame(height: 180)
                    .frame(maxWidth: .infinity)
                    .clipped()
                    .clipShape(RoundedRectangle(cornerRadius: Theme.Radius.lg, style: .continuous))

                    // Open badge
                    HStack(spacing: 6) {
                        Circle()
                            .fill(venue.isOpenNow ? Theme.Palette.densityLow : Theme.Palette.densityHigh)
                            .frame(width: 8, height: 8)
                        Text(venue.isOpenNow ? "Aperto ora" : "Chiuso")
                            .font(Theme.Font.caption(11, weight: .bold))
                    }
                    .padding(.horizontal, 10)
                    .padding(.vertical, 6)
                    .background(.thinMaterial, in: Capsule())
                    .padding(10)
                }

                // Title
                VStack(alignment: .leading, spacing: 4) {
                    Text(venue.name)
                        .font(Theme.Font.display(26, weight: .heavy))
                        .foregroundStyle(Theme.Palette.ink)
                    Text("\(venue.category.capitalized) · \(venue.city)")
                        .font(Theme.Font.body())
                        .foregroundStyle(Theme.Palette.inkSoft)
                }

                // Density / stats
                HStack(spacing: Theme.Spacing.md) {
                    statTile(icon: "person.2.fill", value: "\(venue.peopleEstimate)", label: "Persone")
                    statTile(icon: "checkmark.circle.fill", value: "\(venue.activeCheckIns)", label: "Check-in")
                    statTile(icon: "hand.raised.fill", value: "\(venue.activeIntentions)", label: "Piani")
                    statTile(icon: "person.3.fill", value: "\(venue.openTables)", label: "Tavoli")
                }

                // Density indicator + tags
                VStack(alignment: .leading, spacing: 8) {
                    DensityIndicator(level: venue.densityLevel, count: venue.peopleEstimate)
                    if !venue.tags.isEmpty {
                        ScrollView(.horizontal, showsIndicators: false) {
                            HStack(spacing: 6) {
                                ForEach(venue.tags, id: \.self) { tag in
                                    CloudyPill(text: tag, tone: .neutral)
                                }
                            }
                        }
                    }
                }

                // Description
                if let desc = venue.description, !desc.isEmpty {
                    Text(desc)
                        .font(Theme.Font.body(15))
                        .foregroundStyle(Theme.Palette.ink)
                        .lineSpacing(2)
                }

                // Presence preview (Instagram-like avatars row)
                if !venue.presencePreview.isEmpty {
                    SectionCard {
                        Text("Amici qui ora")
                            .font(Theme.Font.title(16))
                        HStack(spacing: -10) {
                            ForEach(venue.presencePreview.prefix(6)) { p in
                                StoryAvatar(
                                    url: URL(string: p.avatarUrl ?? ""),
                                    size: 44,
                                    hasStory: true,
                                    initials: String(p.displayName.prefix(1))
                                )
                            }
                            if venue.presencePreview.count > 6 {
                                Text("+\(venue.presencePreview.count - 6)")
                                    .font(Theme.Font.caption(13, weight: .bold))
                                    .padding(10)
                                    .background(Circle().fill(Theme.Palette.surfaceAlt))
                            }
                        }
                    }
                }

                if !venueStories.isEmpty {
                    SectionCard {
                        HStack {
                            Text("Foto scattate qui")
                                .font(Theme.Font.title(16))
                            Spacer()
                            Text("\(venueStories.count)")
                                .font(Theme.Font.caption(12, weight: .bold))
                                .foregroundStyle(Theme.Palette.inkMuted)
                        }
                        ScrollView(.horizontal, showsIndicators: false) {
                            HStack(spacing: 10) {
                                ForEach(venueStories) { story in
                                    Button {
                                        selectedStoryRoute = VenueStoryViewerRoute(stories: storyViewerStories(startingFrom: story))
                                    } label: {
                                        VenueStoryThumbnail(story: story)
                                    }
                                    .buttonStyle(.plain)
                                }
                            }
                        }
                    }
                }

                // CTAs
                VStack(spacing: 10) {
                    Button {
                        Task { await checkInNow() }
                    } label: {
                        HStack {
                            Image(systemName: isSubmittingAction ? "hourglass" : "hand.thumbsup.fill")
                            Text("Sono qui ora")
                        }
                        .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.honey)
                    .disabled(isSubmittingAction)

                    Button {
                        Task { await planTonight() }
                    } label: {
                        HStack {
                            Image(systemName: "calendar.badge.plus")
                            Text("Pianifica un'uscita")
                        }
                        .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.ghost)
                    .disabled(isSubmittingAction)

                    Button {
                        showsCreateStory = true
                    } label: {
                        HStack {
                            Image(systemName: "camera.circle.fill")
                            Text("Posta foto qui per 24h")
                        }
                        .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.ghost)

                    Button {
                        showsCreateTable = true
                    } label: {
                        HStack {
                            Image(systemName: "person.3.fill")
                            Text("Crea tavolo sociale")
                        }
                        .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.ghost)

                    Button {
                        showsVenueChat = true
                    } label: {
                        HStack {
                            Image(systemName: "bubble.left.and.bubble.right.fill")
                            Text("Chat del locale")
                        }
                        .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.ghost)

                    if let actionMessage {
                        Text(actionMessage)
                            .font(Theme.Font.caption(12, weight: .semibold))
                            .foregroundStyle(Theme.Palette.inkSoft)
                            .frame(maxWidth: .infinity, alignment: .leading)
                    }
                }
                .padding(.top, 4)

                // Address / contacts
                if !venue.addressLine.isEmpty {
                    rowItem(icon: "mappin.circle.fill", text: "\(venue.addressLine), \(venue.city)")
                }
                if let phone = venue.phoneNumber, !phone.isEmpty {
                    rowItem(icon: "phone.fill", text: phone)
                }
                if let hrs = venue.hoursSummary, !hrs.isEmpty {
                    rowItem(icon: "clock.fill", text: hrs)
                }
            }
            .padding(Theme.Spacing.lg)
        }
        .sheet(isPresented: $showsCreateStory) {
            CreateStoryView(venue: venue) {
                actionMessage = "Storia pubblicata sopra \(venue.name) per 24 ore."
                Task { await loadVenueStories() }
            }
        }
        .sheet(isPresented: $showsCreateTable) {
            CreateSocialTableSheet(venue: venue) { message in
                actionMessage = message
            }
        }
        .sheet(isPresented: $showsVenueChat) {
            NavigationStack {
                GroupChatRoomView(venueId: venue.venueId, title: "Chat di \(venue.name)")
            }
        }
        .fullScreenCover(item: $selectedStoryRoute) { route in
            StoryViewerView(stories: route.stories)
        }
        .task {
            await loadVenueStories()
        }
    }

    private func statTile(icon: String, value: String, label: String) -> some View {
        VStack(spacing: 2) {
            Image(systemName: icon)
                .font(.system(size: 16, weight: .bold))
                .foregroundStyle(Theme.Palette.honeyDeep)
            Text(value)
                .font(Theme.Font.title(20, weight: .heavy))
                .foregroundStyle(Theme.Palette.ink)
            Text(label)
                .font(Theme.Font.caption(11))
                .foregroundStyle(Theme.Palette.inkMuted)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 12)
        .background(
            RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                .fill(Theme.Palette.surfaceAlt)
        )
    }

    private func rowItem(icon: String, text: String) -> some View {
        HStack(spacing: 10) {
            Image(systemName: icon)
                .foregroundStyle(Theme.Palette.honeyDeep)
            Text(text)
                .font(Theme.Font.body(14))
                .foregroundStyle(Theme.Palette.ink)
            Spacer()
        }
    }

    private func iconForCategory(_ c: String) -> String {
        switch c.lowercased() {
        case "bar", "pub":            return "wineglass.fill"
        case "restaurant", "ristorante": return "fork.knife"
        case "cafe", "caffè":         return "cup.and.saucer.fill"
        case "club", "discoteca":     return "music.note"
        default:                      return "mappin.and.ellipse"
        }
    }

    private var currentUserId: UUID? {
        if case .loggedIn(let user) = auth.state {
            return user.userId
        }
        return nil
    }

    private func checkInNow() async {
        guard let currentUserId else { return }
        isSubmittingAction = true
        actionMessage = nil
        defer { isSubmittingAction = false }
        do {
            try await API.checkIn(venueId: venue.venueId, userId: currentUserId)
            Haptics.success()
            actionMessage = "Check-in attivo per le prossime ore."
        } catch {
            Haptics.error()
            actionMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func planTonight() async {
        guard let currentUserId else { return }
        let start = Date().addingTimeInterval(60 * 60)
        let end = start.addingTimeInterval(3 * 60 * 60)
        isSubmittingAction = true
        actionMessage = nil
        defer { isSubmittingAction = false }
        do {
            try await API.createIntention(
                venueId: venue.venueId,
                userId: currentUserId,
                startsAtUtc: start,
                endsAtUtc: end,
                note: "Ci sto pensando"
            )
            Haptics.success()
            actionMessage = "Piano aggiunto per stasera."
        } catch {
            Haptics.error()
            actionMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func loadVenueStories() async {
        do {
            venueStories = try await API.venueStories()
                .filter { $0.venueId == venue.venueId }
                .sorted { $0.createdAtUtc > $1.createdAtUtc }
        } catch {
            venueStories = []
        }
    }

    private func storyViewerStories(startingFrom selected: VenueStory) -> [UserStory] {
        let ordered = venueStories.sorted { lhs, rhs in
            if lhs.id == selected.id { return true }
            if rhs.id == selected.id { return false }
            return lhs.createdAtUtc < rhs.createdAtUtc
        }
        return ordered.map { story in
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

private struct VenueStoryViewerRoute: Identifiable {
    let id = UUID()
    let stories: [UserStory]
}

private struct VenueStoryThumbnail: View {
    let story: VenueStory

    var body: some View {
        ZStack(alignment: .bottomLeading) {
            AsyncImage(url: APIClient.shared.mediaURL(from: story.mediaUrl)) { phase in
                switch phase {
                case .success(let image):
                    image.resizable().scaledToFill()
                default:
                    Rectangle()
                        .fill(Theme.Palette.blue50)
                        .overlay(
                            Image(systemName: "photo.fill")
                                .foregroundStyle(Theme.Palette.blue500)
                        )
                }
            }
            .frame(width: 86, height: 116)
            .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))

            Text(story.displayName ?? story.nickname)
                .font(Theme.Font.caption(10, weight: .bold))
                .foregroundStyle(.white)
                .lineLimit(1)
                .padding(7)
                .frame(width: 86, alignment: .leading)
                .background(
                    LinearGradient(colors: [.clear, .black.opacity(0.55)], startPoint: .top, endPoint: .bottom)
                        .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
                )
        }
    }
}

private struct CreateSocialTableSheet: View {
    let venue: VenueMarker
    var onCreated: (String) -> Void

    @Environment(\.dismiss) private var dismiss
    @Environment(AuthStore.self) private var auth
    @State private var title = ""
    @State private var description = ""
    @State private var startsAt = Date().addingTimeInterval(60 * 60)
    @State private var capacity = 4
    @State private var joinPolicy = "auto"
    @State private var isSaving = false
    @State private var error: String?

    var body: some View {
        NavigationStack {
            Form {
                Section("Locale") {
                    Text(venue.name)
                    Text(venue.city).foregroundStyle(.secondary)
                }
                Section("Dettagli") {
                    TextField("Titolo", text: $title)
                    TextField("Descrizione opzionale", text: $description, axis: .vertical)
                    DatePicker("Orario", selection: $startsAt, in: Date()..., displayedComponents: [.date, .hourAndMinute])
                    Stepper("Posti: \(capacity)", value: $capacity, in: 2...20)
                    Picker("Ingresso", selection: $joinPolicy) {
                        Text("Chi prima arriva entra").tag("auto")
                        Text("Approvazione host").tag("approval")
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
                    .disabled(isSaving || cleanTitle.isEmpty)
                }
            }
        }
        .onAppear {
            if title.isEmpty {
                title = "Tavolo da \(venue.name)"
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

    private func create() async {
        guard let currentUserId else { return }
        isSaving = true
        error = nil
        defer { isSaving = false }
        do {
            let table = try await API.createTable(CreateSocialTableRequest(
                hostUserId: currentUserId,
                venueId: venue.venueId,
                title: cleanTitle,
                description: description.trimmingCharacters(in: .whitespacesAndNewlines).nilIfEmpty,
                startsAtUtc: startsAt,
                capacity: capacity,
                joinPolicy: joinPolicy
            ))
            Haptics.success()
            onCreated("Tavolo creato: \(table.acceptedCount)/\(table.capacity) posti occupati.")
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

// MARK: - Filter sheet

struct MapFiltersSheet: View {
    @Environment(MapStore.self) private var store
    @Environment(\.dismiss) private var dismiss
    @State private var query: String = ""
    @State private var category: String = "all"
    @State private var openNow: Bool = false

    private let categories: [(String, String)] = [
        ("all", "Tutti"),
        ("bar", "Bar"),
        ("restaurant", "Ristoranti"),
        ("cafe", "Caffè"),
        ("club", "Locali notturni")
    ]

    var body: some View {
        NavigationStack {
            VStack(alignment: .leading, spacing: Theme.Spacing.lg) {
                Text("Filtri")
                    .font(Theme.Font.display(28))

                TextField("Cerca un posto…", text: $query)
                    .textFieldStyle(.plain)
                    .padding(12)
                    .background(Theme.Palette.surfaceAlt, in: RoundedRectangle(cornerRadius: 12))

                Text("Categoria")
                    .font(Theme.Font.title(16))
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: 8) {
                        ForEach(categories, id: \.0) { (key, label) in
                            FilterChip(
                                label: label,
                                isSelected: category == key,
                                action: { category = key }
                            )
                        }
                    }
                }

                Toggle(isOn: $openNow) {
                    Text("Aperto ora")
                        .font(Theme.Font.body(15, weight: .semibold))
                }
                .tint(Theme.Palette.honey)

                Spacer()

                Button {
                    store.applyFilters(query: query, category: category, openNow: openNow)
                    dismiss()
                } label: {
                    Text("Applica filtri")
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(.honey)
            }
            .padding(Theme.Spacing.lg)
            .onAppear {
                query = store.query
                category = store.category
                openNow = store.openNowOnly
            }
        }
    }
}
