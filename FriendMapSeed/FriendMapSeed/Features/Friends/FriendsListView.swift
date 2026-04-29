//
//  FriendsListView.swift
//  Cloudy
//

import SwiftUI

struct FriendsListView: View {
    @State private var friends: [SocialConnection] = []
    @State private var isLoading = true
    @State private var errorMessage: String?

    var body: some View {
        List {
            if let errorMessage {
                Text(errorMessage)
                    .font(Theme.Font.caption(12, weight: .semibold))
                    .foregroundStyle(Theme.Palette.densityHigh)
            }

            ForEach(friends) { friend in
                NavigationLink {
                    FriendProfileView(userId: friend.userId)
                } label: {
                    friendRow(friend)
                }
            }
        }
        .listStyle(.plain)
        .overlay {
            if isLoading {
                ProgressView()
            } else if friends.isEmpty {
                CloudyEmptyState(
                    icon: "person.2",
                    title: "Nessun amico",
                    message: "Quando aggiungi amici li trovi qui."
                )
                .padding()
            }
        }
        .background(Theme.Palette.surfaceAlt.ignoresSafeArea())
        .navigationTitle("Amici")
        .navigationBarTitleDisplayMode(.large)
        .task { await loadFriends() }
        .refreshable { await loadFriends() }
    }

    private func friendRow(_ friend: SocialConnection) -> some View {
        HStack(spacing: 12) {
            StoryAvatar(
                url: APIClient.shared.mediaURL(from: friend.avatarUrl),
                size: 48,
                hasStory: false,
                initials: String((friend.displayName ?? friend.nickname).prefix(1)).uppercased()
            )
            VStack(alignment: .leading, spacing: 3) {
                Text(friend.displayName ?? friend.nickname)
                    .font(Theme.Font.body(15, weight: .heavy))
                    .foregroundStyle(Theme.Palette.ink)
                Text(friend.statusLabel)
                    .font(Theme.Font.caption(12, weight: .semibold))
                    .foregroundStyle(Theme.Palette.inkSoft)
                    .lineLimit(1)
            }
        }
        .padding(.vertical, 6)
    }

    private func loadFriends() async {
        isLoading = true
        defer { isLoading = false }
        do {
            friends = try await API.socialHub().friends
            errorMessage = nil
        } catch {
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }
}

struct FriendProfileView: View {
    let userId: UUID

    @Environment(AppRouter.self) private var router
    @State private var profile: UserProfile?
    @State private var isLoading = true
    @State private var errorMessage: String?

    var body: some View {
        ScrollView {
            VStack(spacing: Theme.Spacing.lg) {
                if let profile {
                    header(profile)
                    infoCard(profile)
                    interestsCard(profile)
                    actions(profile)
                } else if let errorMessage {
                    Text(errorMessage)
                        .font(Theme.Font.body(14, weight: .semibold))
                        .foregroundStyle(Theme.Palette.densityHigh)
                        .padding()
                }
            }
            .padding(Theme.Spacing.lg)
            .padding(.bottom, 80)
        }
        .overlay {
            if isLoading {
                ProgressView()
            }
        }
        .background(Theme.Palette.surfaceAlt.ignoresSafeArea())
        .navigationTitle(profile?.displayName ?? profile?.nickname ?? "Profilo")
        .navigationBarTitleDisplayMode(.inline)
        .task { await loadProfile() }
        .refreshable { await loadProfile() }
    }

    private func header(_ profile: UserProfile) -> some View {
        VStack(spacing: 12) {
            StoryAvatar(
                url: APIClient.shared.mediaURL(from: profile.avatarUrl),
                size: 104,
                hasStory: false,
                initials: String((profile.displayName ?? profile.nickname).prefix(1)).uppercased()
            )
            Text(profile.displayName ?? profile.nickname)
                .font(Theme.Font.display(24))
                .foregroundStyle(Theme.Palette.ink)
            Text("@\(profile.nickname)")
                .font(Theme.Font.body(14, weight: .semibold))
                .foregroundStyle(Theme.Palette.inkSoft)
            Text(profile.statusLabel)
                .font(Theme.Font.caption(12, weight: .heavy))
                .foregroundStyle(Theme.Palette.blue700)
                .padding(.horizontal, 12)
                .padding(.vertical, 7)
                .background(Theme.Palette.blue50, in: Capsule())
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, Theme.Spacing.lg)
        .background(Theme.Palette.surface, in: RoundedRectangle(cornerRadius: Theme.Radius.xl, style: .continuous))
        .cardShadow()
    }

    private func infoCard(_ profile: UserProfile) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Info")
                .font(Theme.Font.title(18, weight: .heavy))
            if let bio = profile.bio, !bio.isEmpty {
                Text(bio)
                    .font(Theme.Font.body(15))
                    .foregroundStyle(Theme.Palette.ink)
            }
            if let venue = profile.currentVenueName {
                Label("\(venue) \(profile.currentVenueCategory ?? "")", systemImage: "mappin.circle.fill")
                    .font(Theme.Font.body(14, weight: .semibold))
                    .foregroundStyle(Theme.Palette.inkSoft)
            }
            HStack {
                stat("\(profile.friendsCount)", "amici")
                stat("\(profile.mutualFriendsCount)", "in comune")
            }
        }
        .padding(Theme.Spacing.lg)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Theme.Palette.surface, in: RoundedRectangle(cornerRadius: Theme.Radius.lg, style: .continuous))
        .cardShadow()
    }

    private func interestsCard(_ profile: UserProfile) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Interessi")
                .font(Theme.Font.title(18, weight: .heavy))
            if profile.interests.isEmpty {
                Text("Nessun interesse visibile.")
                    .font(Theme.Font.body(14))
                    .foregroundStyle(Theme.Palette.inkSoft)
            } else {
                FlowLayout(spacing: 8) {
                    ForEach(profile.interests, id: \.self) { interest in
                        Text(interest)
                            .font(Theme.Font.caption(12, weight: .heavy))
                            .foregroundStyle(Theme.Palette.blue700)
                            .padding(.horizontal, 10)
                            .padding(.vertical, 7)
                            .background(Theme.Palette.blue50, in: Capsule())
                    }
                }
            }
        }
        .padding(Theme.Spacing.lg)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Theme.Palette.surface, in: RoundedRectangle(cornerRadius: Theme.Radius.lg, style: .continuous))
        .cardShadow()
    }

    private func actions(_ profile: UserProfile) -> some View {
        Button {
            router.presentedChat = ChatRoute(userId: profile.userId, title: profile.displayName ?? profile.nickname)
        } label: {
            Label("Scrivi messaggio", systemImage: "paperplane.fill")
                .frame(maxWidth: .infinity)
        }
        .buttonStyle(.honey)
        .disabled(!profile.canMessageDirectly)
    }

    private func stat(_ value: String, _ label: String) -> some View {
        VStack(spacing: 3) {
            Text(value).font(Theme.Font.title(20, weight: .heavy))
            Text(label).font(Theme.Font.caption(11)).foregroundStyle(Theme.Palette.inkSoft)
        }
        .frame(maxWidth: .infinity)
    }

    private func loadProfile() async {
        isLoading = true
        defer { isLoading = false }
        do {
            profile = try await API.userProfile(userId: userId)
            errorMessage = nil
        } catch {
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }
}
