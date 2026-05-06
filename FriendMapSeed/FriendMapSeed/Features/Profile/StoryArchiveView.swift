//
//  StoryArchiveView.swift
//  Cloudy
//

import SwiftUI
import AVKit
import UIKit

struct StoryArchiveView: View {
    @Environment(\.dismiss) private var dismiss
    @State private var stories: [UserStory] = []
    @State private var isLoading = false
    @State private var errorMessage: String?
    @State private var selectedStory: UserStory?

    private let columns = [
        GridItem(.flexible(), spacing: 10),
        GridItem(.flexible(), spacing: 10),
        GridItem(.flexible(), spacing: 10)
    ]

    var body: some View {
        NavigationStack {
            Group {
                if isLoading && stories.isEmpty {
                    ProgressView()
                        .tint(Theme.Palette.blue500)
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                } else if stories.isEmpty {
                    CloudyEmptyState(
                        icon: "archivebox.fill",
                        title: "Archivio vuoto",
                        message: "Le storie che pubblichi restano qui solo per te."
                    )
                    .padding()
                } else {
                    ScrollView {
                        LazyVStack(alignment: .leading, spacing: 18) {
                            ForEach(archiveSections, id: \.title) { section in
                                VStack(alignment: .leading, spacing: 10) {
                                    Text(section.title)
                                        .font(Theme.Font.title(18, weight: .heavy))
                                        .foregroundStyle(Theme.Palette.ink)
                                    LazyVGrid(columns: columns, spacing: 10) {
                                        ForEach(section.stories) { story in
                                            Button {
                                                selectedStory = story
                                                Haptics.tap()
                                            } label: {
                                                archiveTile(story)
                                            }
                                            .buttonStyle(.plain)
                                        }
                                    }
                                }
                            }
                        }
                        .padding(Theme.Spacing.lg)
                    }
                }
            }
            .background(Theme.Palette.surfaceAlt.ignoresSafeArea())
            .navigationTitle("Archivio storie")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Fine") { dismiss() }
                }
            }
            .overlay(alignment: .bottom) {
                if let errorMessage {
                    Text(errorMessage)
                        .font(Theme.Font.caption(12, weight: .semibold))
                        .foregroundStyle(.white)
                        .padding(.horizontal, 14)
                        .padding(.vertical, 10)
                        .background(Theme.Palette.densityHigh, in: Capsule())
                        .padding(.bottom, 18)
                }
            }
            .task { await load() }
            .refreshable { await load() }
            .fullScreenCover(item: $selectedStory) { story in
                ArchivedStoryViewer(story: story)
            }
        }
    }

    private func archiveTile(_ story: UserStory) -> some View {
        ZStack(alignment: .bottomLeading) {
            RoundedRectangle(cornerRadius: 18, style: .continuous)
                .fill(Theme.Palette.surface)

            Group {
                if APIClient.shared.mediaURL(from: story.mediaUrl)?.isCloudyVideoURL == true {
                    Rectangle()
                        .fill(Theme.Palette.blue900)
                        .overlay(
                            Image(systemName: "play.fill")
                                .font(.system(size: 26, weight: .heavy))
                                .foregroundStyle(.white)
                                .frame(width: 54, height: 54)
                                .background(.black.opacity(0.32), in: Circle())
                        )
                } else {
                    CachedImage(url: APIClient.shared.mediaURL(from: story.mediaUrl), options: .story)
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .clipped()

            LinearGradient(
                colors: [.black.opacity(0), .black.opacity(0.62)],
                startPoint: .center,
                endPoint: .bottom
            )

            VStack(alignment: .leading, spacing: 3) {
                if let venueName = story.venueName, !venueName.isEmpty {
                    Label(venueName, systemImage: "mappin.circle.fill")
                        .lineLimit(1)
                }
                Text(dateLabel(story.createdAtUtc))
                    .lineLimit(1)
            }
            .font(Theme.Font.caption(10, weight: .heavy))
            .foregroundStyle(.white)
            .padding(8)
        }
        .aspectRatio(9 / 16, contentMode: .fit)
        .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 16, style: .continuous)
                .stroke(Theme.Palette.hairline.opacity(0.8), lineWidth: 1)
        )
    }

    private var archiveSections: [(title: String, stories: [UserStory])] {
        let grouped = Dictionary(grouping: stories) { story in
            monthTitle(story.createdAtUtc)
        }
        return grouped
            .map { (title: $0.key, stories: $0.value.sorted { $0.createdAtUtc > $1.createdAtUtc }) }
            .sorted { lhs, rhs in
                (lhs.stories.first?.createdAtUtc ?? .distantPast) > (rhs.stories.first?.createdAtUtc ?? .distantPast)
            }
    }

    private func load() async {
        isLoading = true
        defer { isLoading = false }
        let cached = DeviceCacheService.shared.cachedStories(includeExpired: true, maxAge: 120 * 24 * 60 * 60)
        if !cached.isEmpty && stories.isEmpty {
            stories = cached
        }
        do {
            let loaded = try await API.storyArchive()
            stories = loaded
            DeviceCacheService.shared.cacheStories(loaded)
            errorMessage = nil
        } catch {
            errorMessage = cached.isEmpty
                ? ((error as? LocalizedError)?.errorDescription ?? error.localizedDescription)
                : "Mostro l'archivio salvato sul dispositivo."
        }
    }

    private func dateLabel(_ date: Date) -> String {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "it_IT")
        formatter.setLocalizedDateFormatFromTemplate("d MMM")
        return formatter.string(from: date)
    }

    private func monthTitle(_ date: Date) -> String {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "it_IT")
        formatter.setLocalizedDateFormatFromTemplate("MMMM yyyy")
        return formatter.string(from: date).capitalized
    }
}

private struct ArchivedStoryViewer: View {
    let story: UserStory
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        ZStack {
            Color.black.ignoresSafeArea()

            GeometryReader { geometry in
                if let url = APIClient.shared.mediaURL(from: story.mediaUrl), url.isCloudyVideoURL {
                    ArchiveStoryVideoPlayer(url: url)
                        .frame(width: geometry.size.width, height: geometry.size.height)
                        .clipped()
                } else {
                    CachedImage(url: APIClient.shared.mediaURL(from: story.mediaUrl), options: .story)
                        .frame(width: geometry.size.width, height: geometry.size.height)
                        .clipped()
                }
            }
            .ignoresSafeArea()

            VStack(spacing: 0) {
                HStack {
                    VStack(alignment: .leading, spacing: 2) {
                        Text("Solo tu")
                            .font(Theme.Font.caption(11, weight: .heavy))
                            .foregroundStyle(.white.opacity(0.74))
                        Text(archiveDate(story.createdAtUtc))
                            .font(Theme.Font.body(15, weight: .heavy))
                            .foregroundStyle(.white)
                    }
                    Spacer()
                    Button {
                        dismiss()
                    } label: {
                        Image(systemName: "xmark")
                            .font(.system(size: 16, weight: .bold))
                            .foregroundStyle(.white)
                            .frame(width: 38, height: 38)
                            .background(.black.opacity(0.28), in: Circle())
                    }
                }
                .padding(.horizontal, 16)
                .padding(.top, 18)

                Spacer()

                if let caption = story.caption, !caption.isEmpty {
                    Text(caption)
                        .font(Theme.Font.body(16, weight: .bold))
                        .foregroundStyle(.white)
                        .shadow(color: .black.opacity(0.6), radius: 6)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(.horizontal, 18)
                        .padding(.bottom, 30)
                }
            }
            .background(
                LinearGradient(
                    colors: [.black.opacity(0.45), .black.opacity(0), .black.opacity(0.45)],
                    startPoint: .top,
                    endPoint: .bottom
                )
                .ignoresSafeArea()
            )
        }
        .statusBar(hidden: true)
    }

    private func archiveDate(_ date: Date) -> String {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "it_IT")
        formatter.dateStyle = .medium
        formatter.timeStyle = .short
        return formatter.string(from: date)
    }
}

private struct ArchiveStoryVideoPlayer: UIViewRepresentable {
    let url: URL

    func makeUIView(context: Context) -> ArchiveVideoContainerView {
        let view = ArchiveVideoContainerView()
        let player = AVPlayer(url: url)
        view.playerLayer.videoGravity = .resizeAspectFill
        view.playerLayer.player = player
        player.play()
        return view
    }

    func updateUIView(_ uiView: ArchiveVideoContainerView, context: Context) {
        uiView.playerLayer.videoGravity = .resizeAspectFill
    }
}

private final class ArchiveVideoContainerView: UIView {
    override static var layerClass: AnyClass {
        AVPlayerLayer.self
    }

    var playerLayer: AVPlayerLayer {
        layer as! AVPlayerLayer
    }
}
