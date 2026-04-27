//
//  ChatRoomView.swift
//  Cloudy — Conversazione 1:1
//
//  Endpoint:
//   - GET  /api/messages/threads/{otherUserId}
//   - POST /api/messages/threads/{otherUserId}
//

import SwiftUI

struct ChatRoomView: View {
    let otherUserId: UUID
    let peerName: String

    @State private var thread: DirectMessageThread?
    @State private var draft: String = ""
    @State private var isSending = false
    @State private var errorMessage: String?
    @FocusState private var fieldFocused: Bool

    var body: some View {
        VStack(spacing: 0) {
            if thread == nil {
                ProgressView().frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                ScrollViewReader { proxy in
                    ScrollView {
                        LazyVStack(spacing: 8) {
                            ForEach(thread?.messages ?? []) { m in
                                bubble(for: m).id(m.messageId)
                            }
                        }
                        .padding(.horizontal, Theme.Spacing.lg)
                        .padding(.vertical, Theme.Spacing.md)
                    }
                    .onChange(of: thread?.messages.count ?? 0) { _, _ in
                        if let last = thread?.messages.last?.messageId {
                            withAnimation { proxy.scrollTo(last, anchor: .bottom) }
                        }
                    }
                }
            }

            if let errorMessage {
                Text(errorMessage)
                    .font(Theme.Font.caption(12))
                    .foregroundStyle(Theme.Palette.densityHigh)
                    .padding(.horizontal)
            }

            composer
        }
        .background(Theme.Palette.surfaceAlt.ignoresSafeArea())
        .navigationTitle(peerName)
        .navigationBarTitleDisplayMode(.inline)
        .task { await load() }
    }

    // MARK: - UI

    private func bubble(for m: DirectMessage) -> some View {
        HStack {
            if m.isMine { Spacer(minLength: 60) }
            VStack(alignment: m.isMine ? .trailing : .leading, spacing: 2) {
                Text(m.body)
                    .font(Theme.Font.body(15))
                    .foregroundStyle(m.isMine ? .white : Theme.Palette.ink)
                    .padding(.horizontal, 14)
                    .padding(.vertical, 10)
                    .background(
                        RoundedRectangle(cornerRadius: 18, style: .continuous)
                            .fill(m.isMine ? AnyShapeStyle(Theme.Gradients.honeyCTA) : AnyShapeStyle(Theme.Palette.surface))
                    )
                Text(timeOnly(m.sentAtUtc))
                    .font(Theme.Font.caption(10))
                    .foregroundStyle(Theme.Palette.inkMuted)
            }
            if !m.isMine { Spacer(minLength: 60) }
        }
    }

    private var composer: some View {
        HStack(spacing: 10) {
            TextField("Scrivi un messaggio…", text: $draft, axis: .vertical)
                .focused($fieldFocused)
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
        .background(Theme.Palette.surface.ignoresSafeArea(edges: .bottom))
    }

    private func timeOnly(_ d: Date) -> String {
        let f = DateFormatter()
        f.dateFormat = "HH:mm"
        return f.string(from: d)
    }

    // MARK: - Actions

    private func load() async {
        do {
            thread = try await API.messageThread(otherUserId: otherUserId)
            errorMessage = nil
        } catch {
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func send() async {
        let body = draft.trimmingCharacters(in: .whitespaces)
        guard !body.isEmpty else { return }
        isSending = true
        defer { isSending = false }
        do {
            let _ = try await API.sendDirectMessage(otherUserId: otherUserId, body: body)
            draft = ""
            Haptics.tap()
            await load()
        } catch {
            Haptics.error()
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }
}
