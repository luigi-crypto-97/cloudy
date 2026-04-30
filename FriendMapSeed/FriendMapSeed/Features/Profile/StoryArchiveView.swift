//
//  StoryArchiveView.swift
//  Cloudy
//

import SwiftUI

struct StoryArchiveView: View {
    @Environment(\.dismiss) private var dismiss
    @State private var stories: [UserStory] = []
    @State private var isLoading = false
    @State private var errorMessage: String?
    @State private var selectedStory: UserStory?

    private let columns = [
        GridItem(.flexible(), spacing: 8),
        GridItem(.flexible(), spacing: 8),
        GridItem(.flexible(), spacing: 8)
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
                        LazyVGrid(columns: columns, spacing: 8) {
                            ForEach(stories) { story in
                                Button {
                                    selectedStory = story
                                    Haptics.tap()
                                } label: {
                                    archiveTile(story)
                                }
                                .buttonStyle(.plain)
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
            AsyncImage(url: APIClient.shared.mediaURL(from: story.mediaUrl)) { phase in
                switch phase {
                case .success(let image):
                    image
                        .resizable()
                        .scaledToFill()
                case .failure:
                    Rectangle()
                        .fill(Theme.Palette.surface)
                        .overlay(Image(systemName: "photo").foregroundStyle(Theme.Palette.inkMuted))
                default:
                    Rectangle()
                        .fill(Theme.Palette.blue50)
                        .overlay(ProgressView().tint(Theme.Palette.blue500))
                }
            }
            .frame(maxWidth: .infinity)
            .aspectRatio(9 / 16, contentMode: .fit)
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
        .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
    }

    private func load() async {
        isLoading = true
        defer { isLoading = false }
        do {
            stories = try await API.storyArchive()
            errorMessage = nil
        } catch {
            errorMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func dateLabel(_ date: Date) -> String {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "it_IT")
        formatter.setLocalizedDateFormatFromTemplate("d MMM")
        return formatter.string(from: date)
    }
}

private struct ArchivedStoryViewer: View {
    let story: UserStory
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        ZStack {
            Color.black.ignoresSafeArea()

            GeometryReader { geometry in
                AsyncImage(url: APIClient.shared.mediaURL(from: story.mediaUrl)) { phase in
                    switch phase {
                    case .success(let image):
                        image
                            .resizable()
                            .scaledToFill()
                            .frame(width: geometry.size.width, height: geometry.size.height)
                            .clipped()
                    case .failure:
                        Image(systemName: "photo")
                            .font(.system(size: 44, weight: .semibold))
                            .foregroundStyle(.white.opacity(0.55))
                            .frame(width: geometry.size.width, height: geometry.size.height)
                    default:
                        ProgressView()
                            .tint(.white)
                            .frame(width: geometry.size.width, height: geometry.size.height)
                    }
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
