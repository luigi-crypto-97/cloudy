//
//  TableThreadView.swift
//  Cloudy — Dettaglio tavolo + chat partecipanti + gestione richieste (host)
//
//  Endpoint:
//   - GET  /api/social/tables/{tableId}/thread
//   - POST /api/social/tables/{tableId}/messages
//   - POST /api/social/tables/{tableId}/participants/{userId}/approve
//   - POST /api/social/tables/{tableId}/participants/{userId}/reject
//

import SwiftUI

struct TableThreadView: View {
    let tableId: UUID

    @Environment(\.dismiss) private var dismiss
    @State private var thread: SocialTableThread?
    @State private var draft: String = ""
    @State private var isSending = false
    @State private var errorMessage: String?

    var body: some View {
        VStack(spacing: 0) {
            if let thread {
                ScrollView {
                    VStack(spacing: Theme.Spacing.md) {
                        header(thread.table)
                        if thread.table.isHost, !thread.requests.isEmpty {
                            requestsSection(thread.requests)
                        }
                        messagesSection(thread.messages)
                    }
                    .padding(Theme.Spacing.lg)
                    .padding(.bottom, 120)
                }
            } else {
                ProgressView().frame(maxWidth: .infinity, maxHeight: .infinity)
            }

            if let errorMessage {
                Text(errorMessage)
                    .font(Theme.Font.caption(12))
                    .foregroundStyle(Theme.Palette.densityHigh)
                    .padding(.horizontal)
            }
        }
        .background(Theme.Palette.surfaceAlt.ignoresSafeArea())
        .safeAreaInset(edge: .bottom) {
            composer
                .padding(.bottom, 96)
        }
        .navigationTitle(thread?.table.title ?? "Tavolo")
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            if thread?.table.isHost == true {
                ToolbarItem(placement: .topBarTrailing) {
                    Button(role: .destructive) {
                        Task { await deleteTable() }
                    } label: {
                        Image(systemName: "trash")
                    }
                    .accessibilityLabel("Cancella tavolo")
                }
            }
        }
        .task { await pollThread() }
    }

    // MARK: - Sections

    private func header(_ t: SocialTableSummary) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(t.title).font(Theme.Font.display(22))
            HStack(spacing: 12) {
                Label(t.venueName, systemImage: "mappin.circle.fill")
                    .font(Theme.Font.caption(12))
                    .foregroundStyle(Theme.Palette.inkSoft)
                Text(formatDate(t.startsAtUtc))
                    .font(Theme.Font.caption(12))
                    .foregroundStyle(Theme.Palette.inkSoft)
            }
            HStack {
                CloudyPill(text: "\(t.acceptedCount)/\(t.capacity) accettati", icon: "person.2.fill", tone: .honey)
                if t.requestedCount > 0 {
                    CloudyPill(text: "\(t.requestedCount) in attesa", icon: "hourglass", tone: .warning)
                }
                if t.isHost { CloudyPill(text: "Host", icon: "crown.fill", tone: .honey) }
            }
            if let desc = t.description, !desc.isEmpty {
                Text(desc).font(Theme.Font.body(14)).foregroundStyle(Theme.Palette.inkSoft)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Theme.Spacing.lg)
        .background(
            RoundedRectangle(cornerRadius: Theme.Radius.lg, style: .continuous)
                .fill(Theme.Palette.surface)
        )
        .cardShadow()
    }

    private func requestsSection(_ rs: [SocialTableRequest]) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Richieste di partecipazione")
                .font(Theme.Font.title(16, weight: .heavy))
            ForEach(rs) { r in
                HStack(spacing: 10) {
                    StoryAvatar(
                        url: r.avatarUrl.flatMap(URL.init(string:)),
                        size: 40,
                        hasStory: false,
                        initials: String((r.displayName ?? r.nickname).prefix(1)).uppercased()
                    )
                    VStack(alignment: .leading, spacing: 2) {
                        Text(r.displayName ?? r.nickname).font(Theme.Font.body(14, weight: .heavy))
                        Text("@\(r.nickname)").font(Theme.Font.caption(11)).foregroundStyle(Theme.Palette.inkSoft)
                    }
                    Spacer()
                    Button {
                        Task { await approve(r.userId) }
                    } label: {
                        Image(systemName: "checkmark").font(.system(size: 14, weight: .heavy))
                    }
                    .buttonStyle(.honeyCompact)
                    Button {
                        Task { await reject(r.userId) }
                    } label: {
                        Image(systemName: "xmark").font(.system(size: 14, weight: .heavy))
                    }
                    .buttonStyle(.ghost)
                }
                .padding(10)
                .background(
                    RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                        .fill(Theme.Palette.surface)
                )
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private func messagesSection(_ ms: [SocialTableMessage]) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Conversazione del tavolo")
                .font(Theme.Font.title(16, weight: .heavy))
            if ms.isEmpty {
                VStack(spacing: 8) {
                    Image(systemName: "bubble.left.and.bubble.right.fill")
                        .font(.system(size: 28, weight: .heavy))
                        .foregroundStyle(Theme.Palette.blue500)
                    Text("La chat del tavolo è pronta")
                        .font(Theme.Font.body(15, weight: .heavy))
                        .foregroundStyle(Theme.Palette.ink)
                    Text("Scrivi il primo messaggio qui sotto.")
                        .font(Theme.Font.caption(12))
                        .foregroundStyle(Theme.Palette.inkMuted)
                }
                .frame(maxWidth: .infinity)
                .padding(Theme.Spacing.lg)
                .background(
                    RoundedRectangle(cornerRadius: Theme.Radius.lg, style: .continuous)
                        .fill(Theme.Palette.surface)
                )
            } else {
                ForEach(ms) { m in
                    bubble(m)
                }
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private func bubble(_ m: SocialTableMessage) -> some View {
        HStack {
            if m.isMine { Spacer(minLength: 60) }
            VStack(alignment: m.isMine ? .trailing : .leading, spacing: 2) {
                if !m.isMine {
                    Text(m.displayName ?? m.nickname)
                        .font(Theme.Font.caption(11, weight: .heavy))
                        .foregroundStyle(Theme.Palette.inkSoft)
                }
                ChatMessageBubbleContent(messageBody: m.body, isMine: m.isMine)
            }
            if !m.isMine { Spacer(minLength: 60) }
        }
    }

    private var composer: some View {
        HStack(spacing: 10) {
            TextField("Scrivi al tavolo…", text: $draft, axis: .vertical)
                .lineLimit(1...4)
                .padding(.horizontal, 14)
                .padding(.vertical, 10)
                .background(
                    RoundedRectangle(cornerRadius: Theme.Radius.pill, style: .continuous)
                        .fill(Theme.Palette.surfaceAlt)
                )
            Button {
                Task { await send() }
            } label: {
                Image(systemName: isSending ? "hourglass" : "paperplane.fill")
                    .font(.system(size: 18, weight: .heavy))
                    .foregroundStyle(.white)
                    .frame(width: 44, height: 44)
                    .background(Circle().fill(Theme.Gradients.honeyCTA))
            }
            .disabled(draft.trimmingCharacters(in: .whitespaces).isEmpty || isSending)
        }
        .padding(.horizontal, Theme.Spacing.lg)
        .padding(.vertical, Theme.Spacing.md)
        .background(.ultraThinMaterial)
        .clipShape(RoundedRectangle(cornerRadius: Theme.Radius.xl, style: .continuous))
        .padding(.horizontal, Theme.Spacing.md)
    }

    private func formatDate(_ d: Date) -> String {
        let f = DateFormatter()
        f.dateStyle = .medium
        f.timeStyle = .short
        f.locale = Locale(identifier: "it_IT")
        return f.string(from: d)
    }

    // MARK: - Actions

    private func load() async {
        if thread == nil, let cached = cachedThread() {
            thread = cached
        }
        do {
            await DeviceCacheService.shared.retryQueuedTableMessages(tableId: tableId)
            let loaded = try await API.tableThread(tableId: tableId)
            DeviceCacheService.shared.cacheTableThread(loaded, tableId: tableId)
            let queued = DeviceCacheService.shared.queuedTableMessages(tableId: tableId)
            thread = SocialTableThread(
                table: loaded.table,
                requests: loaded.requests,
                messages: mergeMessages(loaded.messages + queued)
            )
            errorMessage = nil
        } catch {
            if let cached = cachedThread() {
                thread = cached
                errorMessage = "Mostro messaggi salvati sul dispositivo."
            } else {
                errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
            }
        }
    }

    private func cachedThread() -> SocialTableThread? {
        let cachedMessages = DeviceCacheService.shared.cachedTableMessages(tableId: tableId)
        let queuedMessages = DeviceCacheService.shared.queuedTableMessages(tableId: tableId)
        let messages = mergeMessages(cachedMessages + queuedMessages)
        guard !messages.isEmpty else { return nil }
        let summary = thread?.table ?? SocialTableSummary(
            tableId: tableId,
            title: "Tavolo",
            description: nil,
            startsAtUtc: Date(),
            venueName: "Locale",
            venueCategory: "locale",
            joinPolicy: "open",
            isHost: false,
            membershipStatus: "accepted",
            capacity: 0,
            requestedCount: 0,
            acceptedCount: 0,
            invitedCount: 0
        )
        return SocialTableThread(table: summary, requests: thread?.requests ?? [], messages: messages)
    }

    private func pollThread() async {
        await load()
        while !Task.isCancelled {
            try? await Task.sleep(nanoseconds: 5_000_000_000)
            if Task.isCancelled { return }
            await load()
        }
    }

    private func send() async {
        let body = draft.trimmingCharacters(in: .whitespaces)
        guard !body.isEmpty else { return }
        isSending = true
        defer { isSending = false }
        do {
            _ = try await API.sendTableMessage(tableId: tableId, body: body)
            draft = ""
            Haptics.tap()
            await load()
        } catch {
            let queued = DeviceCacheService.shared.queueTableMessage(tableId: tableId, body: body)
            appendLocalMessage(queued)
            draft = ""
            Haptics.error()
            errorMessage = "Messaggio salvato: lo invio appena torna la connessione."
        }
    }

    private func appendLocalMessage(_ message: SocialTableMessage) {
        guard let current = thread else { return }
        thread = SocialTableThread(
            table: current.table,
            requests: current.requests,
            messages: mergeMessages(current.messages + [message])
        )
    }

    private func mergeMessages(_ values: [SocialTableMessage]) -> [SocialTableMessage] {
        Dictionary(grouping: values, by: \.messageId)
            .compactMap { $0.value.first }
            .sorted { $0.sentAtUtc < $1.sentAtUtc }
    }

    private func approve(_ userId: UUID) async {
        do {
            _ = try await API.approveTableParticipant(tableId: tableId, userId: userId)
            Haptics.success()
            await load()
        } catch {
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func reject(_ userId: UUID) async {
        do {
            _ = try await API.rejectTableParticipant(tableId: tableId, userId: userId)
            Haptics.tap()
            await load()
        } catch {
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func deleteTable() async {
        do {
            _ = try await API.deleteTable(tableId: tableId)
            Haptics.success()
            dismiss()
        } catch {
            Haptics.error()
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }
}
