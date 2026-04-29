//
//  FeedCards.swift
//  Cloudy
//
//  Card V2 del feed: vive, azionabili, privacy-safe.
//

import SwiftUI

struct FeedItemRenderer: View {
    let item: FeedItem
    let rank: Int
    var onCTA: (FeedItem, FeedCTA) -> Void
    var onPrivacy: (FeedItem) -> Void
    var onImpression: (FeedItem, Int) -> Void

    var body: some View {
        Group {
            switch item.payload {
            case .hotspotVenue(let payload):
                HotspotVenueCard(item: item, payload: payload, onCTA: onCTA, onPrivacy: onPrivacy)
            case .friendsActivity(let payload):
                FriendsActivityCard(item: item, payload: payload, onCTA: onCTA, onPrivacy: onPrivacy)
            case .venueStoryStack(let payload):
                VenueStoryStackCard(item: item, payload: payload, onCTA: onCTA, onPrivacy: onPrivacy)
            case .joinableTable(let payload):
                JoinableTableCard(item: item, payload: payload, onCTA: onCTA, onPrivacy: onPrivacy)
            case .flareChain(let payload):
                FlareChainCard(item: item, payload: payload, onCTA: onCTA, onPrivacy: onPrivacy)
            case .arrivalForecast(let payload):
                SocialArrivalForecastCard(item: item, payload: payload, onCTA: onCTA, onPrivacy: onPrivacy)
            case .ghostPing(let payload):
                GhostPingCard(item: item, payload: payload, onCTA: onCTA, onPrivacy: onPrivacy)
            case .empty(let payload):
                FeedEmptyOnboardingCard(item: item, payload: payload, onCTA: onCTA)
            }
        }
        .onAppear { onImpression(item, rank) }
    }
}

// MARK: - Hotspot

private struct HotspotVenueCard: View {
    let item: FeedItem
    let payload: HotspotVenuePayload
    var onCTA: (FeedItem, FeedCTA) -> Void
    var onPrivacy: (FeedItem) -> Void
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        VStack(alignment: .leading, spacing: 15) {
            ZStack(alignment: .bottomLeading) {
                FeedRemoteMedia(urlString: payload.coverImageUrl ?? payload.storyPreviews.first?.mediaUrl, symbol: "mappin.and.ellipse")
                    .frame(height: 196)
                    .clipShape(RoundedRectangle(cornerRadius: 24, style: .continuous))

                LinearGradient(colors: [.clear, .black.opacity(0.70)], startPoint: .top, endPoint: .bottom)
                    .clipShape(RoundedRectangle(cornerRadius: 24, style: .continuous))

                VStack(alignment: .leading, spacing: 9) {
                    HStack {
                        FeedStatusPill(text: payload.pulseCopy, color: stateColor)
                        Spacer()
                        EnergyRing(value: payload.energyScore)
                    }

                    Text(payload.name)
                        .font(Theme.Font.display(28))
                        .foregroundStyle(.white)
                        .lineLimit(2)

                    Text(subtitle)
                        .font(Theme.Font.body(15, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.88))
                        .lineLimit(2)
                }
                .padding(16)
            }
            .overlay(
                RoundedRectangle(cornerRadius: 24, style: .continuous)
                    .stroke(payload.energyScore >= 82 ? Theme.Palette.blue500.opacity(0.55) : Theme.Palette.blue100.opacity(0.7), lineWidth: 1)
            )
            .shadow(color: payload.energyScore >= 82 ? Theme.Palette.blue500.opacity(0.18) : .clear, radius: reduceMotion ? 0 : 22, x: 0, y: 10)

            HStack(spacing: 12) {
                FeedMetric(value: "\(payload.estimatedCrowd)", label: "persone", icon: "person.2.fill")
                FeedMetric(value: "\(payload.friendsHere + payload.friendsArriving)", label: "tuo giro", icon: "sparkles")
                MiniTrend(values: payload.trend, color: stateColor)
                    .frame(height: 42)
                    .frame(maxWidth: .infinity)
            }

            if !payload.friendActivities.isEmpty {
                FeedAvatarCopy(
                    urls: payload.friendActivities.map(\.avatarUrl),
                    text: payload.friendActivities.first?.safeCopy ?? "Il tuo giro si sta muovendo"
                )
            }

            FeedPrivacyButton(envelope: item.privacy) { onPrivacy(item) }
            FeedCTACluster(item: item, onCTA: onCTA, shareText: "Qui si sta accendendo: \(payload.name). Vieni su Cloudy?")
        }
        .padding(16)
        .background(Theme.Palette.surface, in: RoundedRectangle(cornerRadius: 28, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 28, style: .continuous).stroke(Theme.Palette.blue100.opacity(0.70), lineWidth: 1))
        .cardShadow()
    }

    private var subtitle: String {
        if payload.friendsHere > 0 && payload.friendsArriving > 0 {
            return "\(payload.friendsHere) amici qui, \(payload.friendsArriving) in arrivo"
        }
        if payload.friendsArriving > 0 {
            return "\(payload.friendsArriving) amici stanno convergendo"
        }
        if !payload.storyPreviews.isEmpty {
            return "\(payload.storyPreviews.count) stories recenti da questo posto"
        }
        return "\(payload.energyScore)% energia, dato aggregato"
    }

    private var stateColor: Color {
        switch payload.liveState {
        case .almostFull: return Theme.Palette.coral500
        case .hotNow: return Theme.Palette.blue600
        case .growing: return Theme.Palette.mint500
        case .wakingUp: return Theme.Palette.blue400
        }
    }
}

// MARK: - Friends activity

private struct FriendsActivityCard: View {
    let item: FeedItem
    let payload: FriendsActivityPayload
    var onCTA: (FeedItem, FeedCTA) -> Void
    var onPrivacy: (FeedItem) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 13) {
                FeedAvatarStack(urls: payload.avatarUrls, size: 42, maxVisible: 3)
                VStack(alignment: .leading, spacing: 4) {
                    Text(payload.title)
                        .font(Theme.Font.title(20, weight: .heavy))
                        .foregroundStyle(Theme.Palette.ink)
                        .lineLimit(2)
                    Text(payload.subtitle)
                        .font(Theme.Font.body(14, weight: .semibold))
                        .foregroundStyle(Theme.Palette.inkSoft)
                }
                Spacer()
                FeedStatusPill(text: "ora", color: Theme.Palette.mint500)
            }

            FeedPrivacyButton(envelope: item.privacy) { onPrivacy(item) }
            FeedCTACluster(item: item, onCTA: onCTA, shareText: "Il mio giro si sta muovendo su Cloudy.")
        }
        .padding(18)
        .background(Theme.Palette.surface, in: RoundedRectangle(cornerRadius: 24, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 24, style: .continuous).stroke(Theme.Palette.blue100.opacity(0.65), lineWidth: 1))
        .cardShadow()
    }
}

// MARK: - Venue stories

private struct VenueStoryStackCard: View {
    let item: FeedItem
    let payload: VenueStoryStackPayload
    var onCTA: (FeedItem, FeedCTA) -> Void
    var onPrivacy: (FeedItem) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            ZStack(alignment: .bottomLeading) {
                FeedRemoteMedia(urlString: payload.coverMediaUrl, symbol: "photo.stack.fill")
                    .frame(height: 184)
                    .clipShape(RoundedRectangle(cornerRadius: 22, style: .continuous))
                LinearGradient(colors: [.clear, .black.opacity(0.72)], startPoint: .top, endPoint: .bottom)
                    .clipShape(RoundedRectangle(cornerRadius: 22, style: .continuous))
                VStack(alignment: .leading, spacing: 8) {
                    Text("Nuove stories da questo posto")
                        .font(Theme.Font.caption(13, weight: .heavy))
                        .foregroundStyle(.white.opacity(0.86))
                    Text(payload.venueName)
                        .font(Theme.Font.display(26))
                        .foregroundStyle(.white)
                        .lineLimit(2)
                    Text("\(payload.storyCount) contenuti recenti")
                        .font(Theme.Font.body(14, weight: .semibold))
                        .foregroundStyle(.white.opacity(0.84))
                }
                .padding(16)
            }

            HStack(spacing: -8) {
                ForEach(payload.previews.prefix(3)) { story in
                    StoryPreviewCircle(story: story)
                }
                if !payload.friendNames.isEmpty {
                    Text(payload.friendNames.prefix(2).joined(separator: ", "))
                        .font(Theme.Font.caption(12, weight: .heavy))
                        .foregroundStyle(Theme.Palette.inkSoft)
                        .padding(.leading, 16)
                }
                Spacer()
            }

            FeedPrivacyButton(envelope: item.privacy) { onPrivacy(item) }
            FeedCTACluster(item: item, onCTA: onCTA, shareText: "Guarda cosa sta succedendo da \(payload.venueName) su Cloudy.")
        }
        .padding(16)
        .background(Theme.Palette.surface, in: RoundedRectangle(cornerRadius: 26, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 26, style: .continuous).stroke(Theme.Palette.blue100.opacity(0.65), lineWidth: 1))
        .cardShadow()
    }
}

// MARK: - Table

private struct JoinableTableCard: View {
    let item: FeedItem
    let payload: TableSuggestionPayload
    var onCTA: (FeedItem, FeedCTA) -> Void
    var onPrivacy: (FeedItem) -> Void

    var body: some View {
        HStack(alignment: .top, spacing: 16) {
            CapacityRing(value: payload.fillRatio, text: "\(payload.acceptedCount)/\(payload.capacity)")
            VStack(alignment: .leading, spacing: 9) {
                FeedStatusPill(text: payload.fillRatio >= 0.66 ? "Tavolo quasi pieno" : "Tavolo aperto", color: payload.fillRatio >= 0.66 ? Theme.Palette.coral500 : Theme.Palette.blue500)
                Text(payload.title)
                    .font(Theme.Font.title(22, weight: .heavy))
                    .foregroundStyle(Theme.Palette.ink)
                    .lineLimit(2)
                Text("\(payload.venueName) · \(time(payload.startsAt))")
                    .font(Theme.Font.body(14, weight: .semibold))
                    .foregroundStyle(Theme.Palette.inkSoft)
                FeedPrivacyButton(envelope: item.privacy) { onPrivacy(item) }
                FeedCTACluster(item: item, onCTA: onCTA, shareText: "Tavolo quasi pieno da \(payload.venueName), join?")
            }
        }
        .padding(18)
        .background(Theme.Palette.surface, in: RoundedRectangle(cornerRadius: 24, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 24, style: .continuous).stroke(Theme.Palette.blue100.opacity(0.65), lineWidth: 1))
        .cardShadow()
    }

    private func time(_ date: Date) -> String {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "it_IT")
        formatter.dateFormat = "HH:mm"
        return formatter.string(from: date)
    }
}

// MARK: - Flare

private struct FlareChainCard: View {
    let item: FeedItem
    let payload: FlareChainPayload
    var onCTA: (FeedItem, FeedCTA) -> Void
    var onPrivacy: (FeedItem) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 13) {
                CountdownRing(seconds: payload.remainingSeconds(), totalSeconds: max(1, Int(payload.expiresAt.timeIntervalSince(payload.createdAt))))
                VStack(alignment: .leading, spacing: 5) {
                    Text("Flare da rilanciare")
                        .font(Theme.Font.caption(13, weight: .heavy))
                        .foregroundStyle(Theme.Palette.blue600)
                    Text(payload.message)
                        .font(Theme.Font.title(22, weight: .heavy))
                        .foregroundStyle(Theme.Palette.ink)
                        .lineLimit(2)
                    Text("\(payload.responseCount) risposte · \(payload.zoneLabel)")
                        .font(Theme.Font.body(14, weight: .semibold))
                        .foregroundStyle(Theme.Palette.inkSoft)
                }
                Spacer()
            }
            FeedPrivacyButton(envelope: item.privacy) { onPrivacy(item) }
            FeedCTACluster(item: item, onCTA: onCTA, shareText: "Rilancia questo flare su Cloudy: \(payload.message)")
        }
        .padding(18)
        .background(
            LinearGradient(colors: [Theme.Palette.blue50, Theme.Palette.surface], startPoint: .topLeading, endPoint: .bottomTrailing),
            in: RoundedRectangle(cornerRadius: 24, style: .continuous)
        )
        .overlay(RoundedRectangle(cornerRadius: 24, style: .continuous).stroke(Theme.Palette.blue100.opacity(0.75), lineWidth: 1))
        .cardShadow()
    }
}

// MARK: - Forecast / Ghost / Empty

private struct SocialArrivalForecastCard: View {
    let item: FeedItem
    let payload: ArrivalForecastPayload
    var onCTA: (FeedItem, FeedCTA) -> Void
    var onPrivacy: (FeedItem) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack {
                VStack(alignment: .leading, spacing: 5) {
                    Text("Tra \(payload.minutesUntilPeak) min sarete in \(payload.expectedPeople)")
                        .font(Theme.Font.title(22, weight: .heavy))
                        .foregroundStyle(Theme.Palette.ink)
                    Text(payload.venueName)
                        .font(Theme.Font.body(14, weight: .semibold))
                        .foregroundStyle(Theme.Palette.inkSoft)
                }
                Spacer()
                FeedStatusPill(text: "quasi qui", color: Theme.Palette.mint500)
            }
            HStack(spacing: 8) {
                ForEach(payload.buckets, id: \.label) { bucket in
                    FeedBucketChip(label: bucket.label, count: bucket.count)
                }
            }
            FeedPrivacyButton(envelope: item.privacy) { onPrivacy(item) }
            FeedCTACluster(item: item, onCTA: onCTA, shareText: "Il gruppo sta convergendo da \(payload.venueName).")
        }
        .padding(18)
        .background(Theme.Palette.surface, in: RoundedRectangle(cornerRadius: 24, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 24, style: .continuous).stroke(Theme.Palette.blue100.opacity(0.65), lineWidth: 1))
        .cardShadow()
    }
}

private struct GhostPingCard: View {
    let item: FeedItem
    let payload: GhostPingPayload
    var onCTA: (FeedItem, FeedCTA) -> Void
    var onPrivacy: (FeedItem) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 13) {
            HStack(spacing: 12) {
                Image(systemName: "eye.slash.fill")
                    .font(.system(size: 22, weight: .heavy))
                    .foregroundStyle(Theme.Palette.blue500)
                    .frame(width: 52, height: 52)
                    .background(Circle().fill(Theme.Palette.blue50))
                VStack(alignment: .leading, spacing: 4) {
                    Text(payload.title)
                        .font(Theme.Font.title(21, weight: .heavy))
                        .foregroundStyle(Theme.Palette.ink)
                    Text(payload.subtitle)
                        .font(Theme.Font.body(14, weight: .semibold))
                        .foregroundStyle(Theme.Palette.inkSoft)
                }
                Spacer()
            }
            FeedPrivacyButton(envelope: item.privacy) { onPrivacy(item) }
            FeedCTACluster(item: item, onCTA: onCTA, shareText: "C'e movimento nel mio giro su Cloudy.")
        }
        .padding(18)
        .background(
            RadialGradient(colors: [Theme.Palette.blue100.opacity(0.8), Theme.Palette.surface], center: .topLeading, startRadius: 10, endRadius: 260),
            in: RoundedRectangle(cornerRadius: 24, style: .continuous)
        )
        .overlay(RoundedRectangle(cornerRadius: 24, style: .continuous).stroke(Theme.Palette.blue100.opacity(0.72), lineWidth: 1))
        .cardShadow()
    }
}

private struct FeedEmptyOnboardingCard: View {
    let item: FeedItem
    let payload: EmptyFeedPayload
    var onCTA: (FeedItem, FeedCTA) -> Void

    var body: some View {
        VStack(spacing: 14) {
            Image(systemName: "sparkles")
                .font(.system(size: 38, weight: .heavy))
                .foregroundStyle(Theme.Palette.blue500)
            Text(payload.title)
                .font(Theme.Font.display(25))
                .foregroundStyle(Theme.Palette.ink)
                .multilineTextAlignment(.center)
            Text(payload.message)
                .font(Theme.Font.body(15, weight: .semibold))
                .foregroundStyle(Theme.Palette.inkSoft)
                .multilineTextAlignment(.center)
            FeedCTACluster(item: item, onCTA: onCTA, shareText: nil)
        }
        .frame(maxWidth: .infinity)
        .padding(22)
        .background(Theme.Palette.surface, in: RoundedRectangle(cornerRadius: 26, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 26, style: .continuous).stroke(Theme.Palette.blue100.opacity(0.65), lineWidth: 1))
        .cardShadow()
    }
}

// MARK: - Shared atoms

private struct FeedCTACluster: View {
    let item: FeedItem
    var onCTA: (FeedItem, FeedCTA) -> Void
    let shareText: String?

    var body: some View {
        HStack(spacing: 9) {
            ForEach(Array(item.ctas.prefix(3).enumerated()), id: \.element.id) { index, cta in
                if index == 0 {
                    Button {
                        Haptics.tap()
                        onCTA(item, cta)
                    } label: {
                        Label(cta.title, systemImage: cta.systemImage)
                            .lineLimit(1)
                            .frame(maxWidth: .infinity)
                    }
                    .font(Theme.Font.caption(12, weight: .heavy))
                    .buttonStyle(.honeyCompact)
                } else {
                    Button {
                        Haptics.tap()
                        onCTA(item, cta)
                    } label: {
                        Label(cta.title, systemImage: cta.systemImage)
                            .lineLimit(1)
                            .frame(maxWidth: .infinity)
                    }
                    .font(Theme.Font.caption(12, weight: .heavy))
                    .buttonStyle(.ghost)
                }
            }
            if let shareText {
                ShareLink(item: shareText) {
                    Image(systemName: "square.and.arrow.up")
                        .font(.system(size: 14, weight: .heavy))
                        .foregroundStyle(Theme.Palette.blue600)
                        .frame(width: 40, height: 40)
                        .background(Theme.Palette.blue50, in: Circle())
                }
                .simultaneousGesture(TapGesture().onEnded {
                    Haptics.tap()
                })
            }
        }
    }
}

private struct FeedPrivacyButton: View {
    let envelope: FeedPrivacyEnvelope
    var onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            Label(envelope.explanation, systemImage: "lock.shield.fill")
                .font(Theme.Font.caption(11, weight: .semibold))
                .foregroundStyle(Theme.Palette.inkMuted)
                .lineLimit(1)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
        .buttonStyle(.plain)
    }
}

private struct FeedRemoteMedia: View {
    let urlString: String?
    let symbol: String

    var body: some View {
        ZStack {
            if let url = APIClient.shared.mediaURL(from: urlString) {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .success(let image):
                        image.resizable().scaledToFill()
                    default:
                        fallback
                    }
                }
            } else {
                fallback
            }
        }
    }

    private var fallback: some View {
        ZStack {
            LinearGradient(colors: [Theme.Palette.blue100, Theme.Palette.blue500], startPoint: .topLeading, endPoint: .bottomTrailing)
            Image(systemName: symbol)
                .font(.system(size: 46, weight: .heavy))
                .foregroundStyle(.white.opacity(0.34))
        }
    }
}

private struct FeedStatusPill: View {
    let text: String
    let color: Color

    var body: some View {
        Text(text)
            .font(Theme.Font.caption(12, weight: .heavy))
            .foregroundStyle(color)
            .padding(.horizontal, 10)
            .padding(.vertical, 6)
            .background(color.opacity(0.12), in: Capsule())
    }
}

private struct FeedMetric: View {
    let value: String
    let label: String
    let icon: String

    var body: some View {
        HStack(spacing: 7) {
            Image(systemName: icon)
                .font(.system(size: 13, weight: .heavy))
                .foregroundStyle(Theme.Palette.blue500)
            VStack(alignment: .leading, spacing: 1) {
                Text(value)
                    .font(Theme.Font.heroNumber(18).monospacedDigit())
                    .foregroundStyle(Theme.Palette.ink)
                    .contentTransition(.numericText())
                Text(label)
                    .font(Theme.Font.caption(10, weight: .semibold))
                    .foregroundStyle(Theme.Palette.inkMuted)
            }
        }
        .padding(10)
        .background(Theme.Palette.surfaceAlt, in: RoundedRectangle(cornerRadius: 15, style: .continuous))
    }
}

private struct FeedAvatarCopy: View {
    let urls: [String?]
    let text: String

    var body: some View {
        HStack(spacing: 11) {
            FeedAvatarStack(urls: urls, size: 34, maxVisible: 3)
            Text(text)
                .font(Theme.Font.body(13, weight: .semibold))
                .foregroundStyle(Theme.Palette.inkSoft)
                .lineLimit(1)
            Spacer()
        }
    }
}

private struct FeedAvatarStack: View {
    let urls: [String?]
    let size: CGFloat
    let maxVisible: Int

    var body: some View {
        HStack(spacing: -9) {
            ForEach(Array(urls.prefix(maxVisible).enumerated()), id: \.offset) { index, raw in
                StoryAvatar(
                    url: APIClient.shared.mediaURL(from: raw),
                    size: size,
                    hasStory: false,
                    initials: "\(index + 1)"
                )
                .overlay(Circle().stroke(Theme.Palette.surface, lineWidth: 2))
            }
            if urls.count > maxVisible {
                Text("+\(urls.count - maxVisible)")
                    .font(Theme.Font.caption(11, weight: .black))
                    .foregroundStyle(Theme.Palette.blue700)
                    .frame(width: size, height: size)
                    .background(Circle().fill(Theme.Palette.blue50))
                    .overlay(Circle().stroke(Theme.Palette.surface, lineWidth: 2))
            }
        }
    }
}

private struct StoryPreviewCircle: View {
    let story: FeedStoryPreview

    var body: some View {
        ZStack {
            if let url = APIClient.shared.mediaURL(from: story.mediaUrl) {
                AsyncImage(url: url) { phase in
                    switch phase {
                    case .success(let image):
                        image.resizable().scaledToFill()
                    default:
                        Theme.Palette.blue50
                    }
                }
            } else {
                StoryAvatar(
                    url: APIClient.shared.mediaURL(from: story.avatarUrl),
                    size: 44,
                    hasStory: false,
                    initials: String(story.displayName.prefix(1)).uppercased()
                )
            }
        }
        .frame(width: 44, height: 44)
        .clipShape(Circle())
        .overlay(Circle().stroke(Theme.Palette.surface, lineWidth: 3))
    }
}

private struct EnergyRing: View {
    let value: Int

    var body: some View {
        ZStack {
            Circle().stroke(.white.opacity(0.22), lineWidth: 7)
            Circle()
                .trim(from: 0, to: CGFloat(min(value, 100)) / 100)
                .stroke(Theme.Palette.blue500, style: StrokeStyle(lineWidth: 7, lineCap: .round))
                .rotationEffect(.degrees(-90))
            VStack(spacing: 0) {
                Text("\(value)")
                    .font(.system(size: 25, weight: .black, design: .rounded))
                    .foregroundStyle(.white)
                    .contentTransition(.numericText())
                Text("%")
                    .font(Theme.Font.caption(10, weight: .heavy))
                    .foregroundStyle(.white.opacity(0.7))
            }
        }
        .frame(width: 74, height: 74)
    }
}

private struct CapacityRing: View {
    let value: Double
    let text: String

    var body: some View {
        ZStack {
            Circle().fill(Theme.Palette.blue50)
            Circle()
                .trim(from: 0, to: value)
                .stroke(Theme.Palette.blue500, style: StrokeStyle(lineWidth: 7, lineCap: .round))
                .rotationEffect(.degrees(-90))
                .padding(4)
            Text(text)
                .font(Theme.Font.caption(13, weight: .black))
                .foregroundStyle(Theme.Palette.blue700)
        }
        .frame(width: 72, height: 72)
    }
}

private struct CountdownRing: View {
    let seconds: Int
    let totalSeconds: Int

    var body: some View {
        let ratio = totalSeconds == 0 ? 0 : Double(seconds) / Double(totalSeconds)
        ZStack {
            Circle().fill(Theme.Palette.blue50)
            Circle()
                .trim(from: 0, to: ratio)
                .stroke(seconds < 600 ? Theme.Palette.coral500 : Theme.Palette.blue500, style: StrokeStyle(lineWidth: 7, lineCap: .round))
                .rotationEffect(.degrees(-90))
                .padding(4)
            VStack(spacing: 0) {
                Text("\(max(0, seconds / 60))")
                    .font(Theme.Font.heroNumber(20).monospacedDigit())
                    .foregroundStyle(Theme.Palette.ink)
                Text("min")
                    .font(Theme.Font.caption(9, weight: .heavy))
                    .foregroundStyle(Theme.Palette.inkMuted)
            }
        }
        .frame(width: 72, height: 72)
    }
}

private struct FeedBucketChip: View {
    let label: String
    let count: Int

    var body: some View {
        HStack(spacing: 5) {
            Text("\(count)")
                .font(Theme.Font.heroNumber(15).monospacedDigit())
            Text(label)
                .font(Theme.Font.caption(11, weight: .heavy))
        }
        .foregroundStyle(Theme.Palette.blue700)
        .padding(.horizontal, 10)
        .padding(.vertical, 7)
        .background(Theme.Palette.blue50, in: Capsule())
    }
}

private struct MiniTrend: View {
    let values: [Int]
    let color: Color

    var body: some View {
        GeometryReader { proxy in
            let points = pathPoints(size: proxy.size)
            Path { path in
                guard let first = points.first else { return }
                path.move(to: first)
                for point in points.dropFirst() {
                    path.addLine(to: point)
                }
            }
            .stroke(color, style: StrokeStyle(lineWidth: 3, lineCap: .round, lineJoin: .round))
        }
        .padding(10)
        .background(Theme.Palette.surfaceAlt, in: RoundedRectangle(cornerRadius: 15, style: .continuous))
        .accessibilityHidden(true)
    }

    private func pathPoints(size: CGSize) -> [CGPoint] {
        guard let minValue = values.min(), let maxValue = values.max(), values.count > 1 else { return [] }
        let span = Swift.max(maxValue - minValue, 1)
        return values.enumerated().map { index, value in
            let x = CGFloat(index) / CGFloat(values.count - 1) * size.width
            let normalized = CGFloat(value - minValue) / CGFloat(span)
            let y = size.height - normalized * size.height
            return CGPoint(x: x, y: y)
        }
    }
}

#Preview("Feed Cards") {
    let context = FeedDemoFactory.context()
    let items = FeedRankingService().rankedItems(context: context)
    ScrollView {
        LazyVStack(spacing: 16) {
            ForEach(Array(items.enumerated()), id: \.element.id) { index, item in
                FeedItemRenderer(item: item, rank: index, onCTA: { _, _ in }, onPrivacy: { _ in }, onImpression: { _, _ in })
                    .padding(.horizontal)
            }
        }
        .padding(.vertical)
    }
    .background(Theme.Palette.appBackground)
}
