//
//  FeedView.swift
//  Cloudy
//
//  Feed V2: decision engine interleavato. Non e una timeline: ordina segnali
//  live, social proof, stories, tavoli e flares per aiutare a decidere dove
//  andare adesso.
//

import CoreLocation
import SwiftUI

@MainActor
@Observable
final class FeedStore {
    var context: FeedContext?
    var items: [FeedItem] = []
    var isLoading = false
    var error: String?
    var sessionFatigue: [String: Int] = [:]

    private let contextService = FeedContextService()
    private let ranking = FeedRankingService()

    func load(location: CLLocation?, showSpinner: Bool = true) async {
        if showSpinner { isLoading = true }
        error = nil
        defer { if showSpinner { isLoading = false } }

        let loaded = await contextService.load(location: location, previousContext: context)
        context = loaded
        items = ranking.rankedItems(context: loaded, fatigue: sessionFatigue)
    }

    func markSeen(_ item: FeedItem) {
        sessionFatigue[item.id, default: 0] += 1
        Task {
            _ = try? await API.updateFeedFatigue(cardKey: item.id)
        }
    }

    var stories: [UserStory] {
        context?.stories ?? []
    }

    var groupedStories: [[UserStory]] {
        Dictionary(grouping: stories) { $0.userId }
            .values
            .sorted { ($0.first?.createdAtUtc ?? .distantPast) > ($1.first?.createdAtUtc ?? .distantPast) }
    }

    var heroHotspot: FeedItem? {
        items.first { $0.kind == .hotspotVenue }
    }

    var liveMomentItems: [FeedItem] {
        items.filter {
            $0.kind == .friendsActivity || $0.kind == .venueStoryStack || $0.kind == .arrivalForecast
        }
        .prefix(3)
        .map { $0 }
    }

    var remainingItems: [FeedItem] {
        let hiddenIds = Set(([heroHotspot].compactMap { $0 } + liveMomentItems).map(\.id))
        return items.filter { !hiddenIds.contains($0.id) }
    }
}

struct StoryViewerConfig: Identifiable {
    let id = UUID()
    let storiesByUser: [[UserStory]]
    let initialUserIndex: Int
}

struct FeedView: View {
    @Environment(AppRouter.self) private var router
    @Environment(LiveLocationStore.self) private var liveLocation

    @State private var store = FeedStore()
    @State private var showCreateStory = false
    @State private var viewerConfig: StoryViewerConfig?
    @State private var selectedTable: TableRoute?
    @State private var showChats = false
    @State private var showNotifications = false
    @State private var showPrivacy = false
    @State private var showGamification = false
    @State private var statusMessage: String?
    @State private var privacyExplanation: FeedPrivacyEnvelope?
    @State private var impressed = Set<String>()
    @State private var unreadNotifications = 0

    private let analytics = FeedAnalytics()

    var body: some View {
        NavigationStack {
            ZStack {
                FeedSurfaceBackground().ignoresSafeArea()

                ScrollView {
                    LazyVStack(spacing: 18) {
                        topChrome
                            .padding(.horizontal, 22)

                        if let hero = store.heroHotspot, case .hotspotVenue(let payload) = hero.payload {
                            LivePulseHeroCard(item: hero, payload: payload, onCTA: handleCTA)
                                .padding(.horizontal, 18)
                        } else {
                            feedHeader
                                .padding(.horizontal, 18)
                        }

                        storiesRail
                            .padding(.horizontal, 18)

                        if !store.liveMomentItems.isEmpty {
                            liveMomentsSection
                                .padding(.horizontal, 18)
                        }

                        if let gamification = store.context?.gamification {
                            FeedGamificationCard(summary: gamification) {
                                Haptics.tap()
                                showGamification = true
                            }
                            .padding(.horizontal, 18)
                        }

                        if store.isLoading && store.items.isEmpty {
                            feedSkeleton
                                .padding(.horizontal, 18)
                        } else {
                            ForEach(Array(store.remainingItems.enumerated()), id: \.element.id) { index, item in
                                FeedItemRenderer(
                                    item: item,
                                    rank: index,
                                    onCTA: handleCTA,
                                    onPrivacy: { selected in
                                        analytics.track(.privacyExplainerOpened, item: selected, rank: index)
                                        privacyExplanation = selected.privacy
                                    },
                                    onImpression: trackImpression
                                )
                                .padding(.horizontal, 18)
                                .transition(.asymmetric(insertion: .scale(scale: 0.98).combined(with: .opacity), removal: .opacity))
                            }
                        }

                        if let error = store.error {
                            Text(error)
                                .font(Theme.Font.caption(12, weight: .semibold))
                                .foregroundStyle(Theme.Palette.coral500)
                                .padding(.horizontal, 18)
                        }
                    }
                    .padding(.top, 18)
                    .padding(.bottom, 130)
                }
            }
            .refreshable {
                analytics.track(.feedRefreshed)
                await store.load(location: liveLocation.currentLocation, showSpinner: false)
            }
            .toolbar(.hidden, for: .navigationBar)
            .navigationDestination(item: $selectedTable) { route in
                TableThreadView(tableId: route.tableId)
            }
            .navigationDestination(isPresented: $showChats) {
                ChatThreadsView()
            }
            .navigationDestination(isPresented: $showNotifications) {
                NotificationsView()
            }
            .navigationDestination(isPresented: $showPrivacy) {
                PrivacyView()
            }
            .navigationDestination(isPresented: $showGamification) {
                GamificationView()
            }
            .fullScreenCover(isPresented: $showCreateStory) {
                CreateStoryView(onCreated: {
                    Task { await store.load(location: liveLocation.currentLocation, showSpinner: false) }
                })
            }
            .fullScreenCover(item: $viewerConfig) { config in
                StoryViewerView(
                    storiesByUser: config.storiesByUser,
                    initialUserIndex: config.initialUserIndex,
                    onDismiss: { viewerConfig = nil }
                )
            }
            .alert("Privacy feed", isPresented: privacyAlertBinding) {
                Button("Gestisci privacy") { showPrivacy = true }
                Button("OK", role: .cancel) {}
            } message: {
                Text(privacyExplanation?.explanation ?? "")
            }
            .overlay(alignment: .bottom) {
                if let statusMessage {
                    Text(statusMessage)
                        .font(Theme.Font.caption(12, weight: .heavy))
                        .foregroundStyle(.white)
                        .padding(.horizontal, 14)
                        .padding(.vertical, 10)
                        .background(Theme.Palette.blue600, in: Capsule())
                        .padding(.bottom, 104)
                        .transition(.move(edge: .bottom).combined(with: .opacity))
                }
            }
            .task {
                analytics.track(.feedOpened)
                await refreshNotificationBadge()
                await store.load(location: liveLocation.currentLocation)
                while !Task.isCancelled {
                    try? await Task.sleep(nanoseconds: 30_000_000_000)
                    await refreshNotificationBadge()
                    await store.load(location: liveLocation.currentLocation, showSpinner: false)
                }
            }
            .onReceive(NotificationCenter.default.publisher(for: .cloudyBadgesShouldRefresh)) { _ in
                Task { await refreshNotificationBadge() }
            }
        }
    }

    private var privacyAlertBinding: Binding<Bool> {
        Binding(
            get: { privacyExplanation != nil },
            set: { if !$0 { privacyExplanation = nil } }
        )
    }

    private var topChrome: some View {
        HStack {
            Button {
                Haptics.tap()
                showNotifications = true
            } label: {
                ZStack(alignment: .topTrailing) {
                    Image(systemName: "bell.fill")
                        .font(Theme.Font.title(21, weight: .heavy))
                        .foregroundStyle(Theme.Palette.blue600)
                        .frame(width: 52, height: 52)
                        .background(Circle().fill(Theme.Palette.surface))
                        .overlay(Circle().stroke(Theme.Palette.blue100.opacity(0.75), lineWidth: 1))
                        .cardShadow()

                    if unreadNotifications > 0 {
                        Text(unreadNotifications > 9 ? "9+" : "\(unreadNotifications)")
                            .font(Theme.Font.caption(10, weight: .black))
                            .foregroundStyle(.white)
                            .padding(.horizontal, 5)
                            .frame(minWidth: 19, minHeight: 19)
                            .background(Theme.Palette.coral500, in: Capsule())
                            .offset(x: 3, y: -2)
                    }
                }
            }
            .buttonStyle(.plain)

            Spacer()

            Text("In giro")
                .font(Theme.Font.title(22, weight: .heavy))
                .foregroundStyle(Theme.Palette.ink)

            Spacer()

            Button {
                Haptics.tap()
                showChats = true
            } label: {
                Image(systemName: "paperplane.fill")
                    .font(Theme.Font.title(23, weight: .heavy))
                    .foregroundStyle(Theme.Palette.blue600)
                    .frame(width: 52, height: 52)
                    .background(Circle().fill(Theme.Palette.surface))
                    .overlay(Circle().stroke(Theme.Palette.blue100.opacity(0.75), lineWidth: 1))
                    .cardShadow()
            }
            .buttonStyle(.plain)
        }
    }

    private func refreshNotificationBadge() async {
        do {
            unreadNotifications = try await API.notificationUnreadCount().count
        } catch {
            unreadNotifications = 0
        }
    }

    private var feedHeader: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Cosa ti stai perdendo adesso")
                .font(Theme.Font.title(24, weight: .heavy))
                .foregroundStyle(Theme.Palette.ink)
                .lineLimit(2)
            Text(headerCopy)
                .font(Theme.Font.body(15, weight: .semibold))
                .foregroundStyle(Theme.Palette.inkSoft)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var headerCopy: String {
        guard let first = store.items.first else {
            return "Luoghi, amici, stories, tavoli e flare in un unico feed."
        }
        switch first.kind {
        case .hotspotVenue: return "Hot right now vicino a te."
        case .friendsActivity: return "Il tuo giro si sta muovendo."
        case .venueStoryStack: return "Nuove stories dai posti vivi."
        case .joinableTable: return "Tavoli e inviti pronti da joinare."
        case .flareChain: return "Flares in scadenza da rilanciare."
        case .arrivalForecast: return "Il gruppo sta convergendo."
        case .ghostPing: return "Segnali fuzzy, privacy al centro."
        case .emptyOnboarding: return "Accendi il tuo feed con mappa, amici e stories."
        }
    }

    private var storiesRail: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(alignment: .top, spacing: 16) {
                myStoryButton

                ForEach(Array(store.groupedStories.enumerated()), id: \.offset) { index, userStories in
                    if let first = userStories.first {
                        Button {
                            Haptics.tap()
                            viewerConfig = StoryViewerConfig(storiesByUser: store.groupedStories, initialUserIndex: index)
                        } label: {
                            VStack(spacing: 6) {
                                FeedStoryBubble(
                                    imageUrl: APIClient.shared.mediaURL(from: first.mediaUrl ?? first.avatarUrl),
                                    avatarUrl: APIClient.shared.mediaURL(from: first.avatarUrl),
                                    title: first.displayName ?? first.nickname,
                                    isLive: first.venueName != nil
                                )
                                Text(first.displayName ?? first.nickname)
                                    .font(Theme.Font.caption(12, weight: .heavy))
                                    .foregroundStyle(Theme.Palette.inkSoft)
                                    .lineLimit(1)
                                    .frame(width: 76)
                            }
                        }
                        .buttonStyle(.plain)
                    }
                }
            }
        }
    }

    private var liveMomentsSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Text("Cosa sta succedendo ora")
                    .font(Theme.Font.title(22, weight: .heavy))
                    .foregroundStyle(Theme.Palette.ink)
                Spacer()
                Text("Vedi tutto")
                    .font(Theme.Font.caption(13, weight: .heavy))
                    .foregroundStyle(Theme.Palette.blue500)
            }

            VStack(spacing: 10) {
                ForEach(store.liveMomentItems) { item in
                    LiveMomentRow(item: item, onCTA: handleCTA)
                }
            }
        }
    }

    private var myStoryButton: some View {
        VStack(spacing: 6) {
            ZStack {
                Button {
                    Haptics.tap()
                    let existingStories = self.myStories
                    if existingStories.isEmpty {
                        showCreateStory = true
                    } else {
                        viewerConfig = StoryViewerConfig(storiesByUser: [existingStories], initialUserIndex: 0)
                    }
                } label: {
                    ZStack {
                        Circle()
                            .stroke(style: StrokeStyle(lineWidth: 3, dash: myStories.isEmpty ? [8, 7] : []))
                            .foregroundStyle(myStories.isEmpty ? Theme.Palette.blue100 : Theme.Palette.blue500)
                            .frame(width: 76, height: 76)
                        StoryAvatar(
                            url: APIClient.shared.mediaURL(from: store.context?.profile?.avatarUrl),
                            size: 66,
                            hasStory: false,
                            initials: myInitials
                        )
                        .overlay(Circle().stroke(Theme.Palette.surface, lineWidth: 3))
                    }
                }
                .buttonStyle(.plain)

                Button {
                    Haptics.tap()
                    showCreateStory = true
                } label: {
                    Image(systemName: "plus")
                        .font(Theme.Font.body(16, weight: .black))
                        .foregroundStyle(.white)
                        .frame(width: 32, height: 32)
                        .background(Circle().fill(Theme.Palette.blue500))
                        .overlay(Circle().stroke(Theme.Palette.surface, lineWidth: 2))
                }
                .buttonStyle(.plain)
                .offset(x: 25, y: 25)
            }
            .frame(width: 80, height: 80)

            Text("La tua storia")
                .font(Theme.Font.caption(12, weight: .heavy))
                .foregroundStyle(Theme.Palette.inkSoft)
                .lineLimit(1)
                .frame(width: 82)
        }
    }

    private var myStories: [UserStory] {
        guard let myUserId = API.currentUserId else { return [] }
        return store.stories.filter { $0.userId == myUserId }.sorted { $0.createdAtUtc < $1.createdAtUtc }
    }

    private var myInitials: String {
        let name = store.context?.profile?.displayName ?? store.context?.profile?.nickname ?? "?"
        return String(name.prefix(1)).uppercased()
    }

    private var feedSkeleton: some View {
        VStack(spacing: 14) {
            ForEach(0..<4, id: \.self) { _ in
                RoundedRectangle(cornerRadius: 24)
                    .fill(Theme.Palette.blue50)
                    .frame(height: 132)
                    .shimmerLoading()
            }
        }
    }

    private func trackImpression(_ item: FeedItem, rank: Int) {
        guard !impressed.contains(item.tracking.impressionId) else { return }
        impressed.insert(item.tracking.impressionId)
        analytics.track(item.kind == .emptyOnboarding ? .emptyStateSeen : .feedCardImpression, item: item, rank: rank)
        store.markSeen(item)
    }

    private func handleCTA(_ item: FeedItem, _ cta: FeedCTA) {
        analytics.track(.feedCTATap, item: item, cta: cta)
        switch cta.kind {
        case .openVenue, .openMap:
            if let venueId = item.venueId {
                router.openVenue(venueId)
            }
            router.selectedTab = .map
        case .openStories:
            openStories(for: item)
        case .joinTable:
            Task { await joinTable(from: item) }
        case .openTable:
            if let tableId = tableId(from: item) {
                selectedTable = TableRoute(tableId: tableId)
            }
        case .inviteFriends:
            showChats = true
        case .relayFlare:
            Task { await relayFlare(from: item) }
        case .replyToFlare:
            Task { await replyToFlare(from: item) }
        case .createStory:
            showCreateStory = true
        case .openPrivacy:
            showPrivacy = true
        case .share:
            analytics.track(.feedShareStarted, item: item, cta: cta)
        }
    }

    private func openStories(for item: FeedItem) {
        let previews: [FeedStoryPreview]
        switch item.payload {
        case .hotspotVenue(let payload):
            previews = payload.storyPreviews
        case .venueStoryStack(let payload):
            previews = payload.previews
        default:
            previews = []
        }
        let stories = previews.map { preview in
            UserStory(
                id: preview.id,
                userId: preview.userId,
                nickname: preview.displayName,
                displayName: preview.displayName,
                avatarUrl: preview.avatarUrl,
                mediaUrl: preview.mediaUrl,
                caption: preview.caption,
                venueId: preview.venueId,
                venueName: preview.venueName,
                likeCount: 0,
                commentCount: 0,
                hasLiked: false,
                createdAtUtc: preview.createdAt,
                expiresAtUtc: Date().addingTimeInterval(3600)
            )
        }
        guard !stories.isEmpty else {
            showStatus("Nessuna story recente da aprire.")
            return
        }
        analytics.track(.feedStoryStackOpened, item: item)
        viewerConfig = StoryViewerConfig(storiesByUser: [stories], initialUserIndex: 0)
    }

    private func joinTable(from item: FeedItem) async {
        guard let tableId = tableId(from: item) else { return }
        analytics.track(.feedTableJoinStarted, item: item)
        do {
            _ = try await API.joinTable(tableId: tableId)
            Haptics.success()
            selectedTable = TableRoute(tableId: tableId)
        } catch {
            Haptics.error()
            showStatus((error as? LocalizedError)?.errorDescription ?? error.localizedDescription)
        }
    }

    private func relayFlare(from item: FeedItem) async {
        guard case .flareChain(let payload) = item.payload else { return }
        let targets = Array((store.context?.socialHub?.friends ?? []).prefix(3).map(\.userId))
        guard !targets.isEmpty else {
            showStatus("Aggiungi amici per rilanciare il flare.")
            return
        }
        analytics.track(.feedFlareRelayStarted, item: item)
        do {
            _ = try await API.relayFlare(flareId: payload.id, targetUserIds: targets)
            Haptics.success()
            showStatus("Flare rilanciato a \(targets.count) amici.")
        } catch {
            Haptics.error()
            showStatus((error as? LocalizedError)?.errorDescription ?? error.localizedDescription)
        }
    }

    private func replyToFlare(from item: FeedItem) async {
        guard case .flareChain(let payload) = item.payload else { return }
        do {
            _ = try await API.respondToFlare(flareId: payload.id, body: "Io ci sono")
            Haptics.success()
            showStatus("Risposta inviata.")
        } catch {
            Haptics.error()
            showStatus((error as? LocalizedError)?.errorDescription ?? error.localizedDescription)
        }
    }

    private func tableId(from item: FeedItem) -> UUID? {
        if case .joinableTable(let payload) = item.payload {
            return payload.id
        }
        return nil
    }

    private func showStatus(_ message: String) {
        withAnimation(.cloudySnap) {
            statusMessage = message
        }
        Task {
            try? await Task.sleep(nanoseconds: 2_500_000_000)
            await MainActor.run {
                withAnimation(.cloudySnap) {
                    if statusMessage == message {
                        statusMessage = nil
                    }
                }
            }
        }
    }
}

// MARK: - Local UI atoms

private struct FeedSurfaceBackground: View {
    var body: some View {
        ZStack {
            Theme.Palette.appBackground
            RadialGradient(colors: [Theme.Palette.blue100.opacity(0.65), .clear], center: .topLeading, startRadius: 30, endRadius: 360)
            RadialGradient(colors: [Theme.Palette.mint400.opacity(0.14), .clear], center: .bottomTrailing, startRadius: 30, endRadius: 460)
        }
    }
}

private struct FeedStoryBubble: View {
    let imageUrl: URL?
    let avatarUrl: URL?
    let title: String
    let isLive: Bool

    var body: some View {
        ZStack {
            Circle()
                .stroke(Theme.Palette.blue500, lineWidth: 4)
                .frame(width: 78, height: 78)

            ZStack {
                if let imageUrl {
                    AsyncImage(url: imageUrl) { phase in
                        switch phase {
                        case .success(let image):
                            image.resizable().scaledToFill()
                        default:
                            Theme.Palette.surfaceAlt
                        }
                    }
                } else {
                    StoryAvatar(url: avatarUrl, size: 68, hasStory: false, initials: String(title.prefix(1)).uppercased())
                }
            }
            .frame(width: 68, height: 68)
            .clipShape(Circle())
            .overlay(Circle().stroke(Theme.Palette.surface, lineWidth: 3))

            if isLive {
                Text("LIVE")
                    .font(Theme.Font.caption(10, weight: .black))
                    .foregroundStyle(.white)
                    .padding(.horizontal, 8)
                    .padding(.vertical, 4)
                    .background(Theme.Palette.coral500, in: Capsule())
                    .offset(y: 34)
            } else {
                Circle()
                    .fill(Theme.Palette.mint500)
                    .frame(width: 13, height: 13)
                    .overlay(Circle().stroke(Theme.Palette.surface, lineWidth: 2))
                    .offset(x: 27, y: 27)
            }
        }
        .frame(width: 82, height: 88)
    }
}

private struct LivePulseHeroCard: View {
    let item: FeedItem
    let payload: HotspotVenuePayload
    var onCTA: (FeedItem, FeedCTA) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 15) {
            ZStack(alignment: .bottomLeading) {
                FeedHeroMedia(urlString: payload.coverImageUrl ?? payload.storyPreviews.first?.mediaUrl)
                    .frame(height: 180)
                    .clipShape(RoundedRectangle(cornerRadius: 22, style: .continuous))

                LinearGradient(
                    colors: [.black.opacity(0), .black.opacity(0.54)],
                    startPoint: .top,
                    endPoint: .bottom
                )
                .clipShape(RoundedRectangle(cornerRadius: 22, style: .continuous))

                Label(payload.pulseCopy, systemImage: "flame.fill")
                    .font(Theme.Font.caption(12, weight: .heavy))
                    .foregroundStyle(.white)
                    .padding(.horizontal, 10)
                    .padding(.vertical, 7)
                    .background(Theme.Palette.blue500.opacity(0.92), in: Capsule())
                    .padding(14)
            }

            HStack(alignment: .top, spacing: 14) {
                VStack(alignment: .leading, spacing: 8) {
                    Label(payload.name, systemImage: "mappin.circle.fill")
                        .font(Theme.Font.title(24, weight: .heavy))
                        .foregroundStyle(Theme.Palette.ink)
                        .lineLimit(2)

                    Text(heroCopy)
                        .font(Theme.Font.body(15, weight: .semibold))
                        .foregroundStyle(Theme.Palette.inkSoft)
                        .lineSpacing(3)
                        .lineLimit(3)
                }
                Spacer()
                HeroPulseRing(value: payload.energyScore)
            }

            HStack(alignment: .center, spacing: 12) {
                FeedHeroAvatarStack(urls: payload.friendActivities.map(\.avatarUrl), count: payload.friendsHere + payload.friendsArriving)

                Text(friendCopy)
                    .font(Theme.Font.body(14, weight: .heavy))
                    .foregroundStyle(Theme.Palette.ink)
                    .lineLimit(1)

                Spacer()

                if let firstCTA = item.ctas.first {
                    Button {
                        Haptics.tap()
                        onCTA(item, firstCTA)
                    } label: {
                        Label(firstCTA.title, systemImage: "chevron.right")
                            .labelStyle(.titleAndIcon)
                            .font(Theme.Font.caption(12, weight: .black))
                            .foregroundStyle(.white)
                            .padding(.horizontal, 14)
                            .padding(.vertical, 10)
                            .background(Theme.Palette.blue500, in: Capsule())
                    }
                    .buttonStyle(.plain)
                }
            }
        }
        .padding(16)
        .background(Theme.Palette.surface, in: RoundedRectangle(cornerRadius: 28, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 28, style: .continuous).stroke(Theme.Palette.blue100.opacity(0.70), lineWidth: 1))
        .cardShadow()
    }

    private var heroCopy: String {
        if payload.growthScore >= 65 {
            return "Il gruppo sta crescendo. Il momento migliore tra 20 min."
        }
        if payload.energyScore >= 78 {
            return "Qui si sta accendendo. Entra prima del peak."
        }
        return "C'e movimento reale. Guarda chi e gia in zona."
    }

    private var friendCopy: String {
        let total = payload.friendsHere + payload.friendsArriving
        if total > 0 { return "\(total) amici qui" }
        return "\(payload.estimatedCrowd) persone ora"
    }
}

private struct LiveMomentRow: View {
    let item: FeedItem
    var onCTA: (FeedItem, FeedCTA) -> Void

    var body: some View {
        Button {
            Haptics.tap()
            if let cta = item.ctas.first {
                onCTA(item, cta)
            }
        } label: {
            HStack(spacing: 12) {
                thumbnail
                    .frame(width: 112, height: 72)
                    .clipShape(RoundedRectangle(cornerRadius: 15, style: .continuous))
                    .overlay {
                        Image(systemName: "play.fill")
                            .font(Theme.Font.body(15, weight: .black))
                            .foregroundStyle(Theme.Palette.blue600)
                            .frame(width: 34, height: 34)
                            .background(Circle().fill(.white))
                    }

                VStack(alignment: .leading, spacing: 4) {
                    Text(title)
                        .font(Theme.Font.body(16, weight: .heavy))
                        .foregroundStyle(Theme.Palette.ink)
                        .lineLimit(1)
                    Text(subtitle)
                        .font(Theme.Font.body(14, weight: .semibold))
                        .foregroundStyle(Theme.Palette.inkSoft)
                        .lineLimit(1)
                    Text(timeCopy)
                        .font(Theme.Font.caption(11, weight: .semibold))
                        .foregroundStyle(Theme.Palette.inkMuted)
                }

                Spacer()

                activityPill
            }
            .padding(10)
            .background(Theme.Palette.surface, in: RoundedRectangle(cornerRadius: 22, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 22, style: .continuous).stroke(Theme.Palette.blue100.opacity(0.52), lineWidth: 1))
        }
        .buttonStyle(.plain)
    }

    @ViewBuilder
    private var thumbnail: some View {
        let urlString = previewMedia
        if let url = APIClient.shared.mediaURL(from: urlString), url.isCloudyVideoURL {
            momentFallback
                .overlay(
                    Image(systemName: "play.fill")
                        .font(Theme.Font.title(22, weight: .bold))
                        .foregroundStyle(.white)
                        .padding(12)
                        .background(.black.opacity(0.24), in: Circle())
                )
        } else if let url = APIClient.shared.mediaURL(from: urlString) {
            AsyncImage(url: url) { phase in
                switch phase {
                case .success(let image): image.resizable().scaledToFill()
                default: momentFallback
                }
            }
        } else {
            momentFallback
        }
    }

    private var momentFallback: some View {
        LinearGradient(colors: [Theme.Palette.blue100, Theme.Palette.blue500], startPoint: .topLeading, endPoint: .bottomTrailing)
    }

    private var previewMedia: String? {
        switch item.payload {
        case .venueStoryStack(let payload): return payload.coverMediaUrl
        case .hotspotVenue(let payload): return payload.coverImageUrl ?? payload.storyPreviews.first?.mediaUrl
        default: return nil
        }
    }

    private var title: String {
        switch item.payload {
        case .friendsActivity(let payload): return payload.title
        case .venueStoryStack(let payload): return payload.friendNames.first ?? payload.venueName
        case .arrivalForecast(let payload): return "\(payload.expectedPeople) persone"
        default: return "Cloudy"
        }
    }

    private var subtitle: String {
        switch item.payload {
        case .friendsActivity(let payload): return payload.subtitle
        case .venueStoryStack(let payload): return "\(payload.storyCount) stories da \(payload.venueName)"
        case .arrivalForecast(let payload): return "stanno arrivando da \(payload.venueName)"
        default: return "sta succedendo ora"
        }
    }

    private var timeCopy: String {
        switch item.payload {
        case .friendsActivity(let payload): return relative(payload.happenedAt)
        case .venueStoryStack(let payload): return relative(payload.createdAt)
        default: return "ora"
        }
    }

    private var activityPill: some View {
        HStack(spacing: 5) {
            Image(systemName: item.kind == .arrivalForecast ? "person.3.fill" : "flame.fill")
            Text(item.kind == .arrivalForecast ? "3" : "12")
        }
        .font(Theme.Font.caption(12, weight: .black))
        .foregroundStyle(Theme.Palette.blue700)
        .padding(.horizontal, 10)
        .padding(.vertical, 8)
        .background(Theme.Palette.blue50, in: Capsule())
    }

    private func relative(_ date: Date) -> String {
        let formatter = RelativeDateTimeFormatter()
        formatter.locale = Locale(identifier: "it_IT")
        formatter.unitsStyle = .short
        return formatter.localizedString(for: date, relativeTo: Date())
    }
}

private struct FeedGamificationCard: View {
    let summary: GamificationSummary
    var onOpen: () -> Void

    var body: some View {
        Button(action: onOpen) {
            HStack(spacing: 14) {
                ZStack {
                    Circle().fill(Theme.Palette.blue700.opacity(0.20))
                    Image(systemName: "bolt.fill")
                        .font(Theme.Font.title(22, weight: .black))
                        .foregroundStyle(Theme.Palette.blue500)
                }
                .frame(width: 58, height: 58)

                VStack(alignment: .leading, spacing: 4) {
                    Text("Missioni attive")
                        .font(Theme.Font.title(18, weight: .heavy))
                        .foregroundStyle(Theme.Palette.ink)
                    Text(missionCopy)
                        .font(Theme.Font.body(14, weight: .semibold))
                        .foregroundStyle(Theme.Palette.inkSoft)
                        .lineLimit(2)
                }

                Spacer()

                VStack(alignment: .trailing, spacing: 2) {
                    Text("\(summary.weeklyPoints)")
                        .font(Theme.Font.heroNumber(20).monospacedDigit())
                        .foregroundStyle(Theme.Palette.ink)
                    Text("pt week")
                        .font(Theme.Font.caption(10, weight: .black))
                        .foregroundStyle(Theme.Palette.inkMuted)
                }

                Image(systemName: "chevron.right")
                    .font(Theme.Font.body(14, weight: .black))
                    .foregroundStyle(Theme.Palette.inkMuted)
            }
            .padding(16)
            .background(
                LinearGradient(colors: [Theme.Palette.blue50, Theme.Palette.surface], startPoint: .topLeading, endPoint: .bottomTrailing),
                in: RoundedRectangle(cornerRadius: 24, style: .continuous)
            )
            .overlay(RoundedRectangle(cornerRadius: 24, style: .continuous).stroke(Theme.Palette.blue100.opacity(0.72), lineWidth: 1))
        }
        .buttonStyle(.plain)
        .cardShadow()
    }

    private var missionCopy: String {
        if let next = summary.weeklyMissions.first(where: { !$0.isCompleted }) {
            return "\(next.title): \(next.progress)/\(next.target) · +\(next.rewardPoints) pt"
        }
        return "Tutte completate. Sei livello \(summary.level)."
    }
}

private struct FeedHeroMedia: View {
    let urlString: String?

    var body: some View {
        ZStack {
            if let url = APIClient.shared.mediaURL(from: urlString), url.isCloudyVideoURL {
                fallback
                    .overlay(
                        Image(systemName: "play.fill")
                            .font(Theme.Font.title(24, weight: .bold))
                            .foregroundStyle(.white)
                            .padding(14)
                            .background(.black.opacity(0.24), in: Circle())
                    )
            } else if let url = APIClient.shared.mediaURL(from: urlString) {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .success(let image): image.resizable().scaledToFill()
                    default: fallback
                    }
                }
            } else {
                fallback
            }
        }
    }

    private var fallback: some View {
        LinearGradient(
            colors: [Theme.Palette.blue50, Theme.Palette.blue100, Theme.Palette.blue500],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        )
        .overlay {
            Image(systemName: "sparkles")
                .font(Theme.Font.display(54, weight: .black))
                .foregroundStyle(.white.opacity(0.34))
        }
    }
}

private struct HeroPulseRing: View {
    let value: Int

    var body: some View {
        ZStack {
            Circle().stroke(Theme.Palette.blue50, lineWidth: 7)
            Circle()
                .trim(from: 0, to: CGFloat(min(100, value)) / 100)
                .stroke(Theme.Palette.blue500, style: StrokeStyle(lineWidth: 7, lineCap: .round))
                .rotationEffect(.degrees(-90))
            VStack(spacing: 0) {
                Text("\(value)")
                    .font(Theme.Font.heroNumber(24).monospacedDigit())
                    .foregroundStyle(Theme.Palette.ink)
                Text("pulse")
                    .font(Theme.Font.caption(10, weight: .heavy))
                    .foregroundStyle(Theme.Palette.inkMuted)
            }
        }
        .frame(width: 82, height: 82)
    }
}

private struct FeedHeroAvatarStack: View {
    let urls: [String?]
    let count: Int

    var body: some View {
        HStack(spacing: -9) {
            ForEach(Array(urls.prefix(2).enumerated()), id: \.offset) { index, raw in
                StoryAvatar(
                    url: APIClient.shared.mediaURL(from: raw),
                    size: 36,
                    hasStory: false,
                    initials: "\(index + 1)"
                )
                .overlay(Circle().stroke(Theme.Palette.surface, lineWidth: 2))
            }
            if count > max(urls.prefix(2).count, 0) {
                Text("+\(max(0, count - min(urls.count, 2)))")
                    .font(Theme.Font.caption(11, weight: .black))
                    .foregroundStyle(Theme.Palette.blue700)
                    .frame(width: 36, height: 36)
                    .background(Circle().fill(Theme.Palette.blue50))
                    .overlay(Circle().stroke(Theme.Palette.surface, lineWidth: 2))
            }
        }
    }
}
