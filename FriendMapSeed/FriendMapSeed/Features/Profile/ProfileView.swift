//
//  ProfileView.swift
//  Cloudy — Profilo utente
//

import SwiftUI

struct ProfileView: View {
    @Environment(AuthStore.self) private var auth
    @State private var showEditProfile = false
    @State private var profile: EditableUserProfile?
    @State private var publicProfile: UserProfile?
    @State private var isLoadingProfile = false

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(spacing: Theme.Spacing.xl) {
                    if case .loggedIn(let user) = auth.state {
                        header(user: user)
                        statsRow
                        actionsList
                    }
                }
                .padding(Theme.Spacing.lg)
                .padding(.bottom, 130)
            }
            .background(Theme.Palette.surfaceAlt.ignoresSafeArea())
            .navigationTitle("Profilo")
            .navigationBarTitleDisplayMode(.large)
            .sheet(isPresented: $showEditProfile) {
                EditProfileView {
                    Task { await loadProfile() }
                }
            }
            .task { await loadProfile() }
            .refreshable { await loadProfile() }
        }
    }

    private func header(user: AuthUser) -> some View {
        VStack(spacing: 12) {
            StoryAvatar(
                url: APIClient.shared.mediaURL(from: profile?.avatarUrl),
                size: 110,
                hasStory: false,
                initials: String((user.displayName ?? user.nickname).prefix(1)).uppercased()
            )
            Text(user.displayName ?? user.nickname)
                .font(Theme.Font.display(24))
            Text("@\(user.nickname)")
                .font(Theme.Font.body(14))
                .foregroundStyle(Theme.Palette.inkSoft)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, Theme.Spacing.lg)
    }

    private var statsRow: some View {
        HStack {
            NavigationLink {
                FriendsListView()
            } label: {
                stat(value: "\(publicProfile?.friendsCount ?? 0)", label: "Amici")
            }
            .buttonStyle(.plain)
            stat(value: "0", label: "Tavoli")
            stat(value: "0", label: "Check-in")
        }
        .padding(.vertical, Theme.Spacing.md)
        .background(
            RoundedRectangle(cornerRadius: Theme.Radius.lg, style: .continuous)
                .fill(Theme.Palette.surface)
        )
        .cardShadow()
    }

    private func stat(value: String, label: String) -> some View {
        VStack(spacing: 2) {
            Text(value).font(Theme.Font.title(22, weight: .heavy))
            Text(label).font(Theme.Font.caption(12)).foregroundStyle(Theme.Palette.inkSoft)
        }
        .frame(maxWidth: .infinity)
    }

    private var actionsList: some View {
        VStack(spacing: 0) {
            Button {
                showEditProfile = true
            } label: {
                row(icon: "pencil.circle.fill", label: "Modifica profilo")
            }
            .buttonStyle(.plain)
            divider
            NavigationLink {
                PrivacyView()
            } label: {
                row(icon: "lock.shield.fill", label: "Privacy")
            }
            .buttonStyle(.plain)
            divider
            NavigationLink {
                StoryArchiveView()
            } label: {
                row(icon: "archivebox.fill", label: "Archivio storie")
            }
            .buttonStyle(.plain)
            divider
            NavigationLink {
                AddFriendsView()
            } label: {
                row(icon: "person.badge.plus", label: "Aggiungi amici")
            }
            .buttonStyle(.plain)
            divider
            row(icon: "questionmark.circle.fill", label: "Aiuto e supporto")
            divider
            Button(role: .destructive) {
                auth.logout()
            } label: {
                row(icon: "rectangle.portrait.and.arrow.right", label: "Esci", tint: Theme.Palette.densityHigh)
            }
            .buttonStyle(.plain)
        }
        .background(
            RoundedRectangle(cornerRadius: Theme.Radius.lg, style: .continuous)
                .fill(Theme.Palette.surface)
        )
        .cardShadow()
    }

    private var divider: some View {
        Rectangle().fill(Theme.Palette.hairline).frame(height: 0.5).padding(.leading, 50)
    }

    private func row(icon: String, label: String, tint: Color = Theme.Palette.honeyDeep) -> some View {
        HStack(spacing: 14) {
            Image(systemName: icon)
                .font(.system(size: 20))
                .foregroundStyle(tint)
                .frame(width: 28)
            Text(label).font(Theme.Font.body(15, weight: .semibold))
            Spacer()
            Image(systemName: "chevron.right")
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(Theme.Palette.inkMuted)
        }
        .padding(Theme.Spacing.md)
        .contentShape(Rectangle())
    }

    private func loadProfile() async {
        guard case .loggedIn(let user) = auth.state else { return }
        isLoadingProfile = true
        defer { isLoadingProfile = false }
        async let editable = API.myEditableProfile()
        async let publicUser = API.userProfile(userId: user.userId)
        profile = try? await editable
        publicProfile = try? await publicUser
    }
}
