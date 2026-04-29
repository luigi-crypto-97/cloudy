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
    var isLoading: Bool = false
    var error: String?

    func load() async {
        isLoading = true
        error = nil
        defer { isLoading = false }
        do {
            async let s = API.stories()
            async let h = API.socialHub()
            self.stories = try await s
            self.hub = try await h
        } catch {
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
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
    @State private var store = FeedStore()
    @State private var likedIds: Set<UUID> = []
    @State private var showCreateStory: Bool = false
    @State private var viewerConfig: StoryViewerConfig? = nil

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
            .task { await store.load() }
        }
    }

    // MARK: - Stories row

    private var storiesRow: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(alignment: .top, spacing: 12) {
                // Tua storia placeholder
                myStoryButton

                // Stories amici
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
                                    url: URL(string: first.avatarUrl ?? ""),
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
                    Circle()
                        .fill(Theme.Palette.surface)
                        .frame(width: 64, height: 64)
                        .overlay(Circle().stroke(Theme.Palette.hairline, lineWidth: 2))
                        .overlay(
                            Image(systemName: "person.crop.circle.fill")
                                .font(.system(size: 56))
                                .foregroundStyle(Theme.Palette.inkMuted)
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

    // MARK: - Feed items

    private var feedItems: some View {
        VStack(spacing: Theme.Spacing.lg) {
            if store.isLoading && store.stories.isEmpty {
                ProgressView().padding()
            } else if let hub = store.hub {
                ForEach(hub.friends.prefix(20)) { friend in
                    FeedCard(
                        friend: friend,
                        isLiked: likedIds.contains(friend.userId),
                        onLike: {
                            if !likedIds.insert(friend.userId).inserted {
                                likedIds.remove(friend.userId)
                            }
                            Haptics.tap()
                        }
                    )
                }
                if hub.friends.isEmpty {
                    CloudyEmptyState(
                        icon: "person.2.slash",
                        title: "Nessun amico ancora",
                        message: "Quando aggiungi amici li vedrai qui con i loro check-in e piani."
                    )
                }
            }
        }
    }
}

// MARK: - Feed card

struct FeedCard: View {
    let friend: SocialConnection
    let isLiked: Bool
    let onLike: () -> Void

    @State private var doubleTapPulse: Bool = false

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            // Header
            HStack(spacing: 10) {
                StoryAvatar(
                    url: URL(string: friend.avatarUrl ?? ""),
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
                Image(systemName: "ellipsis")
                    .foregroundStyle(Theme.Palette.inkMuted)
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
                Button {} label: {
                    Image(systemName: "bubble.right")
                        .font(.system(size: 22, weight: .semibold))
                        .foregroundStyle(Theme.Palette.ink)
                }
                Button {} label: {
                    Image(systemName: "paperplane")
                        .font(.system(size: 22, weight: .semibold))
                        .foregroundStyle(Theme.Palette.ink)
                }
                Spacer()
                Image(systemName: "bookmark")
                    .font(.system(size: 22, weight: .semibold))
                    .foregroundStyle(Theme.Palette.ink)
            }
            .padding(Theme.Spacing.md)
        }
        .background(
            RoundedRectangle(cornerRadius: Theme.Radius.lg, style: .continuous)
                .fill(Theme.Palette.surface)
        )
        .cardShadow()
    }
}
