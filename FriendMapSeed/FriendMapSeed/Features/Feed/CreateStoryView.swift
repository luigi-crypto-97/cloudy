//
//  CreateStoryView.swift
//  Cloudy — Crea una nuova storia (24h)
//
//  UX premium full-screen: preview immersiva, input minimale,
//  pubblica con un tap. Layout ispirato a Instagram Stories creation.
//  Mantiene camera, galleria PhotosPicker, upload S3 e venue tagging.
//

import SwiftUI
import PhotosUI
import UIKit

struct CreateStoryView: View {
    var venue: VenueMarker? = nil
    var onCreated: () -> Void = {}

    @Environment(\.dismiss) private var dismiss
    @FocusState private var focusedField: Field?
    @State private var mediaUrl: String = ""
    @State private var title: String = ""
    @State private var caption: String = ""
    @State private var photoItem: PhotosPickerItem?
    @State private var selectedImageData: Data?
    @State private var showsCamera: Bool = false
    @State private var isSending: Bool = false
    @State private var error: String?

    private enum Field: Hashable {
        case url, title, caption
    }

    private var hasMedia: Bool {
        selectedImageData != nil || (URL(string: mediaUrl.trimmingCharacters(in: .whitespaces))?.scheme?.hasPrefix("http") == true)
    }

    var body: some View {
        ZStack {
            Color.black.ignoresSafeArea()

            VStack(spacing: 0) {
                topBar

                Spacer()

                if hasMedia {
                    previewSection
                } else {
                    emptyState
                }

                Spacer()

                bottomControls
            }
        }
        .ignoresSafeArea(.container, edges: .bottom)
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

            if let venue {
                CloudyPill(text: venue.name, icon: "mappin.circle.fill", tone: .honey)
            }

            if hasMedia {
                HStack(spacing: 6) {
                    Image(systemName: "link")
                        .font(.system(size: 12))
                        .foregroundStyle(.white.opacity(0.6))
                    TextField("URL immagine...", text: $mediaUrl)
                        .font(Theme.Font.caption(13))
                        .foregroundStyle(.white)
                        .textFieldStyle(.plain)
                        .frame(maxWidth: 160)
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
        VStack(spacing: 24) {
            ZStack {
                Circle()
                    .fill(Color.white.opacity(0.08))
                    .frame(width: 120, height: 120)
                Image(systemName: "photo.on.rectangle.angled")
                    .font(.system(size: 48, weight: .light))
                    .foregroundStyle(.white.opacity(0.8))
            }

            Text("Aggiungi un media")
                .font(Theme.Font.title(20, weight: .bold))
                .foregroundStyle(.white)

            Text("Scatta una foto, scegli dalla galleria o incolla un link.")
                .font(Theme.Font.body(14))
                .foregroundStyle(.white.opacity(0.6))
                .multilineTextAlignment(.center)
                .padding(.horizontal, 40)

            VStack(spacing: 12) {
                if UIImagePickerController.isSourceTypeAvailable(.camera) {
                    Button {
                        showsCamera = true
                    } label: {
                        HStack {
                            Image(systemName: "camera.fill")
                            Text("Scatta adesso")
                            Spacer()
                        }
                        .padding(14)
                        .background(Color.white.opacity(0.12))
                        .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
                        .foregroundStyle(.white)
                    }
                    .buttonStyle(.plain)
                }

                PhotosPicker(selection: $photoItem, matching: .images) {
                    HStack {
                        Image(systemName: "photo.on.rectangle.angled")
                        Text("Scegli dalla galleria")
                        Spacer()
                    }
                    .padding(14)
                    .background(Theme.Palette.honeySoft.opacity(0.9))
                    .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
                    .foregroundStyle(Theme.Palette.ink)
                }

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
            }
            .padding(.horizontal, 32)
            .padding(.top, 8)
        }
    }

    // MARK: - Preview section

    private var previewSection: some View {
        GeometryReader { geo in
            ZStack {
                if let selectedImageData, let image = UIImage(data: selectedImageData) {
                    Image(uiImage: image)
                        .resizable()
                        .scaledToFit
                        .frame(maxWidth: geo.size.width, maxHeight: geo.size.height)
                        .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
                } else if let url = URL(string: mediaUrl.trimmingCharacters(in: .whitespaces)) {
                    AsyncImage(url: url) { phase in
                        switch phase {
                        case .success(let img):
                            img
                                .resizable()
                                .scaledToFit
                                .frame(maxWidth: geo.size.width, maxHeight: geo.size.height)
                                .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
                        case .failure:
                            errorPlaceholder
                        default:
                            ProgressView()
                                .tint(.white)
                                .frame(maxWidth: .infinity, maxHeight: .infinity)
                        }
                    }
                }
            }
        }
        .padding(.horizontal, 12)
    }

    private var errorPlaceholder: some View {
        VStack(spacing: 12) {
            Image(systemName: "exclamationmark.triangle")
                .font(.system(size: 40))
                .foregroundStyle(.white.opacity(0.6))
            Text("Impossibile caricare l'immagine")
                .font(Theme.Font.body(15))
                .foregroundStyle(.white.opacity(0.7))
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    // MARK: - Bottom controls

    private var bottomControls: some View {
        VStack(spacing: 16) {
            if hasMedia {
                HStack(spacing: 10) {
                    VStack(spacing: 8) {
                        TextField("Titolo...", text: $title)
                            .font(Theme.Font.body(14, weight: .semibold))
                            .foregroundStyle(.white)
                            .textFieldStyle(.plain)
                            .textInputAutocapitalization(.sentences)
                            .focused($focusedField, equals: .title)

                        TextField("Didascalia...", text: $caption, axis: .vertical)
                            .font(Theme.Font.body(14))
                            .foregroundStyle(.white.opacity(0.85))
                            .textFieldStyle(.plain)
                            .lineLimit(2)
                            .focused($focusedField, equals: .caption)
                    }
                    .padding(.horizontal, 16)
                    .padding(.vertical, 12)

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

// MARK: - Camera capture

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
