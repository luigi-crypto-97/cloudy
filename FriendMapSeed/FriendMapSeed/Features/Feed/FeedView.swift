//
//  FeedView.swift
//  Cloudy — Feed social ispirato a Instagram
//
//  Layout: stories row in alto + feed verticale di "moments" (check-in,
//  intention, table create) degli amici. Doppio tap → like (haptic).
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
    var hiddenPresenceIds: Set<UUID> = []

    func load(showSpinner: Bool = true) async {
        if showSpinner { isLoading = true }
        error = nil
        defer { if showSpinner { isLoading = false } }
        do {
            async let s = API.stories()
            async let h = API.socialHub()
            async let p = API.myEditableProfile()
            self.stories = try await s
            self.hub = try await h
            self.profile = try? await p
        } catch {
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    func hidePresence(userId: UUID) {
        hiddenPresenceIds.insert(userId)
    }
}

struct FeedView: View {
    @State private var store = FeedStore()
    @State private var likedIds: Set<UUID> = []
    @State private var showCreateStory: Bool = false
    @State private var selectedChat: SocialConnection?

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(spacing: Theme.Spacing.xl) {

                    storiesRow
                        .padding(.horizontal, Theme.Spacing.lg)

                    Divider()
                        .background(Theme.Palette.hairline)

                    feedItems
                        .padding(.horizontal, Theme.Spacing.lg)
                }
                .padding(.top, Theme.Spacing.md)
                .padding(.bottom, 120)
            }
            .background(Theme.Palette.surfaceAlt.ignoresSafeArea())
            .refreshable { await store.load() }
            .navigationTitle("In giro")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    HStack(spacing: 6) {
                        Image(systemName: "cloud.fill")
                            .foregroundStyle(Theme.Palette.honey)
                        Text("Cloudy")
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
            .sheet(isPresented: $showCreateStory) {
                CreateStoryView(onCreated: {
                    Task { await store.load() }
                })
            }
            .navigationDestination(item: $selectedChat) { friend in
                ChatRoomView(otherUserId: friend.userId, peerName: friend.displayName ?? friend.nickname)
            }
            .task {
                await store.load()
                while !Task.isCancelled {
                    try? await Task.sleep(nanoseconds: 15_000_000_000)
                    await store.load(showSpinner: false)
                }
            }
        }
    }

    // MARK: - Stories row

    private var storiesRow: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(alignment: .top, spacing: 12) {
                // Tua storia placeholder
                myStoryButton

                // Stories amici
                ForEach(uniqueStorytellers, id: \.userId) { s in
                    NavigationLink {
                        StoryViewerView(stories: storiesByUser(s.userId))
                    } label: {
                        VStack(spacing: 6) {
                            StoryAvatar(
                                url: APIClient.shared.mediaURL(from: s.avatarUrl),
                                size: 64,
                                hasStory: true,
                                initials: String((s.displayName ?? s.nickname).prefix(1)).uppercased()
                            )
                            Text(s.displayName ?? s.nickname)
                                .font(Theme.Font.caption(11))
                                .foregroundStyle(Theme.Palette.inkSoft)
                                .lineLimit(1)
                                .frame(width: 72)
                        }
                    }
                    .buttonStyle(.plain)
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
                        .fill(Theme.Gradients.honeyCTA)
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
        return storiesByUser(myUserId)
    }

    private var myInitials: String {
        let name = store.profile?.displayName ?? store.profile?.nickname ?? "?"
        return String(name.prefix(1)).uppercased()
    }

    private var uniqueStorytellers: [UserStory] {
        var seen: Set<UUID> = []
        return store.stories.filter { seen.insert($0.userId).inserted }
    }

    private func storiesByUser(_ userId: UUID) -> [UserStory] {
        store.stories.filter { $0.userId == userId }.sorted { $0.createdAtUtc < $1.createdAtUtc }
    }

    // MARK: - Feed items

    private var feedItems: some View {
        VStack(spacing: Theme.Spacing.lg) {
            if store.isLoading && store.stories.isEmpty {
                ProgressView().padding()
            } else if let hub = store.hub {
                let friends = visibleFriends(from: hub)
                ForEach(friends.prefix(30)) { friend in
                    FeedCard(
                        friend: friend,
                        isLiked: likedIds.contains(friend.userId),
                        onLike: {
                            if !likedIds.insert(friend.userId).inserted {
                                likedIds.remove(friend.userId)
                            }
                            Haptics.tap()
                        },
                        onComment: {
                            selectedChat = friend
                        },
                        onDismissPresence: {
                            withAnimation(.cloudySnap) {
                                store.hidePresence(userId: friend.userId)
                            }
                        }
                    )
                }
                if friends.isEmpty {
                    CloudyEmptyState(
                        icon: "location.slash",
                        title: "Nessuna presenza live",
                        message: "Quando gli amici condividono la posizione li vedrai qui."
                    )
                }
            }
        }
    }

    private func visibleFriends(from hub: SocialHub) -> [SocialConnection] {
        hub.friends
            .filter { !store.hiddenPresenceIds.contains($0.userId) }
            .filter { $0.presenceState != "offline" || $0.currentVenueName != nil }
            .sorted { lhs, rhs in
                presenceRank(lhs) > presenceRank(rhs)
            }
    }

    private func presenceRank(_ friend: SocialConnection) -> Int {
        if friend.currentVenueName != nil { return 3 }
        if friend.presenceState == "live" || friend.presenceState == "active" { return 2 }
        if friend.presenceState != "offline" { return 1 }
        return 0
    }
}

// MARK: - Feed card

struct FeedCard: View {
    let friend: SocialConnection
    let isLiked: Bool
    let onLike: () -> Void
    let onComment: () -> Void
    let onDismissPresence: () -> Void

    @State private var doubleTapPulse: Bool = false
    @State private var isSaved: Bool = false

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            // Header
            HStack(spacing: 10) {
                StoryAvatar(
                    url: APIClient.shared.mediaURL(from: friend.avatarUrl),
                    size: 40,
                    hasStory: friend.presenceState != "offline",
                    initials: String((friend.displayName ?? friend.nickname).prefix(1)).uppercased()
                )
                VStack(alignment: .leading, spacing: 2) {
                    Text(friend.displayName ?? friend.nickname)
                        .font(Theme.Font.body(14, weight: .bold))
                        .foregroundStyle(Theme.Palette.ink)
                    Text(friend.statusLabel)
                        .font(Theme.Font.caption(12))
                        .foregroundStyle(Theme.Palette.inkSoft)
                }
                Spacer()
                Button(action: onDismissPresence) {
                    Image(systemName: "xmark.circle.fill")
                        .font(.system(size: 20, weight: .semibold))
                        .foregroundStyle(Theme.Palette.inkMuted)
                }
                .buttonStyle(.plain)
                .accessibilityLabel("Nascondi presenza")
            }
            .padding(Theme.Spacing.md)

            // Hero "venue card"
            ZStack {
                Theme.Gradients.cloudBody
                    .frame(height: 180)
                if let venue = friend.currentVenueName {
                    VStack(spacing: 8) {
                        Image(systemName: "mappin.and.ellipse")
                            .font(.system(size: 36, weight: .bold))
                            .foregroundStyle(Theme.Palette.honeyDeep)
                        Text(venue)
                            .font(Theme.Font.title(18, weight: .heavy))
                            .foregroundStyle(Theme.Palette.ink)
                        if let cat = friend.currentVenueCategory {
                            Text(cat.capitalized)
                                .font(Theme.Font.caption(12))
                                .foregroundStyle(Theme.Palette.inkSoft)
                        }
                    }
                } else {
                    VStack(spacing: 8) {
                        Image(systemName: "moon.stars.fill")
                            .font(.system(size: 36))
                            .foregroundStyle(Theme.Palette.skyDeep)
                        Text("Tranquillo stasera")
                            .font(Theme.Font.body(15, weight: .semibold))
                            .foregroundStyle(Theme.Palette.inkSoft)
                    }
                }

                // Doppio-tap heart pulse (stile Instagram)
                if doubleTapPulse {
                    Image(systemName: "heart.fill")
                        .font(.system(size: 92, weight: .black))
                        .foregroundStyle(.white)
                        .shadow(color: .black.opacity(0.3), radius: 12)
                        .transition(.scale.combined(with: .opacity))
                }
            }
            .clipShape(RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous))
            .padding(.horizontal, Theme.Spacing.md)
            .onTapGesture(count: 2) {
                if !isLiked { onLike() }
                withAnimation(.spring(response: 0.3, dampingFraction: 0.6)) {
                    doubleTapPulse = true
                }
                Task {
                    try? await Task.sleep(nanoseconds: 700_000_000)
                    withAnimation(.easeOut) { doubleTapPulse = false }
                }
            }

            // Action bar
            HStack(spacing: 18) {
                Button(action: onLike) {
                    Image(systemName: isLiked ? "heart.fill" : "heart")
                        .font(.system(size: 22, weight: .semibold))
                        .foregroundStyle(isLiked ? Theme.Palette.igPink : Theme.Palette.ink)
                        .scaleEffect(isLiked ? 1.1 : 1)
                        .animation(.spring(response: 0.3, dampingFraction: 0.5), value: isLiked)
                }
                Button(action: onComment) {
                    Image(systemName: "bubble.right")
                        .font(.system(size: 22, weight: .semibold))
                        .foregroundStyle(Theme.Palette.ink)
                }
                ShareLink(item: shareText) {
                    Image(systemName: "paperplane")
                        .font(.system(size: 22, weight: .semibold))
                        .foregroundStyle(Theme.Palette.ink)
                }
                Spacer()
                Button {
                    isSaved.toggle()
                    Haptics.tap()
                } label: {
                    Image(systemName: isSaved ? "bookmark.fill" : "bookmark")
                        .font(.system(size: 22, weight: .semibold))
                        .foregroundStyle(isSaved ? Theme.Palette.honeyDeep : Theme.Palette.ink)
                        .contentTransition(.symbolEffect(.replace))
                }
            }
            .padding(Theme.Spacing.md)
        }
        .background(
            RoundedRectangle(cornerRadius: Theme.Radius.lg, style: .continuous)
                .fill(Theme.Palette.surface)
        )
        .cardShadow()
    }

    private var shareText: String {
        if let venue = friend.currentVenueName {
            return "\(friend.displayName ?? friend.nickname) e in zona da \(venue) su Cloudy."
        }
        return "\(friend.displayName ?? friend.nickname) e su Cloudy. Raggiungici."
    }
}
