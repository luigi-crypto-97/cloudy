//
//  ChatThreadsView.swift
//  Cloudy — Lista delle conversazioni dirette
//
//  Endpoint: GET /api/messages/threads
//

import SwiftUI

struct ChatThreadsView: View {
    @State private var threads: [DirectMessageThreadSummary] = []
    @State private var isLoading = false
    @State private var errorMessage: String?
    @State private var showAddFriends = false

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(spacing: Theme.Spacing.md) {
                    if isLoading {
                        ProgressView().padding(.top, 80)
                    } else if threads.isEmpty {
                        CloudyEmptyState(
                            icon: "bubble.left.and.bubble.right.fill",
                            title: "Nessuna chat",
                            message: "Inizia parlando con un amico"
                        )
                        .padding(.top, 60)
                    } else {
                        ForEach(threads) { t in
                            NavigationLink {
                                ChatRoomView(otherUserId: t.otherUserId, peerName: t.displayName ?? t.nickname)
                            } label: {
                                threadRow(t)
                            }
                            .buttonStyle(.plain)
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
            .navigationTitle("Chat")
            .navigationBarTitleDisplayMode(.large)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        showAddFriends = true
                    } label: {
                        Image(systemName: "person.badge.plus")
                            .font(.system(size: 17, weight: .heavy))
                    }
                }
            }
            .sheet(isPresented: $showAddFriends) {
                AddFriendsView()
            }
            .task { await load() }
            .refreshable { await load() }
        }
    }

    private func threadRow(_ t: DirectMessageThreadSummary) -> some View {
        HStack(spacing: 12) {
            StoryAvatar(
                url: t.avatarUrl.flatMap(URL.init(string:)),
                size: 50,
                hasStory: false,
                initials: String((t.displayName ?? t.nickname).prefix(1)).uppercased()
            )
            VStack(alignment: .leading, spacing: 4) {
                HStack {
                    Text(t.displayName ?? t.nickname).font(Theme.Font.body(15, weight: .heavy))
                    Spacer()
                    Text(relative(t.lastMessageAtUtc))
                        .font(Theme.Font.caption(11))
                        .foregroundStyle(Theme.Palette.inkMuted)
                }
                Text(t.lastMessagePreview)
                    .font(Theme.Font.body(13))
                    .foregroundStyle(Theme.Palette.inkSoft)
                    .lineLimit(1)
            }
        }
        .padding(Theme.Spacing.md)
        .background(
            RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                .fill(Theme.Palette.surface)
        )
        .cardShadow()
    }

    private func relative(_ d: Date) -> String {
        let f = RelativeDateTimeFormatter()
        f.unitsStyle = .short
        f.locale = Locale(identifier: "it_IT")
        return f.localizedString(for: d, relativeTo: Date())
    }

    private func load() async {
        isLoading = true
        defer { isLoading = false }
        do {
            threads = try await API.messageThreads()
            errorMessage = nil
        } catch {
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }
}
