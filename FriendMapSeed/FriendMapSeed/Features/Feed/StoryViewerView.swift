//
//  StoryViewerView.swift
//  Cloudy — Viewer individuale per storie, con carousel
//

import SwiftUI

struct StoryViewerView: View {
    let stories: [UserStory]
    @State private var localStories: [UserStory] = []
    @State private var currentIndex: Int = 0
    @State private var progress: Double = 0
    @State private var timer: Timer?
    @State private var comments: [StoryComment] = []
    @State private var commentDraft: String = ""
    @State private var showsComments = false
    @State private var showsShare = false
    @State private var friends: [SocialConnection] = []
    @State private var errorMessage: String?
    @State private var isPaused = false
    @State private var privateReplyDraft = ""
    @State private var showsPrivateReply = false
    @State private var isSendingPrivateReply = false
    @Environment(\.dismiss) var dismiss

    private let storyDuration: Double = 15

    var body: some View {
        ZStack {
            // Background scuro
            Color.black.ignoresSafeArea()

            VStack(spacing: 0) {
                // Progress bar
                HStack(spacing: 2) {
                    ForEach(0..<stories.count, id: \.self) { i in
                        GeometryReader { geo in
                            ZStack(alignment: .leading) {
                                Capsule()
                                    .fill(Color.white.opacity(0.3))
                                Capsule()
                                    .fill(Color.white)
                                    .frame(width: geo.size.width * (i == currentIndex ? progress : (i < currentIndex ? 1 : 0)))
                            }
                        }
                        .frame(height: 2)
                    }
                }
                .padding(Theme.Spacing.md)

                // Header con nome
                HStack {
                    StoryAvatar(
                        url: APIClient.shared.mediaURL(from: currentStory.avatarUrl),
                        size: 40,
                        hasStory: false,
                        initials: String((currentStory.displayName ?? currentStory.nickname).prefix(1)).uppercased()
                    )
                    VStack(alignment: .leading, spacing: 2) {
                        Text(currentStory.displayName ?? currentStory.nickname)
                            .font(Theme.Font.body(15, weight: .heavy))
                            .foregroundStyle(.white)
                        Text(timeAgo(currentStory.createdAtUtc))
                            .font(Theme.Font.caption(11))
                            .foregroundStyle(.white.opacity(0.7))
                    }
                    Spacer()
                    Button {
                        dismiss()
                    } label: {
                        Image(systemName: "xmark.circle.fill")
                            .font(.system(size: 24))
                            .foregroundStyle(.white.opacity(0.7))
                    }
                }
                .padding(Theme.Spacing.md)

                Spacer()

                // Media (immagine o placeholder)
                mediaView
                    .overlay {
                        HStack(spacing: 0) {
                            Color.clear
                                .contentShape(Rectangle())
                                .onTapGesture { previousStory() }
                            Color.clear
                                .contentShape(Rectangle())
                                .onTapGesture { nextStory() }
                        }
                    }

                // Caption (se presente)
                if let caption = currentStory.caption {
                    Text(caption)
                        .font(Theme.Font.body(15))
                        .foregroundStyle(.white)
                        .multilineTextAlignment(.leading)
                        .padding(Theme.Spacing.lg)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .background(Color.black.opacity(0.5))
                }

                Spacer()
            }
        }
        .overlay(alignment: .bottom) {
            storyActions
                .padding(.top, 18)
                .padding(.bottom, 168)
                .background(
                    LinearGradient(
                        colors: [.clear, .black.opacity(0.86)],
                        startPoint: .top,
                        endPoint: .bottom
                    )
                    .ignoresSafeArea(edges: .bottom)
                )
                .animation(.cloudySnap, value: showsComments)
                .animation(.cloudySnap, value: showsPrivateReply)
        }
        .onAppear {
            localStories = stories
            startProgress()
        }
        .onDisappear {
            timer?.invalidate()
            timer = nil
        }
        .onChange(of: currentIndex) {
            progress = 0
            privateReplyDraft = ""
            commentDraft = ""
            showsPrivateReply = false
            showsComments = false
            startProgress()
        }
        .sheet(isPresented: $showsShare) {
            shareSheet
        }
        .simultaneousGesture(
            DragGesture(minimumDistance: 0)
                .onChanged { _ in
                    isPaused = true
                }
                .onEnded { value in
                    isPaused = false
                    if value.translation.height < -70 && abs(value.translation.height) > abs(value.translation.width) {
                        withAnimation(.cloudySnap) {
                            showsPrivateReply = true
                        }
                    }
                }
        )
        .onLongPressGesture(minimumDuration: 0.08, maximumDistance: 38, pressing: { pressing in
            isPaused = pressing
        }, perform: {})
    }

    private var currentStory: UserStory {
        let source = localStories.isEmpty ? stories : localStories
        guard currentIndex < source.count else { return source.first! }
        return source[currentIndex]
    }

    @ViewBuilder
    private var mediaView: some View {
        if let mediaUrl = APIClient.shared.mediaURL(from: currentStory.mediaUrl) {
            AsyncImage(url: mediaUrl) { phase in
                switch phase {
                case .success(let image):
                    image
                        .resizable()
                        .scaledToFit()
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                case .empty:
                    ProgressView()
                        .tint(.white)
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                case .failure:
                    placeholder
                @unknown default:
                    placeholder
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        } else {
            placeholder
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

    private var storyActions: some View {
        VStack(spacing: 12) {
            if showsComments {
                commentsPanel
            }

            actionDock
        }
    }

    private var actionDock: some View {
        HStack(spacing: 10) {
            if canPrivateReply {
                HStack(spacing: 8) {
                    TextField("Invia messaggio…", text: $privateReplyDraft)
                        .textFieldStyle(.plain)
                        .font(Theme.Font.body(14, weight: .semibold))
                        .foregroundStyle(.white)
                        .submitLabel(.send)
                        .onSubmit {
                            Task { await sendPrivateReply() }
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
                .padding(.leading, 15)
                .padding(.trailing, 7)
                .padding(.vertical, 7)
                .background(.white.opacity(0.14), in: Capsule())
                .overlay(Capsule().stroke(.white.opacity(0.24), lineWidth: 1))
            } else {
                Text("La tua storia")
                    .font(Theme.Font.body(14, weight: .semibold))
                    .foregroundStyle(.white.opacity(0.7))
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.horizontal, 15)
                    .padding(.vertical, 13)
                    .background(.white.opacity(0.12), in: Capsule())
            }

            Button {
                Task { await toggleLike() }
            } label: {
                VStack(spacing: 1) {
                    Image(systemName: currentStory.hasLiked ? "heart.fill" : "heart")
                        .font(.system(size: 19, weight: .bold))
                    if currentStory.likeCount > 0 {
                        Text("\(currentStory.likeCount)")
                            .font(Theme.Font.caption(9, weight: .heavy))
                    }
                }
                .foregroundStyle(currentStory.hasLiked ? Theme.Palette.igPink : .white)
                .frame(width: 42, height: 42)
                .background(.white.opacity(0.14), in: Circle())
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
                VStack(spacing: 1) {
                    Image(systemName: showsComments ? "bubble.right.fill" : "bubble.right")
                        .font(.system(size: 18, weight: .bold))
                    if currentStory.commentCount > 0 {
                        Text("\(currentStory.commentCount)")
                            .font(Theme.Font.caption(9, weight: .heavy))
                    }
                }
                .foregroundStyle(.white)
                .frame(width: 42, height: 42)
                .background(.white.opacity(showsComments ? 0.24 : 0.14), in: Circle())
            }

            Button {
                showsShare = true
                Task { await loadFriends() }
                Haptics.tap()
            } label: {
                Image(systemName: "paperplane.fill")
                    .font(.system(size: 18, weight: .bold))
                    .foregroundStyle(.white)
                    .frame(width: 42, height: 42)
                    .background(.white.opacity(0.14), in: Circle())
            }
        }
        .padding(10)
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 30, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 30, style: .continuous)
                .stroke(.white.opacity(0.18), lineWidth: 1)
        )
        .padding(.horizontal, 12)
    }

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
        .padding(.horizontal, 12)
        .transition(.asymmetric(insertion: .move(edge: .bottom).combined(with: .opacity), removal: .opacity))
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

    private var canPrivateReply: Bool {
        API.currentUserId != currentStory.userId
    }

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

    private func startProgress() {
        timer?.invalidate()
        progress = 0
        timer = Timer.scheduledTimer(withTimeInterval: 0.016, repeats: true) { _ in
            guard !isPaused, !showsShare, !showsComments else { return }
            progress += 0.016 / storyDuration
            if progress >= 1 {
                if currentIndex < stories.count - 1 {
                    currentIndex += 1
                    progress = 0
                } else {
                    dismiss()
                }
            }
        }
    }

    private func previousStory() {
        withAnimation(.cloudySnap) {
            if currentIndex > 0 {
                currentIndex -= 1
            }
            progress = 0
        }
    }

    private func nextStory() {
        withAnimation(.cloudySnap) {
            if currentIndex < stories.count - 1 {
                currentIndex += 1
                progress = 0
            } else {
                dismiss()
            }
        }
    }

    private func toggleLike() async {
        do {
            let result = try await API.toggleStoryLike(storyId: currentStory.id)
            updateCurrentStory { story in
                story.hasLiked = result.liked
                story.likeCount = result.likeCount
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

    private func updateCurrentStory(_ update: (inout UserStory) -> Void) {
        if localStories.isEmpty {
            localStories = stories
        }
        guard currentIndex < localStories.count else { return }
        update(&localStories[currentIndex])
    }

    private func timeAgo(_ date: Date) -> String {
        let formatter = RelativeDateTimeFormatter()
        formatter.unitsStyle = .short
        formatter.locale = Locale(identifier: "it_IT")
        return formatter.localizedString(for: date, relativeTo: Date())
    }
}
