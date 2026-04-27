//
//  AddFriendsView.swift
//  Cloudy — Aggiungi amici (ricerca + accept richieste)
//
//  Endpoint:
//   - GET  /api/users/search?q=...
//   - POST /api/social/friends/{userId}/request
//   - POST /api/social/friends/{userId}/accept
//   - POST /api/social/friends/{userId}/reject
//   - GET  /api/social/hub  (per richieste in entrata/uscita)
//

import SwiftUI

struct AddFriendsView: View {
    @Environment(\.dismiss) private var dismiss

    @State private var query: String = ""
    @State private var searchResults: [UserSearchResult] = []
    @State private var hub: SocialHub?
    @State private var isSearching = false
    @State private var errorMessage: String?

    private var hasIncoming: Bool { (hub?.incomingRequests.count ?? 0) > 0 }

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: Theme.Spacing.lg) {
                    searchField
                    if isSearching {
                        ProgressView().frame(maxWidth: .infinity).padding(.top, 40)
                    } else if !searchResults.isEmpty {
                        sectionTitle("Risultati")
                        VStack(spacing: 8) {
                            ForEach(searchResults) { user in
                                searchRow(user)
                            }
                        }
                    } else if !query.isEmpty {
                        CloudyEmptyState(
                            icon: "person.fill.questionmark",
                            title: "Nessun utente",
                            message: "Prova un altro nickname"
                        )
                        .padding(.top, 40)
                    }

                    if hasIncoming, let hub {
                        sectionTitle("Richieste in arrivo")
                        VStack(spacing: 8) {
                            ForEach(hub.incomingRequests) { conn in
                                incomingRow(conn)
                            }
                        }
                    }

                    if let errorMessage {
                        Text(errorMessage)
                            .font(Theme.Font.caption(12))
                            .foregroundStyle(Theme.Palette.densityHigh)
                    }
                }
                .padding(Theme.Spacing.lg)
                .padding(.bottom, 130)
            }
            .background(Theme.Palette.surfaceAlt.ignoresSafeArea())
            .navigationTitle("Aggiungi amici")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Chiudi") { dismiss() }
                }
            }
            .task { await loadHub() }
        }
    }

    // MARK: - UI parts

    private var searchField: some View {
        HStack(spacing: 8) {
            Image(systemName: "magnifyingglass")
                .foregroundStyle(Theme.Palette.inkMuted)
            TextField("Cerca per nickname o nome", text: $query)
                .textInputAutocapitalization(.never)
                .autocorrectionDisabled(true)
                .onSubmit { Task { await search() } }
            if !query.isEmpty {
                Button {
                    query = ""
                    searchResults = []
                } label: {
                    Image(systemName: "xmark.circle.fill")
                        .foregroundStyle(Theme.Palette.inkMuted)
                }
                .buttonStyle(.plain)
            }
        }
        .padding(12)
        .background(
            RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                .fill(Theme.Palette.surface)
        )
        .cardShadow()
    }

    private func sectionTitle(_ s: String) -> some View {
        Text(s)
            .font(Theme.Font.title(18, weight: .heavy))
    }

    private func searchRow(_ user: UserSearchResult) -> some View {
        HStack(spacing: 12) {
            StoryAvatar(
                url: user.avatarUrl.flatMap(URL.init(string:)),
                size: 50,
                hasStory: false,
                initials: String((user.displayName ?? user.nickname).prefix(1)).uppercased()
            )
            VStack(alignment: .leading, spacing: 2) {
                Text(user.displayName ?? user.nickname).font(Theme.Font.body(15, weight: .heavy))
                Text("@\(user.nickname)")
                    .font(Theme.Font.caption(12))
                    .foregroundStyle(Theme.Palette.inkSoft)
            }
            Spacer()
            actionButton(for: user)
        }
        .padding(Theme.Spacing.md)
        .background(
            RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                .fill(Theme.Palette.surface)
        )
        .cardShadow()
    }

    @ViewBuilder
    private func actionButton(for user: UserSearchResult) -> some View {
        switch user.relationshipStatus {
        case "friends":
            Label("Amici", systemImage: "checkmark.seal.fill")
                .font(Theme.Font.caption(12, weight: .heavy))
                .foregroundStyle(Theme.Palette.densityLow)
        case "request_sent":
            Text("Inviata")
                .font(Theme.Font.caption(12, weight: .heavy))
                .foregroundStyle(Theme.Palette.inkMuted)
        case "request_received":
            Button("Accetta") { Task { await accept(user.userId) } }
                .buttonStyle(.honeyCompact)
        default:
            Button {
                Task { await request(user.userId) }
            } label: {
                Image(systemName: "person.badge.plus.fill")
                    .font(.system(size: 16, weight: .heavy))
            }
            .buttonStyle(.honeyCompact)
        }
    }

    private func incomingRow(_ conn: SocialConnection) -> some View {
        HStack(spacing: 12) {
            StoryAvatar(
                url: conn.avatarUrl.flatMap(URL.init(string:)),
                size: 50,
                hasStory: false,
                initials: String((conn.displayName ?? conn.nickname).prefix(1)).uppercased()
            )
            VStack(alignment: .leading, spacing: 2) {
                Text(conn.displayName ?? conn.nickname).font(Theme.Font.body(15, weight: .heavy))
                Text("vuole essere tuo amico")
                    .font(Theme.Font.caption(12))
                    .foregroundStyle(Theme.Palette.inkSoft)
            }
            Spacer()
            HStack(spacing: 8) {
                Button {
                    Task { await accept(conn.userId) }
                } label: {
                    Image(systemName: "checkmark")
                        .font(.system(size: 14, weight: .heavy))
                }
                .buttonStyle(.honeyCompact)
                Button {
                    Task { await reject(conn.userId) }
                } label: {
                    Image(systemName: "xmark")
                        .font(.system(size: 14, weight: .heavy))
                }
                .buttonStyle(.ghost)
            }
        }
        .padding(Theme.Spacing.md)
        .background(
            RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                .fill(Theme.Palette.surface)
        )
        .cardShadow()
    }

    // MARK: - Actions

    private func search() async {
        let q = query.trimmingCharacters(in: .whitespaces)
        guard !q.isEmpty else { searchResults = []; return }
        isSearching = true
        defer { isSearching = false }
        do {
            searchResults = try await API.searchUsers(query: q)
            errorMessage = nil
        } catch {
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func loadHub() async {
        do { hub = try await API.socialHub() } catch { /* silenzio */ }
    }

    private func request(_ userId: UUID) async {
        do {
            _ = try await API.requestFriend(userId: userId)
            Haptics.success()
            await search()
        } catch {
            Haptics.error()
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func accept(_ userId: UUID) async {
        do {
            _ = try await API.acceptFriend(userId: userId)
            Haptics.success()
            await loadHub()
            await search()
        } catch {
            Haptics.error()
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func reject(_ userId: UUID) async {
        do {
            _ = try await API.rejectFriend(userId: userId)
            Haptics.tap()
            await loadHub()
        } catch {
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }
}
