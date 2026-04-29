//
//  FeedView.swift
//  Cloudy
//
//  Feed intelligente: non una timeline, ma un decision engine per capire dove
//  sta succedendo qualcosa e cosa conviene fare adesso.
//

import SwiftUI

@MainActor
@Observable
final class FeedStore {
    var stories: [UserStory] = []
    var hub: SocialHub?
    var profile: EditableUserProfile?
    var isLoading: Bool = false
    var error: String?

    private let ranking = FeedRankingService()

    func load(showSpinner: Bool = true) async {
        if showSpinner { isLoading = true }
        error = nil
        defer { if showSpinner { isLoading = false } }
        do {
            async let s = API.stories()
            async let h = API.socialHub()
            async let p = API.myEditableProfile()
            stories = try await s
            hub = try await h
            profile = try? await p
        } catch {
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    var feedItems: [FeedItem] {
        ranking.rankedItems(hub: hub, stories: stories)
    }

    var groupedStories: [[UserStory]] {
        Dictionary(grouping: stories) { $0.userId }
            .values
            .sorted {
                ($0.first?.createdAtUtc ?? .distantPast) > ($1.first?.createdAtUtc ?? .distantPast)
            }
    }
}

struct StoryViewerConfig: Identifiable {
    let id = UUID()
    let storiesByUser: [[UserStory]]
    let initialUserIndex: Int
}

struct FeedView: View {
    @Environment(AppRouter.self) private var router

    @State private var store = FeedStore()
    @State private var showCreateStory = false
    @State private var viewerConfig: StoryViewerConfig?
    @State private var selectedStoryPreviews: [FeedStoryPreview] = []
    @State private var selectedChat: SocialConnection?

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(spacing: Theme.Spacing.xl) {
                    heroHeader
                        .padding(.horizontal, Theme.Spacing.lg)

                    storiesRow
                        .padding(.horizontal, Theme.Spacing.lg)

                    feedContent
                        .padding(.horizontal, Theme.Spacing.lg)
                }
                .padding(.top, Theme.Spacing.md)
                .padding(.bottom, 120)
            }
            .background(Theme.Palette.appBackground.ignoresSafeArea())
            .refreshable { await store.load() }
            .navigationTitle("In giro")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    HStack(spacing: 7) {
                        Image(systemName: "sparkles")
                            .foregroundStyle(Theme.Palette.blue500)
                        Text("Radar")
                            .font(Theme.Font.title(18, weight: .heavy))
                    }
                }
                ToolbarItem(placement: .topBarTrailing) {
                    NavigationLink {
                        ChatThreadsView()
                    } label: {
                        Image(systemName: "paperplane")
                            .foregroundStyle(Theme.Palette.ink)
                    }
                }
            }
            .fullScreenCover(isPresented: $showCreateStory) {
                CreateStoryView(onCreated: {
                    Task { await store.load() }
                })
            }
            .fullScreenCover(item: $viewerConfig) { config in
                StoryViewerView(
                    storiesByUser: config.storiesByUser,
                    initialUserIndex: config.initialUserIndex,
                    onDismiss: { viewerConfig = nil }
                )
            }
            .navigationDestination(item: $selectedChat) { friend in
                ChatRoomView(otherUserId: friend.userId, peerName: friend.displayName ?? friend.nickname)
            }
            .task {
                await store.load()
                while !Task.isCancelled {
                    try? await Task.sleep(nanoseconds: 18_000_000_000)
                    await store.load(showSpinner: false)
                }
            }
        }
    }

    private var heroHeader: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(alignment: .firstTextBaseline) {
                VStack(alignment: .leading, spacing: 6) {
                    Text("Dove si muove la serata")
                        .font(Theme.Font.display(30))
                        .foregroundStyle(Theme.Palette.ink)
                    Text("Luoghi caldi, amici in arrivo e momenti recenti. Tutto aggregato, niente posizione precisa.")
                        .font(Theme.Font.body(14, weight: .semibold))
                        .foregroundStyle(Theme.Palette.inkSoft)
                        .lineSpacing(3)
                }
                Spacer(minLength: 10)
                EnergyOrb(value: topEnergy)
            }

            HStack(spacing: 8) {
                FeedInsightPill(icon: "person.2.fill", text: "\(friendsActiveCount) amici attivi")
                FeedInsightPill(icon: "photo.stack.fill", text: "\(store.stories.count) storie")
            }
        }
        .padding(Theme.Spacing.lg)
        .background(
            RoundedRectangle(cornerRadius: 28, style: .continuous)
                .fill(Theme.Palette.surface)
        )
        .overlay(
            RoundedRectangle(cornerRadius: 28, style: .continuous)
                .stroke(Theme.Palette.blue100.opacity(0.7), lineWidth: 1)
        )
        .cardShadow()
    }

    private var topEnergy: Int {
        store.feedItems.compactMap {
            if case .venue(let card) = $0 { return card.energyScore }
            return nil
        }.max() ?? 42
    }

    private var friendsActiveCount: Int {
        store.hub?.friends.filter { $0.currentVenueName != nil || $0.presenceState != "offline" }.count ?? 0
    }

    // MARK: - Stories row

    private var storiesRow: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(alignment: .top, spacing: 12) {
                myStoryButton

                ForEach(Array(store.groupedStories.enumerated()), id: \.offset) { index, userStories in
                    if let first = userStories.first {
                        Button {
                            Haptics.tap()
                            viewerConfig = StoryViewerConfig(
                                storiesByUser: store.groupedStories,
                                initialUserIndex: index
                            )
                        } label: {
                            VStack(spacing: 6) {
                                StoryAvatar(
                                    url: APIClient.shared.mediaURL(from: first.avatarUrl),
                                    size: 64,
                                    hasStory: true,
                                    initials: String((first.displayName ?? first.nickname).prefix(1)).uppercased()
                                )
                                Text(first.displayName ?? first.nickname)
                                    .font(Theme.Font.caption(11))
                                    .foregroundStyle(Theme.Palette.inkSoft)
                                    .lineLimit(1)
                                    .frame(width: 72)
                            }
                        }
                        .buttonStyle(.plain)
                    }
                }

                if store.stories.isEmpty && !store.isLoading {
                    Text("Nessuna storia oggi")
                        .font(Theme.Font.body(13))
                        .foregroundStyle(Theme.Palette.inkMuted)
                        .frame(height: 80)
                }
            }
        }
    }

    private var myStoryButton: some View {
        Button {
            Haptics.tap()
            showCreateStory = true
        } label: {
            VStack(spacing: 6) {
                ZStack(alignment: .bottomTrailing) {
                    StoryAvatar(
                        url: APIClient.shared.mediaURL(from: store.profile?.avatarUrl),
                        size: 64,
                        hasStory: myStories.isEmpty == false,
                        initials: myInitials
                    )
                    Circle()
                        .fill(Theme.Palette.blue500)
                        .frame(width: 22, height: 22)
                        .overlay(
                            Image(systemName: "plus")
                                .font(.system(size: 12, weight: .heavy))
                                .foregroundStyle(.white)
                        )
                        .offset(x: 2, y: 2)
                }
                .frame(width: 72, height: 72)
                Text("La tua storia")
                    .font(Theme.Font.caption(11))
                    .foregroundStyle(Theme.Palette.inkSoft)
                    .lineLimit(1)
                    .frame(width: 72)
            }
        }
        .buttonStyle(.plain)
    }

    private var myStories: [UserStory] {
        guard let myUserId = API.currentUserId else { return [] }
        return store.stories.filter { $0.userId == myUserId }.sorted { $0.createdAtUtc < $1.createdAtUtc }
    }

    private var myInitials: String {
        let name = store.profile?.displayName ?? store.profile?.nickname ?? "?"
        return String(name.prefix(1)).uppercased()
    }

    // MARK: - Feed

    private var feedContent: some View {
        VStack(spacing: Theme.Spacing.lg) {
            if store.isLoading && store.stories.isEmpty && store.hub == nil {
                feedSkeleton
            } else {
                ForEach(store.feedItems) { item in
                    switch item {
                    case .venue(let card):
                        VenueDecisionCard(
                            card: card,
                            onGo: { router.selectedTab = .map },
                            onOpenTable: { router.selectedTab = .tables },
                            onOpenStories: { openStories(card.storyPreviews) }
                        )
                    case .social(let card):
                        SocialMomentFeedCard(
                            card: card,
                            onAction: { router.selectedTab = .map }
                        )
                    }
                }

                if store.feedItems.isEmpty {
                    CloudyEmptyState(
                        icon: "sunrise.fill",
                        title: "La città si sta ancora svegliando",
                        message: "Segui amici o esplora la mappa per accendere il tuo feed."
                    )
                    .padding(.top, 28)
                }
            }

            if let error = store.error {
                Text(error)
                    .font(Theme.Font.caption(12, weight: .semibold))
                    .foregroundStyle(Theme.Palette.coral500)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
        }
    }

    private var feedSkeleton: some View {
        VStack(spacing: 14) {
            ForEach(0..<3, id: \.self) { _ in
                RoundedRectangle(cornerRadius: 24)
                    .fill(Theme.Palette.blue50)
                    .frame(height: 230)
                    .shimmerLoading()
            }
        }
    }

    private func openStories(_ previews: [FeedStoryPreview]) {
        let stories = previews.map { preview in
            UserStory(
                id: preview.id,
                userId: preview.userId,
                nickname: preview.displayName,
                displayName: preview.displayName,
                avatarUrl: preview.avatarUrl,
                mediaUrl: preview.mediaUrl,
                caption: preview.caption,
                venueId: nil,
                venueName: nil,
                likeCount: 0,
                commentCount: 0,
                hasLiked: false,
                createdAtUtc: preview.createdAt,
                expiresAtUtc: Date().addingTimeInterval(3600)
            )
        }
        guard !stories.isEmpty else { return }
        viewerConfig = StoryViewerConfig(storiesByUser: [stories], initialUserIndex: 0)
    }
}

// MARK: - Venue card

private struct VenueDecisionCard: View {
    let card: VenueFeedCard
    var onGo: () -> Void
    var onOpenTable: () -> Void
    var onOpenStories: () -> Void

    @State private var pressedCTA: String?

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            HStack(alignment: .top, spacing: 14) {
                EnergyBadge(value: card.energyScore, state: card.liveState)
                VStack(alignment: .leading, spacing: 5) {
                    Text(card.name)
                        .font(Theme.Font.display(26))
                        .foregroundStyle(Theme.Palette.ink)
                        .lineLimit(2)
                    Text(card.primaryCopy)
                        .font(Theme.Font.body(14, weight: .semibold))
                        .foregroundStyle(Theme.Palette.inkSoft)
                        .lineLimit(2)
                }
                Spacer()
                VStack(alignment: .trailing, spacing: 5) {
                    Text(card.liveState.copy)
                        .font(Theme.Font.caption(12, weight: .heavy))
                        .foregroundStyle(stateColor)
                        .padding(.horizontal, 10)
                        .padding(.vertical, 6)
                        .background(stateColor.opacity(0.12), in: Capsule())
                    if let category = card.category {
                        Text(category.capitalized)
                            .font(Theme.Font.caption(12, weight: .semibold))
                            .foregroundStyle(Theme.Palette.inkMuted)
                    }
                }
            }

            HStack(alignment: .bottom, spacing: 16) {
                TrendSparkline(values: card.trend, color: stateColor)
                    .frame(height: 58)
                    .frame(maxWidth: .infinity)
                    .padding(12)
                    .background(Theme.Palette.surfaceAlt, in: RoundedRectangle(cornerRadius: 18, style: .continuous))

                VStack(alignment: .leading, spacing: 9) {
                    metric("\(card.friendsHere)", "qui ora", "person.crop.circle.fill")
                    metric("\(card.friendsArriving)", "in arrivo", "arrow.triangle.turn.up.right.circle.fill")
                }
                .frame(width: 112)
            }

            if !card.friendActivities.isEmpty {
                VStack(alignment: .leading, spacing: 8) {
                    ForEach(card.friendActivities.prefix(3)) { activity in
                        HStack(spacing: 9) {
                            StoryAvatar(
                                url: APIClient.shared.mediaURL(from: activity.avatarUrl),
                                size: 28,
                                hasStory: false,
                                initials: String(activity.displayName.prefix(1)).uppercased()
                            )
                            Text(activity.privacyLevel == .aggregated ? "Alcuni amici si stanno muovendo qui" : activity.copy)
                                .font(Theme.Font.caption(12, weight: .semibold))
                                .foregroundStyle(Theme.Palette.inkSoft)
                                .lineLimit(1)
                        }
                    }
                }
            }

            if !card.storyPreviews.isEmpty {
                Button(action: onOpenStories) {
                    HStack(spacing: -8) {
                        ForEach(card.storyPreviews.prefix(4)) { story in
                            StoryPreviewThumb(story: story)
                        }
                        Text("\(card.storyPreviews.count) foto recenti")
                            .font(Theme.Font.caption(12, weight: .heavy))
                            .foregroundStyle(Theme.Palette.blue700)
                            .padding(.leading, 16)
                        Spacer()
                        Image(systemName: "chevron.right")
                            .font(.system(size: 12, weight: .heavy))
                            .foregroundStyle(Theme.Palette.inkMuted)
                    }
                }
                .buttonStyle(.plain)
            }

            HStack(spacing: 10) {
                Button(action: onGo) {
                    Label("Vai", systemImage: "location.fill")
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(.honeyCompact)

                ShareLink(item: inviteText) {
                    Label("Invita", systemImage: "person.badge.plus")
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(.ghost)

                Button(action: onOpenTable) {
                    Label(card.friendsHere > 0 ? "Join" : "Tavolo", systemImage: "person.3.fill")
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(.ghost)
            }
        }
        .padding(18)
        .background(
            RoundedRectangle(cornerRadius: 28, style: .continuous)
                .fill(Theme.Palette.surface)
        )
        .overlay(
            RoundedRectangle(cornerRadius: 28, style: .continuous)
                .stroke(Theme.Palette.blue100.opacity(0.65), lineWidth: 1)
        )
        .cardShadow()
    }

    private var stateColor: Color {
        switch card.liveState {
        case .almostFull: return Theme.Palette.coral500
        case .hotNow: return Theme.Palette.densityHigh
        case .growing: return Theme.Palette.blue500
        case .wakingUp: return Theme.Palette.mint500
        }
    }

    private var inviteText: String {
        "Ci vediamo da \(card.name) su Cloudy. \(card.primaryCopy)."
    }

    private func metric(_ value: String, _ label: String, _ icon: String) -> some View {
        HStack(spacing: 7) {
            Image(systemName: icon)
                .font(.system(size: 13, weight: .heavy))
                .foregroundStyle(Theme.Palette.blue500)
            VStack(alignment: .leading, spacing: 1) {
                Text(value).font(Theme.Font.heroNumber(18).monospacedDigit())
                Text(label).font(Theme.Font.caption(10, weight: .semibold)).foregroundStyle(Theme.Palette.inkMuted)
            }
        }
    }
}

private struct SocialMomentFeedCard: View {
    let card: SocialMomentCard
    var onAction: () -> Void

    var body: some View {
        HStack(spacing: 14) {
            ZStack {
                Circle()
                    .fill(Theme.Palette.blue50)
                    .frame(width: 54, height: 54)
                Image(systemName: "sparkles")
                    .font(.system(size: 22, weight: .heavy))
                    .foregroundStyle(Theme.Palette.blue500)
            }

            VStack(alignment: .leading, spacing: 4) {
                Text(card.title)
                    .font(Theme.Font.title(17, weight: .heavy))
                    .foregroundStyle(Theme.Palette.ink)
                Text(card.subtitle)
                    .font(Theme.Font.body(13, weight: .semibold))
                    .foregroundStyle(Theme.Palette.inkSoft)
                    .lineLimit(2)
            }

            Spacer()

            Button(card.actionTitle, action: onAction)
                .font(Theme.Font.caption(12, weight: .heavy))
                .buttonStyle(.honeyCompact)
        }
        .padding(16)
        .background(Theme.Palette.surface, in: RoundedRectangle(cornerRadius: 24, style: .continuous))
        .cardShadow()
    }
}

private struct EnergyBadge: View {
    let value: Int
    let state: VenueLiveState

    var body: some View {
        ZStack {
            Circle()
                .fill(Theme.Palette.blue50)
                .frame(width: 68, height: 68)
            Circle()
                .trim(from: 0, to: CGFloat(value) / 100)
                .stroke(Theme.Gradients.densityHeat, style: StrokeStyle(lineWidth: 6, lineCap: .round))
                .frame(width: 68, height: 68)
                .rotationEffect(.degrees(-90))
                .animation(.cloudySnap, value: value)
            VStack(spacing: 0) {
                Text("\(value)")
                    .font(Theme.Font.heroNumber(20).monospacedDigit())
                    .contentTransition(.numericText())
                Text("pulse")
                    .font(Theme.Font.caption(9, weight: .heavy))
                    .foregroundStyle(Theme.Palette.inkMuted)
            }
        }
        .accessibilityLabel("Energia \(value)")
    }
}

private struct EnergyOrb: View {
    let value: Int

    var body: some View {
        VStack(spacing: 3) {
            Text("\(value)")
                .font(Theme.Font.heroNumber(22).monospacedDigit())
                .foregroundStyle(.white)
            Text("live")
                .font(Theme.Font.caption(10, weight: .heavy))
                .foregroundStyle(.white.opacity(0.82))
        }
        .frame(width: 68, height: 68)
        .background(Circle().fill(Theme.Palette.blue500))
        .shadow(color: Theme.Palette.blue500.opacity(0.24), radius: 18, x: 0, y: 9)
    }
}

private struct FeedInsightPill: View {
    let icon: String
    let text: String

    var body: some View {
        Label(text, systemImage: icon)
            .font(Theme.Font.caption(12, weight: .heavy))
            .foregroundStyle(Theme.Palette.blue700)
            .padding(.horizontal, 10)
            .padding(.vertical, 7)
            .background(Theme.Palette.blue50, in: Capsule())
    }
}

private struct TrendSparkline: View {
    let values: [Int]
    let color: Color

    var body: some View {
        GeometryReader { proxy in
            let points = pathPoints(size: proxy.size)
            Path { path in
                guard let first = points.first else { return }
                path.move(to: first)
                for point in points.dropFirst() {
                    path.addLine(to: point)
                }
            }
            .stroke(color, style: StrokeStyle(lineWidth: 3, lineCap: .round, lineJoin: .round))

            ForEach(Array(points.enumerated()), id: \.offset) { _, point in
                Circle()
                    .fill(color)
                    .frame(width: 5, height: 5)
                    .position(point)
            }
        }
        .accessibilityHidden(true)
    }

    private func pathPoints(size: CGSize) -> [CGPoint] {
        guard let minValue = values.min(), let maxValue = values.max(), values.count > 1 else { return [] }
        let span = Swift.max(maxValue - minValue, 1)
        return values.enumerated().map { index, value in
            let x = CGFloat(index) / CGFloat(values.count - 1) * size.width
            let normalized = CGFloat(value - minValue) / CGFloat(span)
            let y = size.height - normalized * size.height
            return CGPoint(x: x, y: y)
        }
    }
}

private struct StoryPreviewThumb: View {
    let story: FeedStoryPreview

    var body: some View {
        ZStack {
            if let url = APIClient.shared.mediaURL(from: story.mediaUrl) {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .success(let image):
                        image.resizable().scaledToFill()
                    default:
                        Theme.Palette.blue50
                    }
                }
            } else {
                Theme.Palette.blue50
            }
        }
        .frame(width: 42, height: 42)
        .clipShape(Circle())
        .overlay(Circle().stroke(Theme.Palette.surface, lineWidth: 3))
    }
}

#Preview("Feed venue card") {
    ScrollView {
        VStack {
            VenueDecisionCard(
                card: FeedRankingService.mockVenueCards[0],
                onGo: {},
                onOpenTable: {},
                onOpenStories: {}
            )
            SocialMomentFeedCard(
                card: SocialMomentCard(
                    id: "p",
                    title: "Il tuo giro si sta concentrando",
                    subtitle: "3 amici stanno puntando questo posto.",
                    venueName: "Fellini",
                    avatarUrls: [],
                    actionTitle: "Vai",
                    privacyLevel: .mock
                ),
                onAction: {}
            )
        }
        .padding()
    }
    .background(Theme.Palette.appBackground)
}
