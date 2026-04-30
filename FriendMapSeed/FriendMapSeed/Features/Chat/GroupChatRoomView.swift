//
//  GroupChatRoomView.swift
//  Cloudy
//

import SwiftUI
import PhotosUI
import UniformTypeIdentifiers

struct GroupChatRoomView: View {
    let chatId: UUID?
    let venueId: UUID?
    let title: String

    @Environment(AppRouter.self) private var router

    @State private var thread: GroupChatThread?
    @State private var draft = ""
    @State private var isSending = false
    @State private var errorMessage: String?
    @State private var photoItem: PhotosPickerItem?
    @State private var showsFileImporter = false

    init(chatId: UUID, title: String) {
        self.chatId = chatId
        self.venueId = nil
        self.title = title
    }

    init(venueId: UUID, title: String) {
        self.chatId = nil
        self.venueId = venueId
        self.title = title
    }

    var body: some View {
        VStack(spacing: 0) {
            if thread == nil {
                ProgressView().frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                ScrollViewReader { proxy in
                    ScrollView {
                        LazyVStack(spacing: 8) {
                            ForEach(thread?.messages ?? []) { message in
                                bubble(for: message).id(message.messageId)
                            }
                        }
                        .padding(.horizontal, Theme.Spacing.lg)
                        .padding(.vertical, Theme.Spacing.md)
                    }
                    .onChange(of: thread?.messages.count ?? 0) { _, _ in
                        if let last = thread?.messages.last?.messageId {
                            withAnimation(.cloudySoft) { proxy.scrollTo(last, anchor: .bottom) }
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
        .navigationTitle(thread?.chat.title ?? title)
        .navigationBarTitleDisplayMode(.inline)
        .onAppear { router.isTabBarHidden = true }
        .onDisappear { router.isTabBarHidden = false }
        .task { await pollThread() }
        .onChange(of: photoItem) { _, item in
            Task { await sendPhoto(item) }
        }
        .fileImporter(isPresented: $showsFileImporter, allowedContentTypes: [.item], allowsMultipleSelection: false) { result in
            Task { await sendImportedFile(result) }
        }
    }

    private func bubble(for message: GroupChatMessage) -> some View {
        HStack {
            if message.isMine { Spacer(minLength: 58) }
            VStack(alignment: message.isMine ? .trailing : .leading, spacing: 3) {
                if !message.isMine {
                    Text(message.displayName ?? message.nickname)
                        .font(Theme.Font.caption(11, weight: .bold))
                        .foregroundStyle(Theme.Palette.inkMuted)
                }
                ChatMessageBubbleContent(messageBody: message.body, isMine: message.isMine)
                Text(timeOnly(message.sentAtUtc))
                    .font(Theme.Font.caption(10))
                    .foregroundStyle(Theme.Palette.inkMuted)
            }
            if !message.isMine { Spacer(minLength: 58) }
        }
    }

    private var composer: some View {
        HStack(spacing: 10) {
            PhotosPicker(selection: $photoItem, matching: .images) {
                Image(systemName: isSending ? "hourglass" : "photo.fill")
                    .font(.system(size: 16, weight: .heavy))
                    .foregroundStyle(Theme.Palette.blue500)
                    .frame(width: 36, height: 36)
                    .background(Circle().fill(Theme.Palette.blue50))
            }
            .disabled(isSending)

            Button {
                showsFileImporter = true
            } label: {
                Image(systemName: "paperclip")
                    .font(.system(size: 16, weight: .heavy))
                    .foregroundStyle(Theme.Palette.blue500)
                    .frame(width: 36, height: 36)
                    .background(Circle().fill(Theme.Palette.blue50))
            }
            .disabled(isSending)

            TextField("Scrivi un messaggio...", text: $draft, axis: .vertical)
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
                    .background(Circle().fill(Theme.Palette.blue500))
            }
            .disabled(draft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || isSending)
        }
        .padding(.horizontal, Theme.Spacing.lg)
        .padding(.vertical, Theme.Spacing.md)
        .background(Theme.Palette.surface.ignoresSafeArea(edges: .bottom))
        .safeAreaPadding(.bottom, 2)
    }

    private func load() async {
        do {
            if let venueId {
                thread = try await API.venueChatThread(venueId: venueId)
            } else if let chatId {
                thread = try await API.groupChatThread(chatId: chatId)
            }
            errorMessage = nil
            NotificationCenter.default.post(name: .cloudyBadgesShouldRefresh, object: nil)
        } catch {
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
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
        let body = draft.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !body.isEmpty else { return }
        await sendBody(body)
        draft = ""
    }

    private func sendPhoto(_ item: PhotosPickerItem?) async {
        guard let item else { return }
        do {
            guard let data = try await item.loadTransferable(type: Data.self) else { return }
            await uploadAndSend(data: data, fileName: "foto-\(UUID().uuidString).jpg", mimeType: "image/jpeg")
            photoItem = nil
        } catch {
            Haptics.error()
            errorMessage = "Impossibile leggere la foto."
        }
    }

    private func sendImportedFile(_ result: Result<[URL], Error>) async {
        do {
            guard let url = try result.get().first else { return }
            let didAccess = url.startAccessingSecurityScopedResource()
            defer {
                if didAccess { url.stopAccessingSecurityScopedResource() }
            }
            let data = try Data(contentsOf: url)
            await uploadAndSend(data: data, fileName: url.lastPathComponent, mimeType: mimeType(for: url))
        } catch {
            Haptics.error()
            errorMessage = "Impossibile allegare il file."
        }
    }

    private func uploadAndSend(data: Data, fileName: String, mimeType: String) async {
        isSending = true
        defer { isSending = false }
        do {
            let url = try await API.uploadChatFile(data: data, fileName: fileName, mimeType: mimeType)
            let marker = mimeType.hasPrefix("image/") ? "[image]" : "[file]"
            await sendBody("\(marker) \(fileName)\n\(url)")
        } catch {
            Haptics.error()
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func sendBody(_ body: String) async {
        isSending = true
        defer { isSending = false }
        do {
            if let venueId {
                _ = try await API.sendVenueChatMessage(venueId: venueId, body: body)
            } else if let chatId {
                _ = try await API.sendGroupChatMessage(chatId: chatId, body: body)
            }
            Haptics.tap()
            await load()
        } catch {
            Haptics.error()
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func timeOnly(_ date: Date) -> String {
        let formatter = DateFormatter()
        formatter.dateFormat = "HH:mm"
        return formatter.string(from: date)
    }

    private func mimeType(for url: URL) -> String {
        if let type = UTType(filenameExtension: url.pathExtension),
           let mime = type.preferredMIMEType {
            return mime
        }
        return "application/octet-stream"
    }
}
