//
//  ChatThreadsView.swift
//  Cloudy — Lista delle conversazioni dirette
//
//  Endpoint: GET /api/messages/threads
//

import SwiftUI

struct ChatThreadsView: View {
    @State private var threads: [DirectMessageThreadSummary] = []
    @State private var groupThreads: [GroupChatSummary] = []
    @State private var isLoading = false
    @State private var errorMessage: String?
    @State private var showAddFriends = false
    @State private var showNewChat = false
    @State private var showNewGroupChat = false
    @State private var hub: SocialHub?
    @State private var selectedPeer: SocialConnection?
    @State private var selectedGroup: GroupChatSummary?

    var body: some View {
        NavigationStack {
            List {
                Section {
                    if isLoading {
                        ProgressView()
                            .frame(maxWidth: .infinity)
                            .padding(.top, 80)
                            .listRowChrome()
                    } else if threads.isEmpty && groupThreads.isEmpty {
                        CloudyEmptyState(
                            icon: "bubble.left.and.bubble.right.fill",
                            title: "Nessuna chat",
                            message: "Inizia parlando con un amico"
                        )
                        .padding(.top, 60)
                        .listRowChrome()
                    } else {
                        ForEach(groupThreads) { group in
                            Button {
                                selectedGroup = group
                            } label: {
                                groupRow(group)
                            }
                            .buttonStyle(.plain)
                            .swipeActions(edge: .trailing, allowsFullSwipe: true) {
                                Button(role: .destructive) {
                                    Task { await deleteGroup(group) }
                                } label: {
                                    Label("Elimina", systemImage: "trash")
                                }
                            }
                            .listRowChrome()
                        }
                        ForEach(threads) { t in
                            NavigationLink {
                                ChatRoomView(otherUserId: t.otherUserId, peerName: t.displayName ?? t.nickname)
                            } label: {
                                threadRow(t)
                            }
                            .buttonStyle(.plain)
                            .swipeActions(edge: .trailing, allowsFullSwipe: true) {
                                Button(role: .destructive) {
                                    Task { await deleteThread(t) }
                                } label: {
                                    Label("Elimina", systemImage: "trash")
                                }
                            }
                            .listRowChrome()
                        }
                    }
                    if let errorMessage {
                        Text(errorMessage)
                            .font(Theme.Font.caption(12))
                            .foregroundStyle(Theme.Palette.densityHigh)
                            .listRowChrome()
                    }
                }
            }
            .listStyle(.plain)
            .scrollContentBackground(.hidden)
            .contentMargins(.horizontal, Theme.Spacing.lg, for: .scrollContent)
            .contentMargins(.bottom, 130, for: .scrollContent)
            .background(Theme.Palette.surfaceAlt.ignoresSafeArea())
            .navigationTitle("Chat")
            .navigationBarTitleDisplayMode(.large)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    HStack(spacing: 12) {
                        Button {
                            showNewGroupChat = true
                        } label: {
                            Image(systemName: "person.3.fill")
                                .font(.system(size: 17, weight: .heavy))
                        }
                        Button {
                            showNewChat = true
                        } label: {
                            Image(systemName: "square.and.pencil")
                                .font(.system(size: 17, weight: .heavy))
                        }
                        Button {
                            showAddFriends = true
                        } label: {
                            Image(systemName: "person.badge.plus")
                                .font(.system(size: 17, weight: .heavy))
                        }
                    }
                }
            }
            .sheet(isPresented: $showNewChat) {
                NewChatSelectionView(hub: hub, onSelect: { peer in
                    selectedPeer = peer
                })
            }
            .sheet(isPresented: $showNewGroupChat) {
                NewGroupChatView(hub: hub, onCreated: { group in
                    selectedGroup = group
                    Task { await load() }
                })
            }
            .sheet(isPresented: $showAddFriends) {
                AddFriendsView()
            }
            .navigationDestination(item: $selectedPeer) { peer in
                ChatRoomView(otherUserId: peer.userId, peerName: peer.displayName ?? peer.nickname)
            }
            .navigationDestination(item: $selectedGroup) { group in
                GroupChatRoomView(chatId: group.chatId, title: group.title)
            }
            .task { await load() }
            .refreshable { await load() }
        }
    }

    private func threadRow(_ t: DirectMessageThreadSummary) -> some View {
        HStack(spacing: 12) {
            StoryAvatar(
                url: APIClient.shared.mediaURL(from: t.avatarUrl),
                size: 50,
                hasStory: t.unreadCount > 0,
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
                    .font(Theme.Font.body(13, weight: t.unreadCount > 0 ? .bold : .regular))
                    .foregroundStyle(t.unreadCount > 0 ? Theme.Palette.ink : Theme.Palette.inkSoft)
                    .lineLimit(1)
            }
            if t.unreadCount > 0 {
                Text(t.unreadCount > 99 ? "99+" : "\(t.unreadCount)")
                    .font(.system(size: 12, weight: .black, design: .rounded))
                    .foregroundStyle(.white)
                    .padding(.horizontal, 8)
                    .frame(minWidth: 26, minHeight: 26)
                    .background(Capsule().fill(Theme.Palette.auroraPink))
                    .contentTransition(.numericText())
            }
        }
        .padding(Theme.Spacing.md)
        .background(
            RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                .fill(Theme.Palette.surface)
        )
        .cardShadow()
    }

    private func groupRow(_ group: GroupChatSummary) -> some View {
        HStack(spacing: 12) {
            ZStack {
                Circle().fill(Theme.Palette.blue50)
                Image(systemName: group.kind == "venue" ? "mappin.and.ellipse" : "person.3.fill")
                    .font(.system(size: 20, weight: .heavy))
                    .foregroundStyle(Theme.Palette.blue500)
            }
            .frame(width: 50, height: 50)

            VStack(alignment: .leading, spacing: 4) {
                HStack {
                    Text(group.title).font(Theme.Font.body(15, weight: .heavy))
                    Spacer()
                    Text(relative(group.lastMessageAtUtc))
                        .font(Theme.Font.caption(11))
                        .foregroundStyle(Theme.Palette.inkMuted)
                }
                Text(group.lastMessagePreview)
                    .font(Theme.Font.body(13, weight: group.unreadCount > 0 ? .bold : .regular))
                    .foregroundStyle(group.unreadCount > 0 ? Theme.Palette.ink : Theme.Palette.inkSoft)
                    .lineLimit(1)
                Text(group.kind == "venue" ? "Chat locale" : "\(group.memberCount) persone")
                    .font(Theme.Font.caption(11, weight: .medium))
                    .foregroundStyle(Theme.Palette.inkMuted)
            }

            if group.unreadCount > 0 {
                Text(group.unreadCount > 99 ? "99+" : "\(group.unreadCount)")
                    .font(.system(size: 12, weight: .black, design: .rounded))
                    .foregroundStyle(.white)
                    .padding(.horizontal, 8)
                    .frame(minWidth: 26, minHeight: 26)
                    .background(Capsule().fill(Theme.Palette.blue500))
                    .contentTransition(.numericText())
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
        errorMessage = nil
        defer { isLoading = false }
        async let fetchedThreads: [DirectMessageThreadSummary] = API.messageThreads()
        async let fetchedGroups: [GroupChatSummary] = API.groupChats()
        async let fetchedHub: SocialHub = API.socialHub()

        do {
            let value = try await fetchedThreads
            threads = value
        } catch {
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }

        do {
            let value = try await fetchedGroups
            groupThreads = value
        } catch {
            if errorMessage == nil {
                errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
            }
        }

        do {
            let value = try await fetchedHub
            hub = value
        } catch {
            if errorMessage == nil {
                errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
            }
        }
    }

    private func deleteThread(_ thread: DirectMessageThreadSummary) async {
        do {
            threads.removeAll { $0.otherUserId == thread.otherUserId }
            try await API.deleteDirectMessageThread(otherUserId: thread.otherUserId)
            NotificationCenter.default.post(name: .cloudyBadgesShouldRefresh, object: nil)
        } catch {
            Haptics.error()
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
            await load()
        }
    }

    private func deleteGroup(_ group: GroupChatSummary) async {
        do {
            groupThreads.removeAll { $0.chatId == group.chatId }
            try await API.deleteGroupChat(chatId: group.chatId)
            NotificationCenter.default.post(name: .cloudyBadgesShouldRefresh, object: nil)
        } catch {
            Haptics.error()
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
            await load()
        }
    }
}

private extension View {
    func listRowChrome() -> some View {
        self
            .listRowSeparator(.hidden)
            .listRowInsets(EdgeInsets(top: 6, leading: 0, bottom: 6, trailing: 0))
            .listRowBackground(Color.clear)
    }
}

// MARK: - New Chat Selection View

struct NewChatSelectionView: View {
    let hub: SocialHub?
    let onSelect: (SocialConnection) -> Void
    @Environment(\.dismiss) var dismiss

    var body: some View {
        NavigationStack {
            List {
                if let hub = hub, !hub.friends.isEmpty {
                    ForEach(hub.friends) { friend in
                        Button(action: {
                            onSelect(friend)
                            dismiss()
                        }) {
                            HStack(spacing: 12) {
                                StoryAvatar(
                                    url: APIClient.shared.mediaURL(from: friend.avatarUrl),
                                    size: 40,
                                    hasStory: false,
                                    initials: String((friend.displayName ?? friend.nickname).prefix(1)).uppercased()
                                )
                                VStack(alignment: .leading, spacing: 2) {
                                    Text(friend.displayName ?? friend.nickname)
                                        .font(Theme.Font.body(15, weight: .heavy))
                                        .foregroundStyle(Theme.Palette.ink)
                                    Text(friend.statusLabel)
                                        .font(Theme.Font.caption(11))
                                        .foregroundStyle(Theme.Palette.inkMuted)
                                }
                                Spacer()
                                if let venue = friend.currentVenueName {
                                    Text(venue)
                                        .font(Theme.Font.caption(11))
                                        .foregroundStyle(Theme.Palette.inkSoft)
                                        .lineLimit(1)
                                }
                            }
                            .contentShape(Rectangle())
                        }
                    }
                } else {
                    Text("Nessun amico")
                        .foregroundStyle(Theme.Palette.inkMuted)
                }
            }
            .navigationTitle("Nuova chat")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Annulla") { dismiss() }
                }
            }
        }
    }
}

// MARK: - New Group Chat

struct NewGroupChatView: View {
    let hub: SocialHub?
    let onCreated: (GroupChatSummary) -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var title = ""
    @State private var selectedUserIds = Set<UUID>()
    @State private var isSaving = false
    @State private var errorMessage: String?

    var body: some View {
        NavigationStack {
            List {
                Section("Nome") {
                    TextField("Titolo della chat", text: $title)
                }

                Section("Amici") {
                    if let friends = hub?.friends, !friends.isEmpty {
                        ForEach(friends) { friend in
                            Button {
                                toggle(friend.userId)
                            } label: {
                                HStack(spacing: 12) {
                                    StoryAvatar(
                                        url: APIClient.shared.mediaURL(from: friend.avatarUrl),
                                        size: 40,
                                        hasStory: false,
                                        initials: String((friend.displayName ?? friend.nickname).prefix(1)).uppercased()
                                    )
                                    VStack(alignment: .leading, spacing: 2) {
                                        Text(friend.displayName ?? friend.nickname)
                                            .font(Theme.Font.body(15, weight: .heavy))
                                            .foregroundStyle(Theme.Palette.ink)
                                        Text(friend.statusLabel)
                                            .font(Theme.Font.caption(11))
                                            .foregroundStyle(Theme.Palette.inkMuted)
                                    }
                                    Spacer()
                                    Image(systemName: selectedUserIds.contains(friend.userId) ? "checkmark.circle.fill" : "circle")
                                        .font(.system(size: 20, weight: .bold))
                                        .foregroundStyle(selectedUserIds.contains(friend.userId) ? Theme.Palette.blue500 : Theme.Palette.inkMuted)
                                }
                                .contentShape(Rectangle())
                            }
                            .buttonStyle(.plain)
                        }
                    } else {
                        Text("Aggiungi amici prima di creare un gruppo")
                            .foregroundStyle(Theme.Palette.inkMuted)
                    }
                }

                if let errorMessage {
                    Section {
                        Text(errorMessage)
                            .font(Theme.Font.caption(12, weight: .semibold))
                            .foregroundStyle(Theme.Palette.densityHigh)
                    }
                }
            }
            .navigationTitle("Nuovo gruppo")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Annulla") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button(isSaving ? "Creo..." : "Crea") {
                        Task { await create() }
                    }
                    .disabled(isSaving || cleanTitle.isEmpty || selectedUserIds.isEmpty)
                }
            }
        }
    }

    private var cleanTitle: String {
        title.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private func toggle(_ userId: UUID) {
        if selectedUserIds.contains(userId) {
            selectedUserIds.remove(userId)
        } else {
            selectedUserIds.insert(userId)
        }
        Haptics.tap()
    }

    private func create() async {
        isSaving = true
        errorMessage = nil
        defer { isSaving = false }
        do {
            let group = try await API.createGroupChat(title: cleanTitle, memberUserIds: Array(selectedUserIds))
            Haptics.success()
            onCreated(group)
            dismiss()
        } catch {
            Haptics.error()
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }
}
