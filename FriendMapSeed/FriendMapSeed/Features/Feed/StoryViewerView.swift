//
//  StoryViewerView.swift
//  Cloudy — Full-screen story viewer (Instagram-like)
//
//  UX premium: progress bar segmentata, tap left/right, swipe down dismiss,
//  long-press pausa, risposta rapida via DM.
//

import SwiftUI

struct StoryViewerView: View {
    let storiesByUser: [[UserStory]]
    let initialUserIndex: Int
    let onDismiss: () -> Void

    @State private var userIndex: Int = 0
    @State private var storyIndex: Int = 0
    @State private var progress: CGFloat = 0
    @State private var isPaused: Bool = false
    @State private var isReplying: Bool = false
    @State private var replyText: String = ""
    @State private var dragOffset: CGSize = .zero
    @State private var toastMessage: String?
    @State private var showLikeBurst: Bool = false

    private let storyDuration: CGFloat = 5.0
    private let timer = Timer.publish(every: 0.05, on: .main, in: .common).autoconnect()

    private var currentUserStories: [UserStory] {
        guard storiesByUser.indices.contains(userIndex) else { return [] }
        return storiesByUser[userIndex]
    }

    private var currentStory: UserStory? {
        guard currentUserStories.indices.contains(storyIndex) else { return nil }
        return currentUserStories[storyIndex]
    }

    // MARK: - Body

    var body: some View {
        GeometryReader { geo in
            ZStack {
                Color.black.ignoresSafeArea()

                // MARK: Media
                storyMedia(size: geo.size)

                // MARK: Tap layer (sotto UI, sopra media)
                tapLayer(size: geo.size)

                // MARK: UI overlay
                VStack(spacing: 0) {
                    progressBars(width: geo.size.width)
                    header
                    Spacer()
                    bottomOverlay
                }

                // MARK: Reply sheet
                if isReplying {
                    replySheet
                }

                // MARK: Like burst
                if showLikeBurst {
                    likeBurst
                }

                // MARK: Toast
                if let toast = toastMessage {
                    toastView(message: toast, width: geo.size.width)
                }
            }
            .offset(y: dragOffset.height)
            .simultaneousGesture(
                DragGesture()
                    .onChanged { value in
                        if value.translation.height > 0 && !isReplying {
                            dragOffset = value.translation
                        }
                    }
                    .onEnded { value in
                        if value.translation.height > 120 {
                            dismiss()
                        } else {
                            withAnimation(.spring(response: 0.3)) {
                                dragOffset = .zero
                            }
                        }
                    }
            )
        }
        .onAppear {
            userIndex = initialUserIndex
            storyIndex = 0
            progress = 0
        }
        .onReceive(timer) { _ in
            guard !isPaused && !isReplying else { return }
            progress += 0.05 / storyDuration
            if progress >= 1.0 {
                advance()
            }
        }
        .statusBar(hidden: true)
    }

    // MARK: - Media

    @ViewBuilder
    private func storyMedia(size: CGSize) -> some View {
        if let story = currentStory, let url = URL(string: story.mediaUrl ?? "") {
            AsyncImage(url: url) { phase in
                switch phase {
                case .success(let image):
                    image
                        .resizable()
                        .scaledToFill()
                        .frame(width: size.width, height: size.height)
                        .clipped()
                case .failure:
                    Color.gray.opacity(0.3)
                        .frame(width: size.width, height: size.height)
                default:
                    ProgressView()
                        .tint(.white)
                        .frame(width: size.width, height: size.height)
                }
            }
        } else {
            Color.gray.opacity(0.3)
        }
    }

    // MARK: - Tap layer

    private func tapLayer(size: CGSize) -> some View {
        Color.clear
            .contentShape(Rectangle())
            .simultaneousGesture(
                SpatialTapGesture()
                    .onEnded { event in
                        guard !isReplying else { return }
                        let loc = event.location
                        let headerH: CGFloat = 110
                        let footerH: CGFloat = 160
                        guard loc.y > headerH && loc.y < size.height - footerH else { return }
                        if loc.x < size.width / 3 {
                            goBack()
                        } else {
                            advance()
                        }
                    }
            )
            .onLongPressGesture(
                minimumDuration: .infinity,
                maximumDistance: .infinity,
                pressing: { pressing in
                    isPaused = pressing
                },
                perform: {}
            )
    }

    // MARK: - Progress bars

    private func progressBars(width: CGFloat) -> some View {
        HStack(spacing: 4) {
            ForEach(Array(currentUserStories.enumerated()), id: \.element.id) { index, _ in
                GeometryReader { barGeo in
                    ZStack(alignment: .leading) {
                        RoundedRectangle(cornerRadius: 1)
                            .fill(Color.white.opacity(0.25))
                        RoundedRectangle(cornerRadius: 1)
                            .fill(Color.white)
                            .frame(width: barWidth(for: index, totalWidth: barGeo.size.width), height: 2)
                    }
                }
                .frame(height: 2)
            }
        }
        .padding(.horizontal, 10)
        .padding(.top, 12)
    }

    private func barWidth(for index: Int, totalWidth: CGFloat) -> CGFloat {
        if index < storyIndex {
            return totalWidth
        } else if index == storyIndex {
            return totalWidth * min(progress, 1.0)
        } else {
            return 0
        }
    }

    // MARK: - Header

    private var header: some View {
        HStack(spacing: 10) {
            HStack(spacing: 8) {
                StoryAvatar(
                    url: URL(string: currentStory?.avatarUrl ?? ""),
                    size: 34,
                    hasStory: false,
                    initials: String((currentStory?.displayName ?? currentStory?.nickname ?? "?").prefix(1)).uppercased()
                )
                Text(currentStory?.displayName ?? currentStory?.nickname ?? "")
                    .font(Theme.Font.body(14, weight: .bold))
                    .foregroundStyle(.white)
                if let date = currentStory?.createdAtUtc {
                    Text("· \(relative(date))")
                        .font(Theme.Font.caption(12))
                        .foregroundStyle(.white.opacity(0.7))
                }
            }
            Spacer()
            Button {
                dismiss()
            } label: {
                Image(systemName: "xmark")
                    .font(.system(size: 16, weight: .bold))
                    .foregroundStyle(.white)
                    .padding(8)
                    .background(.ultraThinMaterial)
                    .clipShape(Circle())
            }
        }
        .padding(.horizontal, 14)
        .padding(.top, 4)
    }

    // MARK: - Bottom overlay

    private var bottomOverlay: some View {
        VStack(spacing: 16) {
            if let caption = currentStory?.caption, !caption.isEmpty {
                Text(caption)
                    .font(Theme.Font.body(15, weight: .semibold))
                    .foregroundStyle(.white)
                    .shadow(color: .black.opacity(0.6), radius: 4, x: 0, y: 1)
                    .multilineTextAlignment(.center)
                    .padding(.horizontal, 20)
            }

            HStack(spacing: 14) {
                Button {
                    sendLike()
                } label: {
                    Image(systemName: "heart.fill")
                        .font(.system(size: 26))
                        .foregroundStyle(Theme.Palette.igPink)
                        .frame(width: 44, height: 44)
                }

                Button {
                    withAnimation(.spring(response: 0.35)) {
                        isReplying = true
                    }
                } label: {
                    HStack {
                        Text("Invia un messaggio a \(currentStory?.displayName ?? currentStory?.nickname ?? "")")
                            .font(Theme.Font.body(14, weight: .medium))
                        Spacer()
                    }
                    .padding(.horizontal, 16)
                    .padding(.vertical, 12)
                    .background(
                        Capsule()
                            .stroke(Color.white.opacity(0.35), lineWidth: 1)
                    )
                    .foregroundStyle(.white)
                }

                Spacer(minLength: 0)
            }
            .padding(.horizontal, 14)
        }
        .padding(.bottom, 28)
        .background(
            LinearGradient(
                colors: [.black.opacity(0.0), .black.opacity(0.5)],
                startPoint: .top,
                endPoint: .bottom
            )
            .ignoresSafeArea(edges: .bottom)
        )
    }

    // MARK: - Reply sheet

    private var replySheet: some View {
        VStack {
            Spacer()
            VStack(spacing: 14) {
                HStack {
                    Text("Rispondi a \(currentStory?.displayName ?? currentStory?.nickname ?? "")")
                        .font(Theme.Font.title(16))
                        .foregroundStyle(.white)
                    Spacer()
                    Button {
                        withAnimation(.spring(response: 0.35)) {
                            isReplying = false
                        }
                    } label: {
                        Image(systemName: "xmark")
                            .font(.system(size: 15, weight: .bold))
                            .foregroundStyle(.white)
                            .padding(6)
                            .background(Color.white.opacity(0.15))
                            .clipShape(Circle())
                    }
                }

                HStack(spacing: 10) {
                    TextField("Scrivi qualcosa...", text: $replyText)
                        .font(Theme.Font.body(15))
                        .textFieldStyle(.plain)
                        .padding(.horizontal, 16)
                        .padding(.vertical, 12)
                        .background(Color.white.opacity(0.12))
                        .clipShape(RoundedRectangle(cornerRadius: 20, style: .continuous))
                        .foregroundStyle(.white)

                    Button {
                        sendReply()
                    } label: {
                        Image(systemName: "paperplane.fill")
                            .font(.system(size: 20))
                            .foregroundStyle(Theme.Palette.honey)
                    }
                    .disabled(replyText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                }
            }
            .padding(20)
            .background(.ultraThinMaterial)
            .clipShape(RoundedRectangle(cornerRadius: 28, style: .continuous))
            .padding(.horizontal, 12)
            .padding(.bottom, 16)
        }
        .background(
            Color.black.opacity(0.35)
                .ignoresSafeArea()
                .onTapGesture {
                    withAnimation(.spring(response: 0.35)) {
                        isReplying = false
                    }
                }
        )
        .transition(.move(edge: .bottom))
    }

    // MARK: - Like burst

    private var likeBurst: some View {
        Image(systemName: "heart.fill")
            .font(.system(size: 80, weight: .black))
            .foregroundStyle(Theme.Palette.igPink)
            .shadow(color: .black.opacity(0.3), radius: 12)
            .transition(.scale.combined(with: .opacity))
    }

    // MARK: - Toast

    private func toastView(message: String, width: CGFloat) -> some View {
        VStack {
            Text(message)
                .font(Theme.Font.body(14, weight: .semibold))
                .foregroundStyle(.white)
                .padding(.horizontal, 20)
                .padding(.vertical, 12)
                .background(.ultraThinMaterial)
                .clipShape(Capsule())
                .padding(.top, 100)
            Spacer()
        }
        .frame(width: width)
    }

    // MARK: - Navigation logic

    private func advance() {
        if storyIndex + 1 < currentUserStories.count {
            storyIndex += 1
            progress = 0
        } else if userIndex + 1 < storiesByUser.count {
            userIndex += 1
            storyIndex = 0
            progress = 0
        } else {
            dismiss()
        }
    }

    private func goBack() {
        if storyIndex > 0 {
            storyIndex -= 1
            progress = 0
        } else if userIndex > 0 {
            userIndex -= 1
            storyIndex = max(storiesByUser[userIndex].count - 1, 0)
            progress = 0
        }
    }

    private func dismiss() {
        onDismiss()
    }

    // MARK: - Actions

    private func sendReply() {
        guard let story = currentStory else { return }
        let text = replyText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty else { return }
        Task {
            do {
                _ = try await API.sendDirectMessage(otherUserId: story.userId, body: text)
                await MainActor.run {
                    replyText = ""
                    withAnimation(.spring(response: 0.35)) {
                        isReplying = false
                    }
                    showToast("Messaggio inviato")
                }
            } catch {
                await MainActor.run {
                    showToast("Errore nell'invio")
                }
            }
        }
    }

    private func sendLike() {
        guard let story = currentStory else { return }
        Task {
            do {
                _ = try await API.sendDirectMessage(otherUserId: story.userId, body: "❤️")
                await MainActor.run {
                    withAnimation(.spring(response: 0.3, dampingFraction: 0.5)) {
                        showLikeBurst = true
                    }
                    showToast("Inviato ❤️")
                    Task {
                        try? await Task.sleep(nanoseconds: 900_000_000)
                        await MainActor.run {
                            withAnimation(.easeOut) {
                                showLikeBurst = false
                            }
                        }
                    }
                }
            } catch {
                await MainActor.run {
                    showToast("Errore nell'invio")
                }
            }
        }
    }

    private func showToast(_ message: String) {
        toastMessage = message
        Task {
            try? await Task.sleep(nanoseconds: 2_000_000_000)
            await MainActor.run {
                toastMessage = nil
            }
        }
    }

    // MARK: - Helpers

    private func relative(_ date: Date) -> String {
        let f = RelativeDateTimeFormatter()
        f.unitsStyle = .short
        f.locale = Locale(identifier: "it_IT")
        return f.localizedString(for: date, relativeTo: Date())
    }
}
