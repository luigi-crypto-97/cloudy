//
//  CreateStoryView.swift
//  Cloudy — Crea una nuova storia (24h)
//

import SwiftUI

struct CreateStoryView: View {
    var onCreated: () -> Void = {}

    @Environment(\.dismiss) private var dismiss
    @State private var mediaUrl: String = ""
    @State private var caption: String = ""
    @State private var isSending: Bool = false
    @State private var error: String?

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: Theme.Spacing.lg) {
                    header

                    SectionCard {
                        VStack(alignment: .leading, spacing: Theme.Spacing.sm) {
                            Text("Media (URL)")
                                .font(Theme.Font.title(15, weight: .bold))
                            Text("Incolla il link a un'immagine o video. Più avanti potrai caricare direttamente dalla galleria.")
                                .font(Theme.Font.caption(11))
                                .foregroundStyle(Theme.Palette.inkSoft)
                            TextField("https://...", text: $mediaUrl)
                                .textInputAutocapitalization(.never)
                                .autocorrectionDisabled()
                                .keyboardType(.URL)
                                .padding(12)
                                .background(
                                    RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                                        .fill(Theme.Palette.surfaceAlt)
                                )
                        }
                    }

                    SectionCard {
                        VStack(alignment: .leading, spacing: Theme.Spacing.sm) {
                            Text("Caption (opzionale)")
                                .font(Theme.Font.title(15, weight: .bold))
                            TextEditor(text: $caption)
                                .frame(minHeight: 90)
                                .font(Theme.Font.body(15))
                                .scrollContentBackground(.hidden)
                                .padding(8)
                                .background(
                                    RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                                        .fill(Theme.Palette.surfaceAlt)
                                )
                        }
                    }

                    if !mediaUrl.isEmpty, let url = URL(string: mediaUrl) {
                        AsyncImage(url: url) { phase in
                            switch phase {
                            case .success(let img):
                                img.resizable().scaledToFill()
                            case .failure:
                                Color.clear
                            default:
                                ProgressView()
                            }
                        }
                        .frame(height: 220)
                        .frame(maxWidth: .infinity)
                        .clipShape(RoundedRectangle(cornerRadius: Theme.Radius.lg, style: .continuous))
                    }

                    if let err = error {
                        Text(err)
                            .font(Theme.Font.caption(12))
                            .foregroundStyle(Theme.Palette.densityHigh)
                    }

                    Button {
                        Task { await send() }
                    } label: {
                        HStack {
                            if isSending { ProgressView().tint(.white) } else { Image(systemName: "sparkles") }
                            Text(isSending ? "Pubblicazione…" : "Pubblica storia")
                        }
                        .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.honey)
                    .disabled(isSending || mediaUrl.trimmingCharacters(in: .whitespaces).isEmpty)
                }
                .padding(Theme.Spacing.lg)
            }
            .background(Theme.Palette.surfaceAlt.ignoresSafeArea())
            .navigationTitle("Nuova storia")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Annulla") { dismiss() }
                }
            }
        }
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("Condividi un momento")
                .font(Theme.Font.display(24))
            Text("Le storie scompaiono dopo 24 ore.")
                .font(Theme.Font.body(13))
                .foregroundStyle(Theme.Palette.inkSoft)
        }
    }

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
