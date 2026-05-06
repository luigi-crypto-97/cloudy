//
//  StoryViewerView.swift
//  Cloudy — Full-screen story viewer (Instagram-like)
//
//  UX premium: progress bar segmentata, tap left/right, swipe down dismiss,
//  long-press pausa, risposta rapida, commenti, like e share.
//  Navigazione multi-utente: scorre tra le storie dello stesso autore,
//  poi passa automaticamente al prossimo utente.
//

import SwiftUI
import AVKit
import UIKit

struct StoryViewerView: View {
    let storiesByUser: [[UserStory]]
    let initialUserIndex: Int
    let onDismiss: () -> Void

    @Environment(AppRouter.self) private var router
    @Environment(\.dismiss) private var dismissSheet

    @State private var userIndex: Int = 0
    @State private var storyIndex: Int = 0
    @State private var progress: Double = 0
    @State private var progressTask: Task<Void, Never>?
    @State private var isPaused: Bool = false
    @State private var dragOffset: CGSize = .zero

    // Remote features
    @State private var localUserStories: [UserStory] = []
    @State private var comments: [StoryComment] = []
    @State private var commentDraft: String = ""
    @State private var showsComments = false
    @State private var showsShare = false
    @State private var friends: [SocialConnection] = []
    @State private var errorMessage: String?
    @State private var privateReplyDraft = ""
    @State private var showsPrivateReply = false
    @State private var isSendingPrivateReply = false
    @State private var showLikeBurst: Bool = false
    @State private var showDeleteConfirmation = false
    @State private var isDeletingStory = false

    private let storyDuration: Double = 15

    // MARK: - Init (backward compatible)

    init(storiesByUser: [[UserStory]], initialUserIndex: Int = 0, onDismiss: @escaping () -> Void = {}) {
        self.storiesByUser = storiesByUser
        self.initialUserIndex = initialUserIndex
        self.onDismiss = onDismiss
    }

    init(stories: [UserStory]) {
        self.storiesByUser = [stories]
        self.initialUserIndex = 0
        self.onDismiss = {}
    }

    // MARK: - Computed

    private var currentUserStories: [UserStory] {
        let source = localUserStories.isEmpty ? storiesByUser[userIndex] : localUserStories
        return source
    }

    private var currentStory: UserStory {
        guard storyIndex < currentUserStories.count else { return currentUserStories.first! }
        return currentUserStories[storyIndex]
    }

    // MARK: - Body

    var body: some View {
        ZStack {
            Color.black.ignoresSafeArea()

            // MARK: Media (full screen, ignores safe area)
            mediaView
                .ignoresSafeArea()
                .overlay {
                    HStack(spacing: 0) {
                        Color.clear
                            .contentShape(Rectangle())
                            .onTapGesture { goBack() }
                        Color.clear
                            .contentShape(Rectangle())
                            .onTapGesture { advance() }
                    }
                }

            // MARK: UI overlay
            VStack(spacing: 0) {
                progressBars
                    .padding(.horizontal, 10)
                    .padding(.top, 12)
                header
                    .padding(.horizontal, 14)
                    .padding(.top, 4)
                Spacer()
                bottomOverlay
            }

            // MARK: Like burst
            if showLikeBurst {
                likeBurst
            }
        }
        .offset(y: dragOffset.height)
        .simultaneousGesture(
                DragGesture()
                    .onChanged { value in
                        if value.translation.height > 0 && !showsComments && !showsShare {
                            dragOffset = value.translation
                            isPaused = true
                        }
                    }
                    .onEnded { value in
                        isPaused = false
                        if value.translation.height > 120 {
                            close()
                        } else {
                            withAnimation(.spring(response: 0.3)) {
                                dragOffset = .zero
                            }
                        }
                    }
            )
        .onAppear {
            router.isTabBarHidden = true
            userIndex = initialUserIndex
            storyIndex = 0
            localUserStories = storiesByUser[safe: initialUserIndex] ?? []
            startProgress()
        }
        .onDisappear {
            router.isTabBarHidden = false
            progressTask?.cancel()
            progressTask = nil
        }
        .onChange(of: storyIndex) {
            resetPerStoryState()
            startProgress()
        }
        .onChange(of: userIndex) {
            localUserStories = storiesByUser[safe: userIndex] ?? []
            resetPerStoryState()
            startProgress()
        }
        .sheet(isPresented: $showsShare) {
            shareSheet
        }
        .confirmationDialog("Eliminare questa storia?", isPresented: $showDeleteConfirmation, titleVisibility: .visible) {
            Button("Elimina storia", role: .destructive) {
                Task { await deleteCurrentStory() }
            }
            Button("Annulla", role: .cancel) {}
        } message: {
            Text("La storia verra rimossa per tutti e non sara piu visibile.")
        }
        .onLongPressGesture(minimumDuration: 0.08, maximumDistance: 38, pressing: { pressing in
            isPaused = pressing
        }, perform: {})
        .statusBar(hidden: true)
    }

    // MARK: - Media

    @ViewBuilder
    private var mediaView: some View {
        GeometryReader { geometry in
            if let mediaUrl = APIClient.shared.mediaURL(from: currentStory.mediaUrl) {
                if mediaUrl.isCloudyVideoURL {
                    StoryVideoPlayer(url: mediaUrl)
                        .frame(width: geometry.size.width, height: geometry.size.height)
                        .clipped()
                } else {
                    StoryRemoteImage(url: mediaUrl)
                        .frame(width: geometry.size.width, height: geometry.size.height)
                        .clipped()
                }
            } else {
                placeholder
            }
        }
    }

    private var placeholder: some View {
        ZStack {
            Color.gray.opacity(0.3)
            Image(systemName: "photo.fill")
                .font(.system(size: 40))
                .foregroundStyle(.white.opacity(0.5))
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    // MARK: - Progress bars

    private var progressBars: some View {
        HStack(spacing: 4) {
            ForEach(Array(currentUserStories.enumerated()), id: \.offset) { index, _ in
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
    }

    private func barWidth(for index: Int, totalWidth: CGFloat) -> CGFloat {
        if index < storyIndex { return totalWidth }
        if index == storyIndex { return totalWidth * min(progress, 1.0) }
        return 0
    }

    // MARK: - Header

    private var header: some View {
        HStack(spacing: 10) {
            HStack(spacing: 8) {
                StoryAvatar(
                    url: APIClient.shared.mediaURL(from: currentStory.avatarUrl),
                    size: 34,
                    hasStory: false,
                    initials: String((currentStory.displayName ?? currentStory.nickname).prefix(1)).uppercased()
                )
                Text(currentStory.displayName ?? currentStory.nickname)
                    .font(Theme.Font.body(14, weight: .bold))
                    .foregroundStyle(.white)
                Text("· \(timeAgo(currentStory.createdAtUtc))")
                    .font(Theme.Font.caption(12))
                    .foregroundStyle(.white.opacity(0.7))
            }
            Spacer()
            if canDeleteCurrentStory {
                Button {
                    isPaused = true
                    showDeleteConfirmation = true
                    Haptics.tap()
                } label: {
                    Image(systemName: isDeletingStory ? "hourglass" : "trash")
                        .font(.system(size: 15, weight: .bold))
                        .foregroundStyle(.white)
                        .padding(8)
                        .background(.ultraThinMaterial)
                        .clipShape(Circle())
                }
                .disabled(isDeletingStory)
            }
            Button {
                close()
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
        VStack(spacing: 0) {
            if let venueName = currentStory.venueName, !venueName.isEmpty {
                Label(venueName, systemImage: "mappin.circle.fill")
                    .font(Theme.Font.caption(12, weight: .heavy))
                    .foregroundStyle(.white.opacity(0.9))
                    .padding(.horizontal, 10)
                    .padding(.vertical, 6)
                    .background(.black.opacity(0.30), in: Capsule())
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.horizontal, 18)
                    .padding(.bottom, 8)
            }

            if let caption = currentStory.caption, !caption.isEmpty {
                Text(caption)
                    .font(Theme.Font.body(15, weight: .semibold))
                    .foregroundStyle(.white)
                    .shadow(color: .black.opacity(0.6), radius: 4, x: 0, y: 1)
                    .multilineTextAlignment(.leading)
                    .lineLimit(3)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.horizontal, 18)
                    .padding(.bottom, 12)
            }

            if showsComments {
                commentsPanel
                    .padding(.bottom, 8)
                    .transition(.asymmetric(insertion: .move(edge: .bottom).combined(with: .opacity), removal: .opacity))
            }

            actionDock
        }
        .background(
            LinearGradient(
                colors: [.black.opacity(0.0), .black.opacity(0.50)],
                startPoint: .top,
                endPoint: .bottom
            )
            .ignoresSafeArea(edges: .bottom)
        )
        .animation(.cloudySnap, value: showsComments)
        .animation(.cloudySnap, value: showsPrivateReply)
    }

    // MARK: - Action dock

    private var actionDock: some View {
        HStack(spacing: 8) {
            if canPrivateReply {
                HStack(spacing: 8) {
                    TextField(showsPrivateReply ? "Invia messaggio..." : "Rispondi alla storia", text: $privateReplyDraft)
                        .textFieldStyle(.plain)
                        .font(Theme.Font.body(14, weight: .semibold))
                        .foregroundStyle(.white)
                        .lineLimit(1)
                        .submitLabel(.send)
                        .onSubmit {
                            Task { await sendPrivateReply() }
                        }
                        .onTapGesture {
                            withAnimation(.cloudySnap) { showsPrivateReply = true }
                        }

                    if !privateReplyDraft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                        Button {
                            Task { await sendPrivateReply() }
                        } label: {
                            Image(systemName: isSendingPrivateReply ? "hourglass" : "arrow.up")
                                .font(.system(size: 14, weight: .black))
                                .foregroundStyle(.black)
                                .frame(width: 30, height: 30)
                                .background(Circle().fill(.white))
                        }
                        .disabled(isSendingPrivateReply)
                        .transition(.scale.combined(with: .opacity))
                    }
                }
                .frame(maxWidth: .infinity)
                .layoutPriority(1)
                .padding(.horizontal, 12)
                .padding(.vertical, 9)
                .background(.white.opacity(0.08), in: Capsule())
                .overlay(Capsule().stroke(.white.opacity(0.15), lineWidth: 0.5))
            } else {
                Text("La tua storia")
                    .font(Theme.Font.body(14, weight: .semibold))
                    .foregroundStyle(.white.opacity(0.7))
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.horizontal, 12)
                    .padding(.vertical, 11)
                    .background(.white.opacity(0.06), in: Capsule())
            }

            Button {
                Task { await toggleLike() }
            } label: {
                Image(systemName: currentStory.hasLiked ? "heart.fill" : "heart")
                    .font(.system(size: 20, weight: .bold))
                    .foregroundStyle(currentStory.hasLiked ? Theme.Palette.igPink : .white)
                    .shadow(color: .black.opacity(0.45), radius: 6)
                    .frame(width: 38, height: 38)
            }

            Button {
                withAnimation(.cloudySnap) {
                    showsComments.toggle()
                }
                if showsComments {
                    Task { await loadComments() }
                }
                Haptics.tap()
            } label: {
                Image(systemName: showsComments ? "bubble.right.fill" : "bubble.right")
                    .font(.system(size: 20, weight: .bold))
                    .foregroundStyle(.white)
                    .shadow(color: .black.opacity(0.45), radius: 6)
                    .frame(width: 38, height: 38)
            }

            Button {
                showsShare = true
                Task { await loadFriends() }
                Haptics.tap()
            } label: {
                Image(systemName: "paperplane.fill")
                    .font(.system(size: 20, weight: .bold))
                    .foregroundStyle(.white)
                    .shadow(color: .black.opacity(0.45), radius: 6)
                    .frame(width: 38, height: 38)
            }
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 10)
        .padding(.bottom, 8)
        .background(.black.opacity(0.18), in: Capsule())
        .padding(.horizontal, 10)
    }

    // MARK: - Comments panel

    private var commentsPanel: some View {
        VStack(alignment: .leading, spacing: 12) {
            Capsule()
                .fill(.white.opacity(0.28))
                .frame(width: 36, height: 4)
                .frame(maxWidth: .infinity)

            HStack {
                Text("Commenti")
                    .font(Theme.Font.title(17, weight: .heavy))
                    .foregroundStyle(.white)
                Text("\(currentStory.commentCount)")
                    .font(Theme.Font.caption(11, weight: .heavy))
                    .foregroundStyle(.white.opacity(0.68))
                    .padding(.horizontal, 8)
                    .padding(.vertical, 4)
                    .background(.white.opacity(0.12), in: Capsule())
                Spacer()
                Button {
                    withAnimation(.cloudySnap) { showsComments = false }
                } label: {
                    Image(systemName: "xmark")
                        .font(.system(size: 12, weight: .black))
                        .foregroundStyle(.white.opacity(0.82))
                        .frame(width: 28, height: 28)
                        .background(.white.opacity(0.12), in: Circle())
                }
                .buttonStyle(.plain)
            }

            if comments.isEmpty {
                Text("Ancora nessun commento. Apri tu la conversazione.")
                    .font(Theme.Font.body(13, weight: .semibold))
                    .foregroundStyle(.white.opacity(0.72))
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.vertical, 8)
            } else {
                ScrollView {
                    VStack(spacing: 10) {
                        ForEach(comments.prefix(8)) { comment in
                            commentRow(comment)
                        }
                    }
                }
                .frame(maxHeight: 190)
                .scrollIndicators(.hidden)
            }

            HStack(spacing: 8) {
                TextField("Commenta per gli amici…", text: $commentDraft)
                    .textFieldStyle(.plain)
                    .font(Theme.Font.body(14, weight: .semibold))
                    .foregroundStyle(.white)
                    .lineLimit(1)
                    .frame(maxWidth: .infinity)
                    .submitLabel(.send)
                    .onSubmit {
                        Task { await sendComment() }
                    }
                    .padding(.horizontal, 13)
                    .padding(.vertical, 11)
                    .background(.white.opacity(0.13), in: Capsule())
                    .overlay(Capsule().stroke(.white.opacity(0.16), lineWidth: 1))

                Button {
                    Task { await sendComment() }
                } label: {
                    Image(systemName: "arrow.up")
                        .font(.system(size: 15, weight: .black))
                        .foregroundStyle(.black)
                        .frame(width: 38, height: 38)
                        .background(Circle().fill(.white))
                }
                .disabled(commentDraft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                .opacity(commentDraft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? 0.42 : 1)
            }

            if let errorMessage {
                Text(errorMessage)
                    .font(Theme.Font.caption(11, weight: .semibold))
                    .foregroundStyle(Theme.Palette.honey)
            }
        }
        .padding(14)
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 28, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 28, style: .continuous)
                .stroke(.white.opacity(0.18), lineWidth: 1)
        )
        .padding(.horizontal, 10)
    }

    private func commentRow(_ comment: StoryComment) -> some View {
        HStack(alignment: .top, spacing: 9) {
            StoryAvatar(
                url: APIClient.shared.mediaURL(from: comment.avatarUrl),
                size: 30,
                hasStory: false,
                initials: String((comment.displayName ?? comment.nickname).prefix(1)).uppercased()
            )
            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: 5) {
                    Text(comment.displayName ?? comment.nickname)
                        .font(Theme.Font.caption(11, weight: .heavy))
                        .foregroundStyle(.white)
                    Text(timeAgo(comment.createdAtUtc))
                        .font(Theme.Font.caption(10))
                        .foregroundStyle(.white.opacity(0.52))
                }
                Text(comment.body)
                    .font(Theme.Font.body(13, weight: .semibold))
                    .foregroundStyle(.white.opacity(0.9))
                    .fixedSize(horizontal: false, vertical: true)
            }
            Spacer(minLength: 0)
        }
    }

    // MARK: - Like burst

    private var likeBurst: some View {
        Image(systemName: "heart.fill")
            .font(.system(size: 80, weight: .black))
            .foregroundStyle(Theme.Palette.igPink)
            .shadow(color: .black.opacity(0.3), radius: 12)
            .transition(.scale.combined(with: .opacity))
    }

    // MARK: - Share sheet

    private var shareSheet: some View {
        NavigationStack {
            List(friends) { friend in
                Button {
                    Task {
                        do {
                            _ = try await API.shareStory(storyId: currentStory.id, targetUserId: friend.userId, message: nil)
                            Haptics.success()
                            showsShare = false
                        } catch {
                            Haptics.error()
                            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
                        }
                    }
                } label: {
                    HStack {
                        StoryAvatar(
                            url: APIClient.shared.mediaURL(from: friend.avatarUrl),
                            size: 38,
                            initials: String((friend.displayName ?? friend.nickname).prefix(1)).uppercased()
                        )
                        Text(friend.displayName ?? friend.nickname)
                    }
                }
            }
            .navigationTitle("Invia a")
            .navigationBarTitleDisplayMode(.inline)
            .overlay {
                if friends.isEmpty {
                    CloudyEmptyState(
                        icon: "person.2",
                        title: "Nessun amico disponibile",
                        message: "Quando aggiungi amici potrai inviare loro questa storia."
                    )
                    .padding()
                }
            }
        }
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
            close()
        }
    }

    private func goBack() {
        if storyIndex > 0 {
            storyIndex -= 1
            progress = 0
        } else if userIndex > 0 {
            userIndex -= 1
            storyIndex = max((storiesByUser[safe: userIndex]?.count ?? 1) - 1, 0)
            progress = 0
        }
    }

    private func close() {
        progressTask?.cancel()
        progressTask = nil
        onDismiss()
    }

    private func resetPerStoryState() {
        progress = 0
        privateReplyDraft = ""
        commentDraft = ""
        showsPrivateReply = false
        showsComments = false
    }

    // MARK: - Progress

    private func startProgress() {
        progressTask?.cancel()
        let storyId = currentStory.id
        progressTask = Task {
            while !Task.isCancelled {
                try? await Task.sleep(nanoseconds: 50_000_000)
                guard !Task.isCancelled, currentStory.id == storyId else { return }
                guard !isPaused, !showsShare, !showsComments else { continue }
                tickProgress(delta: 0.05)
            }
        }
    }

    private func tickProgress(delta: Double) {
        guard currentUserStories.indices.contains(storyIndex) else { return }
        progress += delta / storyDuration
        if progress >= 1 {
            if storyIndex < currentUserStories.count - 1 {
                storyIndex += 1
                progress = 0
            } else if userIndex < storiesByUser.count - 1 {
                userIndex += 1
                storyIndex = 0
                progress = 0
            } else {
                close()
            }
        }
    }

    // MARK: - Actions

    private func toggleLike() async {
        do {
            let result = try await API.toggleStoryLike(storyId: currentStory.id)
            updateCurrentStory { story in
                story.hasLiked = result.liked
                story.likeCount = result.likeCount
            }
            if result.liked {
                withAnimation(.spring(response: 0.3, dampingFraction: 0.5)) {
                    showLikeBurst = true
                }
                Task {
                    try? await Task.sleep(nanoseconds: 900_000_000)
                    await MainActor.run {
                        withAnimation(.easeOut) { showLikeBurst = false }
                    }
                }
            }
            Haptics.tap()
        } catch {
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func loadComments() async {
        do {
            comments = try await API.storyComments(storyId: currentStory.id)
            errorMessage = nil
        } catch {
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func sendComment() async {
        let body = commentDraft.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !body.isEmpty else { return }
        do {
            let comment = try await API.addStoryComment(storyId: currentStory.id, body: body)
            comments.append(comment)
            commentDraft = ""
            updateCurrentStory { $0.commentCount += 1 }
            Haptics.success()
        } catch {
            Haptics.error()
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func sendPrivateReply() async {
        let body = privateReplyDraft.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !body.isEmpty else { return }
        isSendingPrivateReply = true
        defer { isSendingPrivateReply = false }
        do {
            _ = try await API.shareStory(storyId: currentStory.id, targetUserId: currentStory.userId, message: body)
            privateReplyDraft = ""
            withAnimation(.cloudySnap) { showsPrivateReply = false }
            Haptics.success()
        } catch {
            Haptics.error()
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func loadFriends() async {
        do {
            friends = try await API.socialHub().friends
        } catch {
            friends = []
        }
    }

    private func deleteCurrentStory() async {
        guard canDeleteCurrentStory else { return }
        isDeletingStory = true
        defer {
            isDeletingStory = false
            isPaused = false
        }

        do {
            let deletedId = currentStory.id
            try await API.deleteStory(id: deletedId)
            if localUserStories.isEmpty {
                localUserStories = storiesByUser[safe: userIndex] ?? []
            }
            localUserStories.removeAll { $0.id == deletedId }
            Haptics.success()

            if localUserStories.isEmpty {
                close()
            } else {
                storyIndex = min(storyIndex, localUserStories.count - 1)
                resetPerStoryState()
                startProgress()
            }
        } catch {
            Haptics.error()
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    // MARK: - Helpers

    private var canPrivateReply: Bool {
        API.currentUserId != currentStory.userId
    }

    private var canDeleteCurrentStory: Bool {
        API.currentUserId == currentStory.userId
    }

    private func updateCurrentStory(_ update: (inout UserStory) -> Void) {
        if localUserStories.isEmpty {
            localUserStories = storiesByUser[safe: userIndex] ?? []
        }
        guard storyIndex < localUserStories.count else { return }
        update(&localUserStories[storyIndex])
    }

    private func timeAgo(_ date: Date) -> String {
        let formatter = RelativeDateTimeFormatter()
        formatter.unitsStyle = .short
        formatter.locale = Locale(identifier: "it_IT")
        return formatter.localizedString(for: date, relativeTo: Date())
    }
}

private struct StoryVideoPlayer: View {
    let url: URL
    @State private var player: AVPlayer?
    @State private var loadFailed = false

    var body: some View {
        ZStack {
            if let player {
                StoryFillVideoPlayer(player: player)
                    .onAppear { player.play() }
                    .onDisappear { player.pause() }
            } else if loadFailed {
                Color.black
                    .overlay(Image(systemName: "video.slash").foregroundStyle(.white.opacity(0.7)))
            } else {
                Color.black
                    .overlay(ProgressView().tint(.white))
            }
        }
        .task(id: url) {
            do {
                let playableURL = url.isFileURL ? url : try await MediaFileCache.shared.localFileURL(for: url)
                let newPlayer = AVPlayer(url: playableURL)
                player = newPlayer
                newPlayer.play()
            } catch {
                loadFailed = true
            }
        }
    }
}

private struct StoryFillVideoPlayer: UIViewRepresentable {
    let player: AVPlayer

    func makeUIView(context: Context) -> PlayerContainerView {
        let view = PlayerContainerView()
        view.playerLayer.videoGravity = .resizeAspectFill
        view.playerLayer.player = player
        return view
    }

    func updateUIView(_ uiView: PlayerContainerView, context: Context) {
        uiView.playerLayer.videoGravity = .resizeAspectFill
        uiView.playerLayer.player = player
    }
}

private final class PlayerContainerView: UIView {
    override static var layerClass: AnyClass {
        AVPlayerLayer.self
    }

    var playerLayer: AVPlayerLayer {
        layer as! AVPlayerLayer
    }
}

private struct StoryRemoteImage: View {
    let url: URL
    @State private var image: UIImage?
    @State private var loadFailed = false

    var body: some View {
        ZStack {
            Color.black

            if let image {
                Image(uiImage: image)
                    .resizable()
                    .scaledToFill()
                    .transition(.opacity.animation(.easeInOut(duration: 0.18)))
            } else if loadFailed {
                VStack(spacing: 10) {
                    Image(systemName: "photo.badge.exclamationmark")
                        .font(.system(size: 30, weight: .semibold))
                    Text("Media non disponibile")
                        .font(Theme.Font.caption(12, weight: .semibold))
                }
                .foregroundStyle(.white.opacity(0.72))
            } else {
                ProgressView()
                    .tint(.white)
            }
        }
        .task(id: url) {
            loadFailed = false
            image = nil
            do {
                let data = try await MediaFileCache.shared.data(for: url)
                guard let decoded = UIImage(data: data) else {
                    throw URLError(.cannotDecodeContentData)
                }
                image = decoded
            } catch {
                loadFailed = true
            }
        }
    }
}

// MARK: - Safe array access

private extension Array {
    subscript(safe index: Int) -> Element? {
        indices.contains(index) ? self[index] : nil
    }
}
