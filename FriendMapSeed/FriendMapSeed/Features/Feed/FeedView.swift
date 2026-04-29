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
            ZStack {
                FeedNightBackground()
                    .ignoresSafeArea()

                ScrollView {
                    VStack(spacing: 22) {
                        topChrome
                            .padding(.horizontal, 22)

                        if let primaryVenue {
                            LiveVenueHeroCard(
                                card: primaryVenue,
                                onOpenVenue: { router.selectedTab = .map }
                            )
                            .padding(.horizontal, 18)
                        }

                        storiesRow
                            .padding(.horizontal, 18)

                        liveMomentsSection
                            .padding(.horizontal, 18)

                        urgencyBanner
                            .padding(.horizontal, 18)
                    }
                    .padding(.top, 18)
                    .padding(.bottom, 128)
                }
            }
            .refreshable { await store.load() }
            .navigationTitle("In giro")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar(.hidden, for: .navigationBar)
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

    private var topChrome: some View {
        HStack {
            Button {
                Haptics.tap()
            } label: {
                Image(systemName: "sparkles")
                    .font(.system(size: 24, weight: .heavy))
                    .foregroundStyle(Theme.Palette.blue400)
                    .frame(width: 58, height: 58)
                    .background(Circle().fill(.white.opacity(0.06)))
                    .overlay(Circle().stroke(.white.opacity(0.10), lineWidth: 1))
            }
            .buttonStyle(.plain)

            Spacer()

            Text("In giro")
                .font(Theme.Font.display(25))
                .foregroundStyle(.white)

            Spacer()

            NavigationLink {
                ChatThreadsView()
            } label: {
                Image(systemName: "paperplane.fill")
                    .font(.system(size: 23, weight: .heavy))
                    .foregroundStyle(.white)
                    .frame(width: 58, height: 58)
                    .background(Circle().fill(.white.opacity(0.06)))
                    .overlay(alignment: .topTrailing) {
                        Circle()
                            .fill(Color(hex: 0xFF2F7D))
                            .frame(width: 12, height: 12)
                            .offset(x: -8, y: 8)
                    }
                    .overlay(Circle().stroke(.white.opacity(0.10), lineWidth: 1))
            }
            .buttonStyle(.plain)
        }
    }

    private var primaryVenue: VenueFeedCard? {
        venueCards.first
    }

    private var venueCards: [VenueFeedCard] {
        store.feedItems.compactMap {
            if case .venue(let card) = $0 { return card }
            return nil
        }
    }

    private var topEnergy: Int {
        venueCards.map(\.energyScore).max() ?? 42
    }

    private var friendsActiveCount: Int {
        store.hub?.friends.filter { $0.currentVenueName != nil || $0.presenceState != "offline" }.count ?? 0
    }

    // MARK: - Stories row

    private var storiesRow: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(alignment: .top, spacing: 16) {
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
                                FeedStoryBubble(
                                    imageUrl: APIClient.shared.mediaURL(from: first.mediaUrl ?? first.avatarUrl),
                                    avatarUrl: APIClient.shared.mediaURL(from: first.avatarUrl),
                                    title: first.displayName ?? first.nickname,
                                    isLive: first.venueName != nil
                                )
                                Text(first.displayName ?? first.nickname)
                                    .font(Theme.Font.caption(12, weight: .heavy))
                                    .foregroundStyle(.white.opacity(0.86))
                                    .lineLimit(1)
                                    .frame(width: 76)
                            }
                        }
                        .buttonStyle(.plain)
                    }
                }

                if store.stories.isEmpty && !store.isLoading {
                    Text("Nessuna storia oggi")
                        .font(Theme.Font.body(13))
                        .foregroundStyle(.white.opacity(0.55))
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
                    Circle()
                        .stroke(style: StrokeStyle(lineWidth: 3, dash: [8, 7]))
                        .foregroundStyle(.white.opacity(0.22))
                        .frame(width: 76, height: 76)
                    Circle()
                        .fill(Theme.Palette.blue500)
                        .frame(width: 32, height: 32)
                        .overlay(
                            Image(systemName: "plus")
                                .font(.system(size: 17, weight: .heavy))
                                .foregroundStyle(.white)
                        )
                        .offset(x: 0, y: 0)
                }
                .frame(width: 80, height: 80)
                Text("La tua storia")
                    .font(Theme.Font.caption(12, weight: .heavy))
                    .foregroundStyle(.white.opacity(0.86))
                    .lineLimit(1)
                    .frame(width: 82)
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

    private var liveMomentsSection: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack {
                Text("Cosa sta succedendo ora")
                    .font(Theme.Font.display(25))
                    .foregroundStyle(.white)
                Spacer()
                Button {
                    router.selectedTab = .map
                } label: {
                    Text("Vedi tutto")
                        .font(Theme.Font.body(14, weight: .heavy))
                        .foregroundStyle(Theme.Palette.blue400)
                }
                .buttonStyle(.plain)
            }

            if store.isLoading && store.stories.isEmpty && store.hub == nil {
                feedSkeleton
            } else {
                let moments = liveMomentRows
                if moments.isEmpty {
                    FeedEmptyMomentCard()
                } else {
                    ForEach(moments.prefix(6)) { moment in
                        LiveMomentRow(moment: moment) {
                            if let story = moment.story {
                                openStories([story])
                            } else {
                                router.selectedTab = .map
                            }
                        }
                    }
                }
            }
        }
    }

    private var feedSkeleton: some View {
        VStack(spacing: 14) {
            ForEach(0..<3, id: \.self) { _ in
                RoundedRectangle(cornerRadius: 24)
                    .fill(.white.opacity(0.08))
                    .frame(height: 110)
                    .shimmerLoading()
            }
        }
    }

    private var urgencyBanner: some View {
        Button {
            router.selectedTab = .map
            Haptics.tap()
        } label: {
            HStack(spacing: 14) {
                Image(systemName: "bolt.fill")
                    .font(.system(size: 27, weight: .black))
                    .foregroundStyle(Color(hex: 0xFF4DFF))
                    .frame(width: 58, height: 58)
                    .background(Circle().fill(.white.opacity(0.10)))

                VStack(alignment: .leading, spacing: 3) {
                    Text("Il gruppo e attivo!")
                        .font(Theme.Font.title(18, weight: .heavy))
                        .foregroundStyle(.white)
                    Text("Piu persone, piu energia.")
                        .font(Theme.Font.body(14, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.78))
                }
                Spacer()
                AvatarStack(urls: venueCards.first?.friendActivities.map(\.avatarUrl) ?? [], maxVisible: 3)
                Image(systemName: "chevron.right")
                    .font(.system(size: 16, weight: .heavy))
                    .foregroundStyle(.white.opacity(0.72))
            }
            .padding(16)
            .background(
                LinearGradient(
                    colors: [Color(hex: 0x1824D8), Color(hex: 0xC0267A)],
                    startPoint: .leading,
                    endPoint: .trailing
                ),
                in: RoundedRectangle(cornerRadius: 24, style: .continuous)
            )
        }
        .buttonStyle(.plain)
    }

    private var liveMomentRows: [FeedLiveMoment] {
        var rows: [FeedLiveMoment] = []
        for venue in venueCards {
            for activity in venue.friendActivities {
                rows.append(FeedLiveMoment(
                    id: activity.id,
                    title: activity.displayName,
                    subtitle: momentSubtitle(activity),
                    time: relativeTime(activity.createdAt),
                    heat: heatFor(activity, venue: venue),
                    icon: iconFor(activity),
                    imageUrl: firstStoryUrl(for: venue),
                    story: nil
                ))
            }
            for story in venue.storyPreviews {
                rows.append(FeedLiveMoment(
                    id: "story-row-\(story.id.uuidString)",
                    title: story.displayName,
                    subtitle: story.caption ?? "ha postato una foto qui",
                    time: relativeTime(story.createdAt),
                    heat: max(3, min(14, venue.energyScore / 8)),
                    icon: "play.fill",
                    imageUrl: APIClient.shared.mediaURL(from: story.mediaUrl),
                    story: story
                ))
            }
        }

        if rows.isEmpty {
            rows = FeedLiveMoment.mock
        }
        return rows
    }

    private func momentSubtitle(_ activity: FriendActivity) -> String {
        switch activity.kind {
        case .arrived: return "appena arrivato"
        case .going: return "sta decidendo se venire"
        case .postedStory: return "brindisi in terrazza"
        case .groupConverging(let count): return "\(count) persone sono arrivate"
        }
    }

    private func heatFor(_ activity: FriendActivity, venue: VenueFeedCard) -> Int {
        switch activity.kind {
        case .groupConverging(let count): return count
        case .postedStory: return max(6, venue.energyScore / 9)
        case .going: return max(4, venue.friendsArriving)
        case .arrived: return max(6, venue.energyScore / 7)
        }
    }

    private func iconFor(_ activity: FriendActivity) -> String {
        switch activity.kind {
        case .arrived, .postedStory: return "play.fill"
        case .going: return "arrow.up.right"
        case .groupConverging: return "person.3.fill"
        }
    }

    private func firstStoryUrl(for venue: VenueFeedCard) -> URL? {
        venue.storyPreviews.compactMap { APIClient.shared.mediaURL(from: $0.mediaUrl) }.first
    }

    private func relativeTime(_ date: Date) -> String {
        let seconds = max(60, Int(Date().timeIntervalSince(date)))
        let minutes = max(1, seconds / 60)
        return "\(minutes) min fa"
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

// MARK: - Live moments UI

private struct FeedNightBackground: View {
    var body: some View {
        ZStack {
            Color.black
            RadialGradient(
                colors: [Theme.Palette.blue500.opacity(0.28), .clear],
                center: .topLeading,
                startRadius: 20,
                endRadius: 380
            )
            RadialGradient(
                colors: [Color(hex: 0xC026D3).opacity(0.20), .clear],
                center: .bottomTrailing,
                startRadius: 20,
                endRadius: 460
            )
        }
    }
}

private struct LiveVenueHeroCard: View {
    let card: VenueFeedCard
    var onOpenVenue: () -> Void

    var body: some View {
        ZStack {
            HeroVenueMedia(url: heroUrl)

            LinearGradient(
                colors: [.black.opacity(0.18), .black.opacity(0.78)],
                startPoint: .top,
                endPoint: .bottom
            )

            VStack(alignment: .leading, spacing: 18) {
                HStack(alignment: .top) {
                    VStack(alignment: .leading, spacing: 11) {
                        Label(card.name, systemImage: "mappin.circle.fill")
                            .font(Theme.Font.display(30))
                            .foregroundStyle(.white)
                            .lineLimit(2)

                        Label(urgencyLabel, systemImage: "flame.fill")
                            .font(Theme.Font.title(18, weight: .heavy))
                            .foregroundStyle(Color(hex: 0xFF3D82))

                        Text(tensionCopy)
                            .font(Theme.Font.body(20, weight: .semibold))
                            .foregroundStyle(.white)
                            .lineSpacing(5)
                            .fixedSize(horizontal: false, vertical: true)
                    }

                    Spacer()

                    PulseRing(value: card.energyScore)
                }

                Spacer()

                HStack(spacing: 12) {
                    AvatarStack(urls: card.friendActivities.map(\.avatarUrl), maxVisible: 3)
                    Text(friendCopy)
                        .font(Theme.Font.body(17, weight: .heavy))
                        .foregroundStyle(.white)
                    Circle()
                        .fill(Theme.Palette.mint500)
                        .frame(width: 9, height: 9)

                    Spacer()

                    Button(action: onOpenVenue) {
                        HStack(spacing: 10) {
                            Text("Apri luogo")
                                .font(Theme.Font.body(16, weight: .heavy))
                            Image(systemName: "chevron.right")
                                .font(.system(size: 14, weight: .black))
                        }
                        .foregroundStyle(.white)
                        .padding(.horizontal, 18)
                        .padding(.vertical, 14)
                        .background(.white.opacity(0.18), in: Capsule())
                    }
                    .buttonStyle(.plain)
                }
            }
            .padding(24)
        }
        .frame(height: 330)
        .clipShape(RoundedRectangle(cornerRadius: 30, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 30, style: .continuous)
                .stroke(.white.opacity(0.16), lineWidth: 1)
        )
        .shadow(color: Theme.Palette.blue500.opacity(0.20), radius: 28, x: 0, y: 16)
    }

    private var heroUrl: URL? {
        card.storyPreviews.compactMap { APIClient.shared.mediaURL(from: $0.mediaUrl) }.first
    }

    private var urgencyLabel: String {
        switch card.liveState {
        case .almostFull: return "Quasi pieno"
        case .hotNow: return "Sta salendo"
        case .growing: return "Si sta accendendo"
        case .wakingUp: return "Si sta svegliando"
        }
    }

    private var tensionCopy: String {
        if card.friendsArriving >= 3 {
            return "Il gruppo sta crescendo.\nPeak probabile tra 20 min"
        }
        if card.friendsHere >= 2 {
            return "Ci sono gia amici.\nIl momento migliore e adesso"
        }
        if card.storyPreviews.count > 0 {
            return "Qualcuno ha appena postato.\nSta succedendo qualcosa"
        }
        return "Il posto si sta muovendo.\nGuarda cosa ti stai perdendo"
    }

    private var friendCopy: String {
        let count = max(card.friendsHere, card.friendsArriving)
        return count == 1 ? "1 amico qui" : "\(max(count, 2)) amici qui"
    }
}

private struct HeroVenueMedia: View {
    let url: URL?

    var body: some View {
        Group {
            if let url {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .success(let image):
                        image.resizable().scaledToFill()
                    default:
                        fallback
                    }
                }
            } else {
                fallback
            }
        }
    }

    private var fallback: some View {
        ZStack {
            LinearGradient(
                colors: [Color(hex: 0x121A2A), Color(hex: 0x35204A), Color(hex: 0x120814)],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )
            Circle()
                .fill(Theme.Palette.blue500.opacity(0.34))
                .blur(radius: 50)
                .offset(x: 90, y: -40)
            Circle()
                .fill(Color(hex: 0xFF3D82).opacity(0.25))
                .blur(radius: 55)
                .offset(x: -90, y: 95)
            Image(systemName: "wineglass.fill")
                .font(.system(size: 86, weight: .black))
                .foregroundStyle(.white.opacity(0.10))
        }
    }
}

private struct PulseRing: View {
    let value: Int

    var body: some View {
        ZStack {
            Circle()
                .stroke(.white.opacity(0.16), lineWidth: 9)
            Circle()
                .trim(from: 0, to: CGFloat(min(value, 100)) / 100)
                .stroke(
                    AngularGradient(
                        colors: [Theme.Palette.blue500, Color(hex: 0xA855F7), Color(hex: 0xFF3D82), Theme.Palette.blue500],
                        center: .center
                    ),
                    style: StrokeStyle(lineWidth: 9, lineCap: .round)
                )
                .rotationEffect(.degrees(-90))
            VStack(spacing: 0) {
                Text("\(value)")
                    .font(.system(size: 42, weight: .black, design: .rounded))
                    .foregroundStyle(.white)
                    .contentTransition(.numericText())
                Text("pulse")
                    .font(Theme.Font.body(18, weight: .heavy))
                    .foregroundStyle(.white.opacity(0.72))
            }
        }
        .frame(width: 126, height: 126)
    }
}

private struct FeedStoryBubble: View {
    let imageUrl: URL?
    let avatarUrl: URL?
    let title: String
    let isLive: Bool

    var body: some View {
        ZStack(alignment: .bottom) {
            Circle()
                .stroke(
                    AngularGradient(
                        colors: [Theme.Palette.blue500, Color(hex: 0xA855F7), Color(hex: 0xFF3D82), Theme.Palette.blue500],
                        center: .center
                    ),
                    lineWidth: 4
                )
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

            if isLive {
                Text("LIVE")
                    .font(.system(size: 10, weight: .black, design: .rounded))
                    .foregroundStyle(.white)
                    .padding(.horizontal, 8)
                    .padding(.vertical, 4)
                    .background(Color(hex: 0xFF2F7D), in: Capsule())
                    .offset(y: 10)
            } else {
                Circle()
                    .fill(Theme.Palette.mint500)
                    .frame(width: 13, height: 13)
                    .overlay(Circle().stroke(Color.black, lineWidth: 2))
                    .offset(x: 24, y: 23)
            }
        }
        .frame(width: 82, height: 86)
    }
}

private struct FeedLiveMoment: Identifiable, Hashable {
    let id: String
    let title: String
    let subtitle: String
    let time: String
    let heat: Int
    let icon: String
    let imageUrl: URL?
    let story: FeedStoryPreview?

    static let mock: [FeedLiveMoment] = [
        FeedLiveMoment(id: "mock-luca", title: "Luca", subtitle: "appena arrivato", time: "2 min fa", heat: 12, icon: "play.fill", imageUrl: nil, story: nil),
        FeedLiveMoment(id: "mock-gigi", title: "Gigi", subtitle: "brindisi in terrazza", time: "5 min fa", heat: 9, icon: "play.fill", imageUrl: nil, story: nil),
        FeedLiveMoment(id: "mock-group", title: "3 persone", subtitle: "sono arrivate", time: "7 min fa", heat: 3, icon: "person.3.fill", imageUrl: nil, story: nil)
    ]
}

private struct LiveMomentRow: View {
    let moment: FeedLiveMoment
    var onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: 14) {
                MomentThumb(url: moment.imageUrl, icon: moment.icon)

                VStack(alignment: .leading, spacing: 3) {
                    HStack(spacing: 7) {
                        Text(moment.title)
                            .font(Theme.Font.title(20, weight: .heavy))
                            .foregroundStyle(.white)
                        Circle()
                            .fill(Theme.Palette.mint500)
                            .frame(width: 8, height: 8)
                    }
                    Text(moment.subtitle)
                        .font(Theme.Font.body(17, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.88))
                    Text(moment.time)
                        .font(Theme.Font.caption(13, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.46))
                }

                Spacer()

                HStack(spacing: 6) {
                    Image(systemName: moment.icon == "person.3.fill" ? "person.3.fill" : "flame.fill")
                        .font(.system(size: 15, weight: .black))
                        .foregroundStyle(moment.icon == "person.3.fill" ? Theme.Palette.blue400 : Color(hex: 0xFF5C7A))
                    Text("\(moment.heat)")
                        .font(Theme.Font.body(15, weight: .black))
                        .foregroundStyle(.white)
                }
                .padding(.horizontal, 14)
                .padding(.vertical, 10)
                .background(.white.opacity(0.10), in: Capsule())

                Image(systemName: "ellipsis")
                    .font(.system(size: 18, weight: .heavy))
                    .foregroundStyle(.white.opacity(0.42))
            }
            .padding(8)
            .background(.white.opacity(0.055), in: RoundedRectangle(cornerRadius: 22, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 22, style: .continuous)
                    .stroke(.white.opacity(0.07), lineWidth: 1)
            )
        }
        .buttonStyle(.plain)
    }
}

private struct MomentThumb: View {
    let url: URL?
    let icon: String

    var body: some View {
        ZStack {
            if let url {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .success(let image):
                        image.resizable().scaledToFill()
                    default:
                        fallback
                    }
                }
            } else {
                fallback
            }

            Circle()
                .fill(.white)
                .frame(width: 43, height: 43)
                .overlay(
                    Image(systemName: icon)
                        .font(.system(size: 17, weight: .black))
                        .foregroundStyle(.black)
                )
        }
        .frame(width: 138, height: 82)
        .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
    }

    private var fallback: some View {
        ZStack {
            LinearGradient(
                colors: [Theme.Palette.blue700, Color(hex: 0x741C84), Color(hex: 0x111827)],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )
            Image(systemName: "sparkles")
                .font(.system(size: 34, weight: .heavy))
                .foregroundStyle(.white.opacity(0.28))
        }
    }
}

private struct AvatarStack: View {
    let urls: [String?]
    let maxVisible: Int

    var body: some View {
        HStack(spacing: -10) {
            ForEach(Array(urls.prefix(maxVisible).enumerated()), id: \.offset) { index, raw in
                StoryAvatar(
                    url: APIClient.shared.mediaURL(from: raw),
                    size: 34,
                    hasStory: false,
                    initials: "\(index + 1)"
                )
                .overlay(Circle().stroke(.white.opacity(0.85), lineWidth: 2))
            }
            if urls.count > maxVisible {
                Text("+\(urls.count - maxVisible)")
                    .font(Theme.Font.caption(12, weight: .black))
                    .foregroundStyle(.white)
                    .frame(width: 34, height: 34)
                    .background(Circle().fill(.white.opacity(0.18)))
            }
        }
    }
}

private struct FeedEmptyMomentCard: View {
    var body: some View {
        VStack(spacing: 10) {
            Image(systemName: "sunrise.fill")
                .font(.system(size: 34, weight: .heavy))
                .foregroundStyle(Theme.Palette.blue400)
            Text("La citta si sta ancora svegliando")
                .font(Theme.Font.title(20, weight: .heavy))
                .foregroundStyle(.white)
            Text("Segui amici o esplora la mappa per accendere il tuo feed.")
                .font(Theme.Font.body(14, weight: .semibold))
                .foregroundStyle(.white.opacity(0.62))
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity)
        .padding(28)
        .background(.white.opacity(0.055), in: RoundedRectangle(cornerRadius: 24, style: .continuous))
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
