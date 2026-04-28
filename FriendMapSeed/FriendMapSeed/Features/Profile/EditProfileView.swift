//
//  EditProfileView.swift
//  Cloudy — Modifica del profilo dell'utente corrente
//
//  Endpoint: GET /api/users/me/profile, PUT /api/users/me/profile
//

import SwiftUI
import PhotosUI
import UIKit

struct EditProfileView: View {
    var onSaved: (() -> Void)? = nil

    @Environment(\.dismiss) private var dismiss

    @State private var profile: EditableUserProfile?
    @State private var displayName: String = ""
    @State private var bio: String = ""
    @State private var birthYearText: String = ""
    @State private var gender: String = "unspecified"
    @State private var avatarUrl: String = ""
    @State private var avatarItem: PhotosPickerItem?
    @State private var selectedAvatarData: Data?
    @State private var showsCamera = false
    @State private var interests: [String] = []
    @State private var newInterest: String = ""
    @State private var interestDeckIndex: Int = 0
    @State private var isLoading = false
    @State private var isSaving = false
    @State private var errorMessage: String?

    private let genders: [(label: String, value: String)] = [
        ("Non specificato", "unspecified"),
        ("Donna", "female"),
        ("Uomo", "male"),
        ("Non binario", "non_binary"),
        ("Altro", "other")
    ]

    private let interestSuggestions = [
        "aperitivo", "cocktail", "ristoranti", "sushi", "pizza", "pub",
        "club", "musica live", "casino", "fitness", "running", "cinema",
        "arte", "tech", "startup", "viaggi", "coffee", "wine bar"
    ]

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(spacing: Theme.Spacing.lg) {
                    if isLoading {
                        ProgressView().padding(.top, 80)
                    } else if let _ = profile {
                        avatarSection
                        identitySection
                        aboutSection
                        interestsSection
                        if let errorMessage {
                            Text(errorMessage)
                                .font(Theme.Font.caption(12))
                                .foregroundStyle(Theme.Palette.densityHigh)
                                .frame(maxWidth: .infinity, alignment: .leading)
                        }
                    } else if let errorMessage {
                        CloudyEmptyState(
                            icon: "exclamationmark.triangle.fill",
                            title: "Errore",
                            message: errorMessage
                        )
                        .padding(.top, 60)
                    }
                }
                .padding(Theme.Spacing.lg)
                .padding(.bottom, 130)
            }
            .background(Theme.Palette.surfaceAlt.ignoresSafeArea())
            .navigationTitle("Modifica profilo")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Annulla") { dismiss() }
                }
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        Task { await save() }
                    } label: {
                        if isSaving {
                            ProgressView().scaleEffect(0.8)
                        } else {
                            Text("Salva").font(Theme.Font.body(15, weight: .heavy))
                        }
                    }
                    .disabled(isSaving || profile == nil)
                }
            }
            .task { await load() }
        }
    }

    // MARK: - Sections

    private var avatarSection: some View {
        VStack(spacing: 12) {
            StoryAvatar(
                url: selectedAvatarData == nil && !avatarUrl.isEmpty ? URL(string: avatarUrl) : nil,
                size: 110,
                hasStory: false,
                initials: initials
            )
            .overlay {
                if let selectedAvatarData, let image = UIImage(data: selectedAvatarData) {
                    Image(uiImage: image)
                        .resizable()
                        .scaledToFill()
                        .clipShape(Circle())
                }
            }
            VStack(alignment: .leading, spacing: 6) {
                Text("Avatar")
                    .font(Theme.Font.caption(11, weight: .bold))
                    .foregroundStyle(Theme.Palette.inkMuted)
                HStack(spacing: 10) {
                    PhotosPicker(selection: $avatarItem, matching: .images) {
                        Label(selectedAvatarData == nil ? "Galleria" : "Selezionata", systemImage: "photo")
                            .frame(maxWidth: .infinity)
                            .padding(12)
                            .background(
                                RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                                    .fill(Theme.Palette.honeySoft)
                            )
                    }

                    Button {
                        showsCamera = true
                    } label: {
                        Label("Scatta", systemImage: "camera.fill")
                            .frame(maxWidth: .infinity)
                            .padding(12)
                            .background(
                                RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                                    .fill(Theme.Palette.honeySoft)
                            )
                    }
                    .buttonStyle(.plain)
                    .disabled(!UIImagePickerController.isSourceTypeAvailable(.camera))
                }
                TextField("https://…", text: $avatarUrl)
                    .textInputAutocapitalization(.never)
                    .autocorrectionDisabled(true)
                    .keyboardType(.URL)
                    .padding(12)
                    .background(
                        RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                            .fill(Theme.Palette.surface)
                    )
            }
        }
        .frame(maxWidth: .infinity)
        .onChange(of: avatarItem) { _, newValue in
            Task { await loadAvatar(newValue) }
        }
        .sheet(isPresented: $showsCamera) {
            CameraImagePicker(imageData: $selectedAvatarData)
                .ignoresSafeArea()
        }
        .padding(Theme.Spacing.lg)
        .background(
            RoundedRectangle(cornerRadius: Theme.Radius.lg, style: .continuous)
                .fill(Theme.Palette.surface)
        )
        .cardShadow()
    }

    private var identitySection: some View {
        SectionCard {
            sectionTitle("Identità")
            VStack(alignment: .leading, spacing: Theme.Spacing.md) {
                fieldGroup(label: "Nome visualizzato", placeholder: "Es. Luigi", text: $displayName)
                fieldGroup(label: "Anno di nascita", placeholder: "Es. 1997", text: $birthYearText)
                    .onChange(of: birthYearText) { _, new in
                        birthYearText = String(new.filter(\.isNumber).prefix(4))
                    }
                VStack(alignment: .leading, spacing: 6) {
                    Text("Genere")
                        .font(Theme.Font.caption(11, weight: .bold))
                        .foregroundStyle(Theme.Palette.inkMuted)
                    Picker("Genere", selection: $gender) {
                        ForEach(genders, id: \.value) { g in
                            Text(g.label).tag(g.value)
                        }
                    }
                    .pickerStyle(.segmented)
                }
            }
        }
    }

    private var aboutSection: some View {
        SectionCard {
            sectionTitle("Su di te")
            VStack(alignment: .leading, spacing: 6) {
                Text("Bio")
                    .font(Theme.Font.caption(11, weight: .bold))
                    .foregroundStyle(Theme.Palette.inkMuted)
                TextEditor(text: $bio)
                    .frame(minHeight: 100)
                    .padding(8)
                    .background(
                        RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                            .fill(Theme.Palette.surfaceAlt)
                    )
            }
        }
    }

    private var interestsSection: some View {
        SectionCard {
            sectionTitle("Interessi")
            VStack(alignment: .leading, spacing: Theme.Spacing.md) {
                if !interests.isEmpty {
                    FlowLayout(spacing: 8) {
                        ForEach(interests, id: \.self) { tag in
                            HStack(spacing: 6) {
                                Text(tag).font(Theme.Font.caption(12, weight: .bold))
                                Button {
                                    interests.removeAll { $0 == tag }
                                } label: {
                                    Image(systemName: "xmark.circle.fill")
                                        .font(.system(size: 14))
                                }
                                .buttonStyle(.plain)
                            }
                            .padding(.horizontal, 12)
                            .padding(.vertical, 6)
                            .background(Capsule().fill(Theme.Palette.honeySoft))
                            .foregroundStyle(Theme.Palette.ink)
                        }
                    }
                }
                InterestSwipeDeck(
                    suggestions: interestSuggestions.filter { !interests.contains($0) },
                    startIndex: $interestDeckIndex,
                    onAccept: { tag in
                        if !interests.contains(tag) {
                            interests.append(tag)
                            Haptics.tap()
                        }
                    },
                    onReject: { Haptics.tap() }
                )
                HStack {
                    TextField("Aggiungi un interesse", text: $newInterest)
                        .textInputAutocapitalization(.never)
                        .padding(10)
                        .background(
                            RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                                .fill(Theme.Palette.surfaceAlt)
                        )
                    Button {
                        addInterest()
                    } label: {
                        Image(systemName: "plus.circle.fill")
                            .font(.system(size: 26))
                            .foregroundStyle(Theme.Palette.honeyDeep)
                    }
                    .disabled(newInterest.trimmingCharacters(in: .whitespaces).isEmpty)
                }
            }
        }
    }

    private func sectionTitle(_ s: String) -> some View {
        Text(s)
            .font(Theme.Font.title(18, weight: .heavy))
            .frame(maxWidth: .infinity, alignment: .leading)
    }

    private func fieldGroup(label: String, placeholder: String, text: Binding<String>) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(label)
                .font(Theme.Font.caption(11, weight: .bold))
                .foregroundStyle(Theme.Palette.inkMuted)
            TextField(placeholder, text: text)
                .padding(12)
                .background(
                    RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                        .fill(Theme.Palette.surfaceAlt)
                )
        }
    }

    private var initials: String {
        let s = displayName.isEmpty ? (profile?.nickname ?? "?") : displayName
        return String(s.prefix(1)).uppercased()
    }

    // MARK: - Actions

    private func addInterest() {
        let t = newInterest.trimmingCharacters(in: .whitespaces)
        guard !t.isEmpty, !interests.contains(t) else { return }
        interests.append(t)
        newInterest = ""
    }

    private func loadAvatar(_ item: PhotosPickerItem?) async {
        guard let item else { return }
        do {
            selectedAvatarData = try await item.loadTransferable(type: Data.self)
        } catch {
            errorMessage = "Impossibile leggere l'immagine selezionata."
        }
    }

    private func load() async {
        isLoading = true
        defer { isLoading = false }
        do {
            let p = try await API.myEditableProfile()
            profile = p
            displayName = p.displayName ?? ""
            bio = p.bio ?? ""
            birthYearText = p.birthYear.map(String.init) ?? ""
            gender = p.gender
            avatarUrl = p.avatarUrl ?? ""
            interests = p.interests
        } catch {
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func save() async {
        isSaving = true
        defer { isSaving = false }
        do {
            if let selectedAvatarData {
                let updated = try await API.uploadAvatar(
                    data: selectedAvatarData,
                    fileName: "avatar-\(UUID().uuidString).jpg",
                    mimeType: "image/jpeg"
                )
                avatarUrl = updated.avatarUrl ?? avatarUrl
            }
            let req = UpdateMyProfileRequest(
                displayName: displayName.isEmpty ? nil : displayName,
                avatarUrl: avatarUrl.isEmpty ? nil : avatarUrl,
                bio: bio.isEmpty ? nil : bio,
                birthYear: Int(birthYearText),
                gender: gender,
                interests: interests
            )
            _ = try await API.updateMyProfile(req)
            Haptics.success()
            onSaved?()
            dismiss()
        } catch {
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
            Haptics.error()
        }
    }
}

private struct CameraImagePicker: UIViewControllerRepresentable {
    @Binding var imageData: Data?
    @Environment(\.dismiss) private var dismiss

    func makeUIViewController(context: Context) -> UIImagePickerController {
        let controller = UIImagePickerController()
        controller.sourceType = .camera
        controller.cameraCaptureMode = .photo
        controller.allowsEditing = true
        controller.delegate = context.coordinator
        return controller
    }

    func updateUIViewController(_ uiViewController: UIImagePickerController, context: Context) {}

    func makeCoordinator() -> Coordinator {
        Coordinator(parent: self)
    }

    final class Coordinator: NSObject, UIImagePickerControllerDelegate, UINavigationControllerDelegate {
        let parent: CameraImagePicker

        init(parent: CameraImagePicker) {
            self.parent = parent
        }

        func imagePickerController(_ picker: UIImagePickerController, didFinishPickingMediaWithInfo info: [UIImagePickerController.InfoKey: Any]) {
            let image = (info[.editedImage] as? UIImage) ?? (info[.originalImage] as? UIImage)
            parent.imageData = image?.jpegData(compressionQuality: 0.82)
            parent.dismiss()
        }

        func imagePickerControllerDidCancel(_ picker: UIImagePickerController) {
            parent.dismiss()
        }
    }
}

// MARK: - FlowLayout helper

/// Layout semplice per chip/tag che si dispongono su più righe.
struct FlowLayout: Layout {
    var spacing: CGFloat = 8

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) -> CGSize {
        let maxWidth = proposal.width ?? .infinity
        var rows: [[CGSize]] = [[]]
        var currentWidth: CGFloat = 0
        for subview in subviews {
            let size = subview.sizeThatFits(.unspecified)
            if currentWidth + size.width > maxWidth, !rows[rows.count - 1].isEmpty {
                rows.append([size])
                currentWidth = size.width + spacing
            } else {
                rows[rows.count - 1].append(size)
                currentWidth += size.width + spacing
            }
        }
        let height = rows.reduce(0) { acc, row in
            acc + (row.map(\.height).max() ?? 0) + spacing
        }
        return CGSize(width: maxWidth, height: max(0, height - spacing))
    }

    func placeSubviews(in bounds: CGRect, proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) {
        var x = bounds.minX
        var y = bounds.minY
        var rowMax: CGFloat = 0
        for subview in subviews {
            let size = subview.sizeThatFits(.unspecified)
            if x + size.width > bounds.maxX, x > bounds.minX {
                x = bounds.minX
                y += rowMax + spacing
                rowMax = 0
            }
            subview.place(at: CGPoint(x: x, y: y), proposal: ProposedViewSize(size))
            x += size.width + spacing
            rowMax = max(rowMax, size.height)
        }
    }
}

private struct InterestSwipeDeck: View {
    let suggestions: [String]
    @Binding var startIndex: Int
    let onAccept: (String) -> Void
    let onReject: () -> Void

    @State private var dragOffset: CGSize = .zero

    private var current: String? {
        guard !suggestions.isEmpty else { return nil }
        return suggestions[min(startIndex, suggestions.count - 1)]
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Scorri interessi")
                .font(Theme.Font.caption(11, weight: .bold))
                .foregroundStyle(Theme.Palette.inkMuted)

            if let current {
                ZStack {
                    ForEach(Array(suggestions.dropFirst(startIndex).prefix(3).enumerated()).reversed(), id: \.offset) { offset, tag in
                        card(tag)
                            .scaleEffect(1 - CGFloat(offset) * 0.04)
                            .offset(y: CGFloat(offset) * 7)
                            .zIndex(Double(3 - offset))
                    }
                }
                .frame(height: 145)
                .overlay(alignment: .bottom) {
                    HStack(spacing: 14) {
                        Button {
                            reject()
                        } label: {
                            Image(systemName: "xmark")
                        }
                        .buttonStyle(.ghost)

                        Button {
                            accept(current)
                        } label: {
                            Image(systemName: "heart.fill")
                        }
                        .buttonStyle(.honeyCompact)
                    }
                    .offset(y: 12)
                }
            } else {
                Text("Hai visto tutti gli interessi suggeriti.")
                    .font(Theme.Font.caption(12))
                    .foregroundStyle(Theme.Palette.inkMuted)
            }
        }
    }

    private func card(_ tag: String) -> some View {
        VStack(spacing: 8) {
            Image(systemName: icon(for: tag))
                .font(.system(size: 28, weight: .heavy))
                .foregroundStyle(Theme.Palette.honeyDeep)
            Text(tag.capitalized)
                .font(Theme.Font.title(20, weight: .heavy))
                .foregroundStyle(Theme.Palette.ink)
            Text("Swipe a destra per aggiungere")
                .font(Theme.Font.caption(11))
                .foregroundStyle(Theme.Palette.inkMuted)
        }
        .frame(maxWidth: .infinity)
        .padding(Theme.Spacing.md)
        .background(
            RoundedRectangle(cornerRadius: Theme.Radius.lg, style: .continuous)
                .fill(Theme.Palette.surfaceAlt)
        )
        .overlay {
            RoundedRectangle(cornerRadius: Theme.Radius.lg, style: .continuous)
                .stroke(Theme.Palette.hairline, lineWidth: 1)
        }
        .offset(dragOffset)
        .rotationEffect(.degrees(Double(dragOffset.width / 18)))
        .gesture(
            DragGesture()
                .onChanged { dragOffset = $0.translation }
                .onEnded { value in
                    if value.translation.width > 90 {
                        accept(tag)
                    } else if value.translation.width < -90 {
                        reject()
                    } else {
                        withAnimation(.spring()) { dragOffset = .zero }
                    }
                }
        )
    }

    private func accept(_ tag: String) {
        onAccept(tag)
        advance()
    }

    private func reject() {
        onReject()
        advance()
    }

    private func advance() {
        withAnimation(.spring(response: 0.35, dampingFraction: 0.82)) {
            startIndex += 1
            dragOffset = .zero
        }
    }

    private func icon(for tag: String) -> String {
        switch tag {
        case "cocktail", "wine bar", "pub": return "wineglass.fill"
        case "ristoranti", "sushi", "pizza": return "fork.knife"
        case "fitness", "running": return "figure.run"
        case "casino": return "suit.club.fill"
        case "musica live", "club": return "music.note"
        default: return "sparkles"
        }
    }
}
