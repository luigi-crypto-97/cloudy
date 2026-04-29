//
//  ChatRoomView.swift
//  Cloudy — Conversazione 1:1
//
//  Endpoint:
//   - GET  /api/messages/threads/{otherUserId}
//   - POST /api/messages/threads/{otherUserId}
//

import SwiftUI
import PhotosUI
import UniformTypeIdentifiers

struct ChatRoomView: View {
    let otherUserId: UUID
    let peerName: String

    @Environment(AppRouter.self) private var router

    @State private var thread: DirectMessageThread?
    @State private var draft: String = ""
    @State private var isSending = false
    @State private var errorMessage: String?
    @State private var photoItem: PhotosPickerItem?
    @State private var showsFileImporter = false
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
        .safeAreaPadding(.bottom, 2)
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
            NotificationCenter.default.post(name: .cloudyBadgesShouldRefresh, object: nil)
            errorMessage = nil
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
            let body = "📎 \(fileName)\n\(url)"
            _ = try await API.sendDirectMessage(otherUserId: otherUserId, body: body)
            Haptics.success()
            await load()
        } catch {
            Haptics.error()
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func mimeType(for url: URL) -> String {
        if let type = UTType(filenameExtension: url.pathExtension),
           let mime = type.preferredMIMEType {
            return mime
        }
        return "application/octet-stream"
    }
}
