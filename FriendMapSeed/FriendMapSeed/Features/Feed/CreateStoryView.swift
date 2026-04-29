//
//  CreateStoryView.swift
//  Cloudy — Crea una nuova storia (24h)
//
//  UX premium full-screen: preview immersiva, input minimale,
//  pubblica con un tap. Layout ispirato a Instagram Stories creation.
//

import SwiftUI

struct CreateStoryView: View {
    var onCreated: () -> Void = {}

    @Environment(\.dismiss) private var dismiss
    @FocusState private var focusedField: Field?
    @State private var mediaUrl: String = ""
    @State private var caption: String = ""
    @State private var isSending: Bool = false
    @State private var error: String?

    private enum Field: Hashable {
        case url, caption
    }

    private var hasValidUrl: Bool {
        guard let url = URL(string: mediaUrl.trimmingCharacters(in: .whitespaces)),
              url.scheme?.hasPrefix("http") == true else {
            return false
        }
        return true
    }

    var body: some View {
        ZStack {
            background

            VStack(spacing: 0) {
                topBar

                Spacer()

                if hasValidUrl {
                    previewSection
                } else {
                    emptyState
                }

                Spacer()

                bottomControls
            }
        }
        .ignoresSafeArea(.container, edges: .bottom)
    }

    // MARK: - Background

    private var background: some View {
        Color.black
            .ignoresSafeArea()
            .overlay(
                LinearGradient(
                    colors: [
                        Color(red: 0.05, green: 0.06, blue: 0.10),
                        Color(red: 0.10, green: 0.12, blue: 0.18)
                    ],
                    startPoint: .top,
                    endPoint: .bottom
                )
                .opacity(hasValidUrl ? 0 : 1)
            )
    }

    // MARK: - Top bar

    private var topBar: some View {
        HStack {
            Button {
                dismiss()
            } label: {
                Image(systemName: "xmark")
                    .font(.system(size: 16, weight: .bold))
                    .foregroundStyle(.white)
                    .padding(10)
                    .background(.ultraThinMaterial)
                    .clipShape(Circle())
            }

            Spacer()

            if hasValidUrl {
                HStack(spacing: 6) {
                    Image(systemName: "link")
                        .font(.system(size: 12))
                        .foregroundStyle(.white.opacity(0.6))
                    TextField("URL immagine...", text: $mediaUrl)
                        .font(Theme.Font.caption(13))
                        .foregroundStyle(.white)
                        .textFieldStyle(.plain)
                        .frame(maxWidth: 200)
                }
                .padding(.horizontal, 12)
                .padding(.vertical, 8)
                .background(Color.white.opacity(0.1))
                .clipShape(Capsule())
            }
        }
        .padding(.horizontal, 16)
        .padding(.top, 20)
    }

    // MARK: - Empty state

    private var emptyState: some View {
        VStack(spacing: 20) {
            ZStack {
                Circle()
                    .fill(Color.white.opacity(0.08))
                    .frame(width: 120, height: 120)
                Image(systemName: "photo.on.rectangle.angled")
                    .font(.system(size: 48, weight: .light))
                    .foregroundStyle(.white.opacity(0.8))
            }

            Text("Aggiungi un link")
                .font(Theme.Font.title(20, weight: .bold))
                .foregroundStyle(.white)

            Text("Incolla il link a un'immagine o video. Più avanti potrai caricare direttamente dalla galleria.")
                .font(Theme.Font.body(14))
                .foregroundStyle(.white.opacity(0.6))
                .multilineTextAlignment(.center)
                .padding(.horizontal, 40)

            HStack(spacing: 8) {
                Image(systemName: "link")
                    .font(.system(size: 14))
                    .foregroundStyle(.white.opacity(0.5))
                TextField("https://...", text: $mediaUrl)
                    .font(Theme.Font.body(15))
                    .foregroundStyle(.white)
                    .textFieldStyle(.plain)
                    .keyboardType(.URL)
                    .textInputAutocapitalization(.never)
                    .autocorrectionDisabled()
                    .focused($focusedField, equals: .url)
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 14)
            .background(Color.white.opacity(0.1))
            .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
            .padding(.horizontal, 32)
            .padding(.top, 8)
        }
    }

    // MARK: - Preview section

    private var previewSection: some View {
        GeometryReader { geo in
            if let url = URL(string: mediaUrl.trimmingCharacters(in: .whitespaces)) {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .success(let image):
                        image
                            .resizable()
                            .scaledToFit
                            .frame(maxWidth: geo.size.width, maxHeight: geo.size.height)
                            .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
                    case .failure:
                        VStack(spacing: 12) {
                            Image(systemName: "exclamationmark.triangle")
                                .font(.system(size: 40))
                                .foregroundStyle(.white.opacity(0.6))
                            Text("Impossibile caricare l'immagine")
                                .font(Theme.Font.body(15))
                                .foregroundStyle(.white.opacity(0.7))
                        }
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                    default:
                        ProgressView()
                            .tint(.white)
                            .frame(maxWidth: .infinity, maxHeight: .infinity)
                    }
                }
            }
        }
        .padding(.horizontal, 12)
    }

    // MARK: - Bottom controls

    private var bottomControls: some View {
        VStack(spacing: 16) {
            if hasValidUrl {
                // Caption
                HStack(spacing: 10) {
                    TextField("Aggiungi una didascalia...", text: $caption, axis: .vertical)
                        .font(Theme.Font.body(15))
                        .foregroundStyle(.white)
                        .textFieldStyle(.plain)
                        .lineLimit(2)
                        .focused($focusedField, equals: .caption)

                    Button {
                        Task { await send() }
                    } label: {
                        ZStack {
                            Circle()
                                .fill(Theme.Gradients.honeyCTA)
                                .frame(width: 52, height: 52)
                            if isSending {
                                ProgressView()
                                    .tint(Theme.Palette.ink)
                            } else {
                                Image(systemName: "paperplane.fill")
                                    .font(.system(size: 20, weight: .bold))
                                    .foregroundStyle(Theme.Palette.ink)
                            }
                        }
                    }
                    .disabled(isSending)
                }
                .padding(.horizontal, 16)
                .padding(.vertical, 12)
                .background(Color.white.opacity(0.1))
                .clipShape(RoundedRectangle(cornerRadius: 24, style: .continuous))
            }

            if let err = error {
                Text(err)
                    .font(Theme.Font.caption(12))
                    .foregroundStyle(Theme.Palette.densityHigh)
                    .multilineTextAlignment(.center)
                    .padding(.horizontal, 20)
            }
        }
        .padding(.horizontal, 12)
        .padding(.bottom, 32)
        .background(
            LinearGradient(
                colors: [.black.opacity(0.0), .black.opacity(0.5)],
                startPoint: .top,
                endPoint: .bottom
            )
            .ignoresSafeArea(edges: .bottom)
        )
    }

    // MARK: - Send

    private func send() async {
        let trimmed = mediaUrl.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }
        isSending = true
        error = nil
        defer { isSending = false }
        do {
            _ = try await API.createStory(
                mediaUrl: trimmed,
                caption: caption.isEmpty ? nil : caption
            )
            Haptics.tap()
            onCreated()
            dismiss()
        } catch {
            Haptics.error()
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }
}
