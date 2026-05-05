//
//  ChatRoomView.swift
//  Cloudy — Chat room (polling temporaneo, SignalR da abilitare)
//

import SwiftUI

struct ChatRoomView: View {
    let otherUserId: UUID
    let peerName: String
    
    @Environment(\.dismiss) private var dismiss
    @State private var messages: [DirectMessage] = []
    @State private var draft: String = ""
    @State private var isLoading = false
    @State private var errorMessage: String?
    
    var body: some View {
        VStack(spacing: 0) {
            // Messages
            ScrollViewReader { proxy in
                ScrollView {
                    if isLoading {
                        ProgressView()
                            .frame(maxWidth: .infinity, maxHeight: .infinity)
                            .padding(.top, 100)
                    } else if messages.isEmpty {
                        CloudyEmptyState(
                            icon: "bubble.left.and.bubble.right.fill",
                            title: "Nessun messaggio",
                            message: "Inizia la conversazione!"
                        )
                        .padding(.top, 100)
                    } else {
                        ForEach(messages) { message in
                            MessageBubble(message: message)
                                .id(message.messageId)
                        }
                    }
                }
                .padding(.horizontal)
            }
            
            // Composer
            composer
                .padding(.bottom, 16)
        }
        .background(Theme.Palette.surfaceAlt.ignoresSafeArea())
        .navigationTitle(peerName)
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button(role: .destructive) {
                    Task { await deleteThread() }
                } label: {
                    Image(systemName: "trash")
                }
            }
        }
        .task { await pollThread() }
    }
    
    private var composer: some View {
        HStack(spacing: 12) {
            TextField("Scrivi...", text: $draft, axis: .vertical)
                .lineLimit(1...4)
                .padding(.horizontal, 16)
                .padding(.vertical, 12)
                .background(Theme.Palette.surface, in: RoundedRectangle(cornerRadius: 24))
            
            Button {
                Task { await sendMessage() }
            } label: {
                Image(systemName: draft.isEmpty ? "hourglass" : "paperplane.fill")
                    .font(.system(size: 18, weight: .bold))
                    .foregroundStyle(.white)
                    .frame(width: 44, height: 44)
                    .background(Circle().fill(Theme.Palette.blue500))
            }
            .disabled(draft.trimmingCharacters(in: .whitespaces).isEmpty)
        }
        .padding(.horizontal)
    }
    
    private func load() async {
        if messages.isEmpty {
            let cached = DeviceCacheService.shared.cachedDirectMessages(otherUserId: otherUserId)
            let queued = DeviceCacheService.shared.queuedDirectMessages(otherUserId: otherUserId)
            if !cached.isEmpty || !queued.isEmpty {
                messages = mergeMessages(cached + queued)
            }
        }
        isLoading = messages.isEmpty
        do {
            await DeviceCacheService.shared.retryQueuedDirectMessages(otherUserId: otherUserId)
            let thread = try await API.messageThread(otherUserId: otherUserId)
            DeviceCacheService.shared.cacheDirectThread(thread, otherUserId: otherUserId)
            messages = mergeMessages(thread.messages + DeviceCacheService.shared.queuedDirectMessages(otherUserId: otherUserId))
            errorMessage = nil
        } catch {
            let cached = DeviceCacheService.shared.cachedDirectMessages(otherUserId: otherUserId)
            let queued = DeviceCacheService.shared.queuedDirectMessages(otherUserId: otherUserId)
            if !cached.isEmpty || !queued.isEmpty {
                messages = mergeMessages(cached + queued)
                errorMessage = "Mostro messaggi salvati sul dispositivo."
            } else {
                errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
            }
        }
        isLoading = false
    }
    
    private func pollThread() async {
        await load()
        while !Task.isCancelled {
            try? await Task.sleep(nanoseconds: 5_000_000_000) // 5 secondi
            if Task.isCancelled { return }
            await load()
        }
    }
    
    private func sendMessage() async {
        let text = draft.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty else { return }
        draft = ""
        
        do {
            _ = try await API.sendDirectMessage(otherUserId: otherUserId, body: text)
            Haptics.tap()
            await load()
        } catch {
            let queued = DeviceCacheService.shared.queueDirectMessage(otherUserId: otherUserId, body: text)
            messages = mergeMessages(messages + [queued])
            Haptics.error()
            errorMessage = "Messaggio salvato: lo invio appena torna la connessione."
        }
    }
    
    private func deleteThread() async {
        do {
            try await API.deleteDirectMessageThread(otherUserId: otherUserId)
            Haptics.success()
            dismiss()
        } catch {
            Haptics.error()
        }
    }

    private func mergeMessages(_ values: [DirectMessage]) -> [DirectMessage] {
        Dictionary(grouping: values, by: \.messageId)
            .compactMap { $0.value.first }
            .sorted { $0.sentAtUtc < $1.sentAtUtc }
    }
}

// MARK: - Message Bubble

struct MessageBubble: View {
    let message: DirectMessage
    
    var body: some View {
        HStack {
            if message.isMine { Spacer(minLength: 60) }
            
            VStack(alignment: message.isMine ? .trailing : .leading, spacing: 4) {
                if !message.isMine {
                    Text(message.displayName ?? message.nickname)
                        .font(.caption2)
                        .foregroundStyle(Theme.Palette.inkSoft)
                }
                
                Text(message.body)
                    .font(.body)
                    .padding(.horizontal, 14)
                    .padding(.vertical, 10)
                    .background(
                        RoundedRectangle(cornerRadius: 18)
                            .fill(message.isMine ? Theme.Palette.blue500 : Theme.Palette.surface)
                    )
                    .foregroundStyle(message.isMine ? .white : Theme.Palette.ink)
                
                Text(formatTime(message.sentAtUtc))
                    .font(.caption2)
                    .foregroundStyle(Theme.Palette.inkMuted)
            }
            
            if !message.isMine { Spacer(minLength: 60) }
        }
        .padding(.vertical, 4)
    }
    
    private func formatTime(_ date: Date) -> String {
        let formatter = DateFormatter()
        formatter.timeStyle = .short
        return formatter.string(from: date)
    }
}
