//
//  CreateStoryView.swift
//  Cloudy — Crea una nuova storia (24h)
//

import SwiftUI
import PhotosUI
import UIKit

struct CreateStoryView: View {
    var venue: VenueMarker? = nil
    var onCreated: () -> Void = {}

    @Environment(\.dismiss) private var dismiss
    @State private var mediaUrl: String = ""
    @State private var title: String = ""
    @State private var caption: String = ""
    @State private var photoItem: PhotosPickerItem?
    @State private var selectedImageData: Data?
    @State private var showsCamera: Bool = false
    @State private var isSending: Bool = false
    @State private var error: String?

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: Theme.Spacing.lg) {
                    header

                    if let venue {
                        CloudyPill(text: venue.name, icon: "mappin.circle.fill", tone: .honey)
                    }

                    SectionCard {
                        VStack(alignment: .leading, spacing: Theme.Spacing.sm) {
                            Text("Media")
                                .font(Theme.Font.title(15, weight: .bold))
                            Text("Scegli una foto dalla galleria oppure incolla un URL pubblico.")
                                .font(Theme.Font.caption(11))
                                .foregroundStyle(Theme.Palette.inkSoft)
                            if UIImagePickerController.isSourceTypeAvailable(.camera) {
                                Button {
                                    showsCamera = true
                                } label: {
                                    HStack {
                                        Image(systemName: "camera.fill")
                                        Text("Scatta adesso")
                                        Spacer()
                                    }
                                    .padding(12)
                                    .background(
                                        RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                                            .fill(Theme.Palette.surfaceAlt)
                                    )
                                }
                                .buttonStyle(.plain)
                            }
                            PhotosPicker(selection: $photoItem, matching: .images) {
                                HStack {
                                    Image(systemName: "photo.on.rectangle.angled")
                                    Text(selectedImageData == nil ? "Scegli dalla galleria" : "Foto selezionata")
                                    Spacer()
                                    Image(systemName: "chevron.right")
                                }
                                .padding(12)
                                .background(
                                    RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                                        .fill(Theme.Palette.honeySoft)
                                )
                            }
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
                            Text("Titolo")
                                .font(Theme.Font.title(15, weight: .bold))
                            TextField("Es. Serata ai Navigli", text: $title)
                                .textInputAutocapitalization(.sentences)
                                .padding(12)
                                .background(
                                    RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                                        .fill(Theme.Palette.surfaceAlt)
                                )
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

                    if let selectedImageData, let image = UIImage(data: selectedImageData) {
                        Image(uiImage: image)
                            .resizable()
                            .scaledToFill()
                            .frame(height: 220)
                            .frame(maxWidth: .infinity)
                            .clipShape(RoundedRectangle(cornerRadius: Theme.Radius.lg, style: .continuous))
                    } else if !mediaUrl.isEmpty, let url = URL(string: mediaUrl) {
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
                    .disabled(isSending || (mediaUrl.trimmingCharacters(in: .whitespaces).isEmpty && selectedImageData == nil))
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
        .onChange(of: photoItem) { _, newValue in
            Task { await loadPhoto(newValue) }
        }
        .sheet(isPresented: $showsCamera) {
            CameraCaptureView { image in
                selectedImageData = image.jpegData(compressionQuality: 0.82)
                mediaUrl = ""
            }
            .ignoresSafeArea()
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
        let typedUrl = mediaUrl.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !typedUrl.isEmpty || selectedImageData != nil else { return }
        isSending = true
        error = nil
        defer { isSending = false }
        do {
            let finalMediaUrl: String
            if let selectedImageData {
                finalMediaUrl = try await API.uploadStoryMedia(
                    data: selectedImageData,
                    fileName: "story-\(UUID().uuidString).jpg",
                    mimeType: "image/jpeg"
                )
            } else {
                finalMediaUrl = typedUrl
            }
            _ = try await API.createStory(
                mediaUrl: finalMediaUrl,
                caption: storyCaption,
                venueId: venue?.venueId
            )
            Haptics.tap()
            onCreated()
            dismiss()
        } catch {
            Haptics.error()
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func loadPhoto(_ item: PhotosPickerItem?) async {
        guard let item else { return }
        do {
            selectedImageData = try await item.loadTransferable(type: Data.self)
            if selectedImageData != nil {
                mediaUrl = ""
            }
        } catch {
            self.error = "Impossibile leggere la foto selezionata."
        }
    }

    private var storyCaption: String? {
        let cleanTitle = title.trimmingCharacters(in: .whitespacesAndNewlines)
        let cleanCaption = caption.trimmingCharacters(in: .whitespacesAndNewlines)
        switch (cleanTitle.isEmpty, cleanCaption.isEmpty) {
        case (true, true): return nil
        case (false, true): return cleanTitle
        case (true, false): return cleanCaption
        case (false, false): return "\(cleanTitle)\n\n\(cleanCaption)"
        }
    }
}

private struct CameraCaptureView: UIViewControllerRepresentable {
    var onCapture: (UIImage) -> Void
    @Environment(\.dismiss) private var dismiss

    func makeUIViewController(context: Context) -> UIImagePickerController {
        let picker = UIImagePickerController()
        picker.sourceType = .camera
        picker.cameraCaptureMode = .photo
        picker.delegate = context.coordinator
        return picker
    }

    func updateUIViewController(_ uiViewController: UIImagePickerController, context: Context) {}

    func makeCoordinator() -> Coordinator {
        Coordinator(onCapture: onCapture, dismiss: dismiss)
    }

    final class Coordinator: NSObject, UIImagePickerControllerDelegate, UINavigationControllerDelegate {
        private let onCapture: (UIImage) -> Void
        private let dismiss: DismissAction

        init(onCapture: @escaping (UIImage) -> Void, dismiss: DismissAction) {
            self.onCapture = onCapture
            self.dismiss = dismiss
        }

        func imagePickerController(_ picker: UIImagePickerController, didFinishPickingMediaWithInfo info: [UIImagePickerController.InfoKey: Any]) {
            if let image = info[.originalImage] as? UIImage {
                onCapture(image)
            }
            dismiss()
        }

        func imagePickerControllerDidCancel(_ picker: UIImagePickerController) {
            dismiss()
        }
    }
}
