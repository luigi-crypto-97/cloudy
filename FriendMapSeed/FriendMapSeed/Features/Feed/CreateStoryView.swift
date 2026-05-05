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
import AVKit
import UniformTypeIdentifiers

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
    @State private var selectedVideoData: Data?
    @State private var selectedVideoPreviewURL: URL?
    @State private var activeCameraMode: StoryCameraMode?
    @State private var isSending: Bool = false
    @State private var isPreparingVideo: Bool = false
    @State private var error: String?

    private enum Field: Hashable {
        case url, title, caption
    }

    private var hasMedia: Bool {
        selectedImageData != nil ||
        selectedVideoData != nil ||
        selectedVideoPreviewURL != nil ||
        (URL(string: mediaUrl.trimmingCharacters(in: .whitespaces))?.scheme?.hasPrefix("http") == true)
    }

    private var isSelectedVideo: Bool {
        selectedVideoData != nil || mediaUrl.isCloudyVideoURL
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
        .sheet(item: $activeCameraMode) { mode in
            CameraCaptureView(mode: mode) { result in
                switch result {
                case .photo(let image):
                    selectedImageData = image.cloudyStoryJPEGData()
                    selectedVideoData = nil
                    selectedVideoPreviewURL = nil
                case .video(let url):
                    selectedVideoPreviewURL = url
                    selectedVideoData = nil
                    selectedImageData = nil
                    Task { await prepareVideoData(from: url) }
                }
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
                    TextField("URL foto o video...", text: $mediaUrl)
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
                        activeCameraMode = .photo
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

                    Button {
                        activeCameraMode = .video
                    } label: {
                        HStack {
                            Image(systemName: "video.fill")
                            Text("Registra video")
                            Spacer()
                        }
                        .padding(14)
                        .background(Color.white.opacity(0.12))
                        .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
                        .foregroundStyle(.white)
                    }
                    .buttonStyle(.plain)
                }

                PhotosPicker(selection: $photoItem, matching: .any(of: [.images, .videos])) {
                    HStack {
                        Image(systemName: "photo.on.rectangle.angled")
                        Text("Scegli foto o video")
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
                        .scaledToFill()
                        .frame(width: geo.size.width, height: geo.size.height)
                        .clipped()
                } else if let selectedVideoPreviewURL {
                    VideoPlayer(player: AVPlayer(url: selectedVideoPreviewURL))
                        .frame(width: geo.size.width, height: geo.size.height)
                        .clipped()
                } else if let url = URL(string: mediaUrl.trimmingCharacters(in: .whitespaces)) {
                    if url.isCloudyVideoURL {
                        VideoPlayer(player: AVPlayer(url: url))
                            .frame(width: geo.size.width, height: geo.size.height)
                            .clipped()
                    } else {
                        AsyncImage(url: url) { phase in
                        switch phase {
                        case .success(let img):
                            img
                                .resizable()
                                .scaledToFill()
                                .frame(width: geo.size.width, height: geo.size.height)
                                .clipped()
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
        }
        .ignoresSafeArea()
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
                            if isSending || isPreparingVideo {
                                ProgressView()
                                    .tint(Theme.Palette.ink)
                            } else {
                                Image(systemName: "paperplane.fill")
                                    .font(.system(size: 20, weight: .bold))
                                    .foregroundStyle(Theme.Palette.ink)
                            }
                        }
                    }
                    .disabled(isSending || isPreparingVideo)
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
        guard !typedUrl.isEmpty || selectedImageData != nil || selectedVideoData != nil else {
            if selectedVideoPreviewURL != nil {
                error = "Sto preparando il video. Riprova tra un istante."
            }
            return
        }
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
            } else if let selectedVideoData {
                finalMediaUrl = try await API.uploadStoryMedia(
                    data: selectedVideoData,
                    fileName: "story-\(UUID().uuidString).mp4",
                    mimeType: "video/mp4"
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
            if item.supportedContentTypes.contains(where: { $0.conforms(to: .movie) || $0.conforms(to: .video) }) {
                if let data = try await item.loadTransferable(type: Data.self) {
                    selectedVideoData = data
                    selectedVideoPreviewURL = writeTemporaryVideo(data: data)
                    selectedImageData = nil
                    mediaUrl = ""
                    return
                }
            }
            if let data = try await item.loadTransferable(type: Data.self),
               let image = UIImage(data: data) {
                selectedImageData = image.cloudyStoryJPEGData()
                selectedVideoData = nil
                selectedVideoPreviewURL = nil
                mediaUrl = ""
            }
        } catch {
            self.error = "Impossibile leggere il media selezionato."
        }
    }

    private func writeTemporaryVideo(data: Data) -> URL? {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("cloudy-story-\(UUID().uuidString)")
            .appendingPathExtension("mp4")
        do {
            try data.write(to: url, options: .atomic)
            return url
        } catch {
            return nil
        }
    }

    private func prepareVideoData(from url: URL) async {
        isPreparingVideo = true
        defer { isPreparingVideo = false }
        let data = await Task.detached(priority: .userInitiated) {
            try? Data(contentsOf: url)
        }.value
        if let data {
            selectedVideoData = data
        } else {
            error = "Impossibile leggere il video registrato."
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

// MARK: - Story media normalization

private extension UIImage {
    /// Stories devono nascere gia' nel formato viewer: verticale 9:16,
    /// 1080x1920, crop centrale. Cosi' camera e galleria hanno lo stesso
    /// comportamento full-screen e non mandiamo immagini enormi al server.
    func cloudyStoryJPEGData() -> Data? {
        let targetSize = CGSize(width: 1080, height: 1920)
        let targetRatio = targetSize.width / targetSize.height
        let sourceRatio = size.width / size.height

        let cropRect: CGRect
        if sourceRatio > targetRatio {
            let cropWidth = size.height * targetRatio
            cropRect = CGRect(
                x: (size.width - cropWidth) / 2,
                y: 0,
                width: cropWidth,
                height: size.height
            )
        } else {
            let cropHeight = size.width / targetRatio
            cropRect = CGRect(
                x: 0,
                y: (size.height - cropHeight) / 2,
                width: size.width,
                height: cropHeight
            )
        }

        let rendererFormat = UIGraphicsImageRendererFormat()
        rendererFormat.scale = 1
        rendererFormat.opaque = true
        let renderer = UIGraphicsImageRenderer(size: targetSize, format: rendererFormat)
        return renderer.jpegData(withCompressionQuality: 0.88) { context in
            UIColor.black.setFill()
            context.fill(CGRect(origin: .zero, size: targetSize))
            let scale = targetSize.width / cropRect.width
            let drawRect = CGRect(
                x: -cropRect.minX * scale,
                y: -cropRect.minY * scale,
                width: size.width * scale,
                height: size.height * scale
            )
            draw(in: drawRect, blendMode: .normal, alpha: 1)
        }
    }
}

// MARK: - Camera capture

private enum StoryCameraMode: String, Identifiable {
    case photo
    case video

    var id: String { rawValue }
}

private enum StoryCameraCaptureResult {
    case photo(UIImage)
    case video(URL)
}

private struct CameraCaptureView: UIViewControllerRepresentable {
    let mode: StoryCameraMode
    var onCapture: (StoryCameraCaptureResult) -> Void
    @Environment(\.dismiss) private var dismiss

    func makeUIViewController(context: Context) -> UIImagePickerController {
        let picker = UIImagePickerController()
        let sourceType: UIImagePickerController.SourceType = UIImagePickerController.isSourceTypeAvailable(.camera)
            ? .camera
            : .photoLibrary
        picker.sourceType = sourceType

        let availableMediaTypes = UIImagePickerController.availableMediaTypes(for: sourceType) ?? []
        let requestedType = mode == .video ? UTType.movie.identifier : UTType.image.identifier
        let resolvedType = availableMediaTypes.contains(requestedType)
            ? requestedType
            : (availableMediaTypes.first ?? UTType.image.identifier)
        picker.mediaTypes = [resolvedType]

        if sourceType == .camera {
            let device = Self.preferredCameraDevice()
            if let device {
                picker.cameraDevice = device
            }

            let requestedCaptureMode: UIImagePickerController.CameraCaptureMode = resolvedType == UTType.movie.identifier ? .video : .photo
            let supportedModes = device.flatMap { UIImagePickerController.availableCaptureModes(for: $0) } ?? []
            let supportsRequestedMode = supportedModes.contains { $0.intValue == requestedCaptureMode.rawValue }
            picker.cameraCaptureMode = supportsRequestedMode ? requestedCaptureMode : .photo
        }

        picker.videoQuality = .typeMedium
        picker.videoMaximumDuration = 15
        picker.delegate = context.coordinator
        return picker
    }

    func updateUIViewController(_ uiViewController: UIImagePickerController, context: Context) {}

    func makeCoordinator() -> Coordinator {
        Coordinator(onCapture: onCapture, dismiss: dismiss)
    }

    private static func preferredCameraDevice() -> UIImagePickerController.CameraDevice? {
        if UIImagePickerController.isCameraDeviceAvailable(.rear) {
            return .rear
        }
        if UIImagePickerController.isCameraDeviceAvailable(.front) {
            return .front
        }
        return nil
    }

    final class Coordinator: NSObject, UIImagePickerControllerDelegate, UINavigationControllerDelegate {
        private let onCapture: (StoryCameraCaptureResult) -> Void
        private let dismiss: DismissAction

        init(onCapture: @escaping (StoryCameraCaptureResult) -> Void, dismiss: DismissAction) {
            self.onCapture = onCapture
            self.dismiss = dismiss
        }

        func imagePickerController(_ picker: UIImagePickerController, didFinishPickingMediaWithInfo info: [UIImagePickerController.InfoKey: Any]) {
            if let image = info[.originalImage] as? UIImage {
                onCapture(.photo(image))
            } else if let mediaURL = info[.mediaURL] as? URL {
                onCapture(.video(Self.copyVideoToStableTemporaryFile(mediaURL) ?? mediaURL))
            }
            dismiss()
        }

        func imagePickerControllerDidCancel(_ picker: UIImagePickerController) {
            dismiss()
        }

        private static func copyVideoToStableTemporaryFile(_ sourceURL: URL) -> URL? {
            let extensionPart = sourceURL.pathExtension.isEmpty ? "mov" : sourceURL.pathExtension
            let destinationURL = FileManager.default.temporaryDirectory
                .appendingPathComponent("cloudy-story-\(UUID().uuidString)")
                .appendingPathExtension(extensionPart)

            let scoped = sourceURL.startAccessingSecurityScopedResource()
            defer {
                if scoped {
                    sourceURL.stopAccessingSecurityScopedResource()
                }
            }

            do {
                try FileManager.default.copyItem(at: sourceURL, to: destinationURL)
                return destinationURL
            } catch {
                return nil
            }
        }
    }
}

extension URL {
    var isCloudyVideoURL: Bool {
        pathExtension.lowercased().isCloudyVideoExtension
    }
}

extension String {
    var isCloudyVideoURL: Bool {
        guard let url = URL(string: trimmingCharacters(in: .whitespacesAndNewlines)) else { return false }
        return url.isCloudyVideoURL
    }

    fileprivate var isCloudyVideoExtension: Bool {
        ["mp4", "mov", "m4v", "webm"].contains(lowercased())
    }
}
