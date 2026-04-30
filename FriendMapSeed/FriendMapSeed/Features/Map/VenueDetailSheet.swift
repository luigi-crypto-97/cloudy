//
//  VenueDetailSheet.swift
//  Cloudy — Bottom sheet di dettaglio venue (Bumble-like card)
//

import SwiftUI

struct VenueDetailSheet: View {
    let venue: VenueMarker
    @Environment(\.dismiss) private var dismiss
    @Environment(AuthStore.self) private var auth
    @State private var actionMessage: String?
    @State private var isSubmittingAction = false
    @State private var showsCreateStory = false
    @State private var showsCreateTable = false
    @State private var showsVenueChat = false
    @State private var venueStories: [VenueStory] = []
    @State private var selectedStoryRoute: VenueStoryViewerRoute?
    @State private var ratingSummary: VenueRatingSummary?
    @State private var ratingReviews: [VenueRatingReview] = []
    @State private var isSubmittingRating = false

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Theme.Spacing.lg) {

                // Hero
                ZStack(alignment: .topTrailing) {
                    AsyncImage(url: URL(string: venue.coverImageUrl ?? "")) { phase in
                        switch phase {
                        case .success(let img):
                            img.resizable().scaledToFill()
                        default:
                            ZStack {
                                Theme.Gradients.honeyCTA
                                Image(systemName: iconForCategory(venue.category))
                                    .font(.system(size: 56, weight: .semibold))
                                    .foregroundStyle(.white.opacity(0.9))
                            }
                        }
                    }
                    .frame(height: 180)
                    .frame(maxWidth: .infinity)
                    .clipped()
                    .clipShape(RoundedRectangle(cornerRadius: Theme.Radius.lg, style: .continuous))

                    // Open badge
                    HStack(spacing: 6) {
                        Circle()
                            .fill(venue.isOpenNow ? Theme.Palette.densityLow : Theme.Palette.densityHigh)
                            .frame(width: 8, height: 8)
                        Text(venue.isOpenNow ? "Aperto ora" : "Chiuso")
                            .font(Theme.Font.caption(11, weight: .bold))
                    }
                    .padding(.horizontal, 10)
                    .padding(.vertical, 6)
                    .background(.thinMaterial, in: Capsule())
                    .padding(10)
                }

                // Title
                VStack(alignment: .leading, spacing: 4) {
                    Text(venue.name)
                        .font(Theme.Font.display(26, weight: .heavy))
                        .foregroundStyle(Theme.Palette.ink)
                    Text("\(venue.category.capitalized) · \(venue.city)")
                        .font(Theme.Font.body())
                        .foregroundStyle(Theme.Palette.inkSoft)
                }

                VenueRatingCard(
                    average: ratingSummary?.averageRating ?? venue.averageRating,
                    count: ratingSummary?.ratingCount ?? venue.ratingCount,
                    myRating: ratingSummary?.myRating ?? venue.myRating,
                    isVerified: ratingSummary?.myRatingIsVerified ?? false,
                    reviews: ratingReviews,
                    isSubmitting: isSubmittingRating,
                    onRate: { stars in
                        Task { await rateVenue(stars: stars) }
                    },
                    onReport: { review in
                        Task { await reportRating(review) }
                    }
                )

                // Density / stats
                HStack(spacing: Theme.Spacing.md) {
                    statTile(icon: "person.2.fill", value: "\(venue.peopleEstimate)", label: "Persone")
                    statTile(icon: "checkmark.circle.fill", value: "\(venue.activeCheckIns)", label: "Check-in")
                    statTile(icon: "hand.raised.fill", value: "\(venue.activeIntentions)", label: "Piani")
                    statTile(icon: "person.3.fill", value: "\(venue.openTables)", label: "Tavoli")
                }

                PartyPulseCard(pulse: venue.partyPulse)

                IntentRadarCard(radar: venue.intentRadar)

                // Density indicator + tags
                VStack(alignment: .leading, spacing: 8) {
                    DensityIndicator(level: venue.densityLevel, count: venue.peopleEstimate)
                    if !venue.tags.isEmpty {
                        ScrollView(.horizontal, showsIndicators: false) {
                            HStack(spacing: 6) {
                                ForEach(venue.tags, id: \.self) { tag in
                                    CloudyPill(text: tag, tone: .neutral)
                                }
                            }
                        }
                    }
                }

                // Description
                if let desc = venue.description, !desc.isEmpty {
                    Text(desc)
                        .font(Theme.Font.body(15))
                        .foregroundStyle(Theme.Palette.ink)
                        .lineSpacing(2)
                }

                // Presence preview (Instagram-like avatars row)
                if !venue.presencePreview.isEmpty {
                    SectionCard {
                        Text("Amici qui ora")
                            .font(Theme.Font.title(16))
                        HStack(spacing: -10) {
                            ForEach(venue.presencePreview.prefix(6)) { p in
                                StoryAvatar(
                                    url: APIClient.shared.mediaURL(from: p.avatarUrl),
                                    size: 44,
                                    hasStory: true,
                                    initials: String(p.displayName.prefix(1))
                                )
                            }
                            if venue.presencePreview.count > 6 {
                                Text("+\(venue.presencePreview.count - 6)")
                                    .font(Theme.Font.caption(13, weight: .bold))
                                    .padding(10)
                                    .background(Circle().fill(Theme.Palette.surfaceAlt))
                            }
                        }
                    }
                }

                if !venueStories.isEmpty {
                    SectionCard {
                        HStack {
                            Text("Foto scattate qui")
                                .font(Theme.Font.title(16))
                            Spacer()
                            Text("\(venueStories.count)")
                                .font(Theme.Font.caption(12, weight: .bold))
                                .foregroundStyle(Theme.Palette.inkMuted)
                        }
                        ScrollView(.horizontal, showsIndicators: false) {
                            HStack(spacing: 10) {
                                ForEach(venueStories) { story in
                                    Button {
                                        selectedStoryRoute = VenueStoryViewerRoute(stories: storyViewerStories(startingFrom: story))
                                    } label: {
                                        VenueStoryThumbnail(story: story)
                                    }
                                    .buttonStyle(.plain)
                                }
                            }
                        }
                    }
                }

                // CTAs
                VStack(spacing: 10) {
                    Button {
                        Task { await checkInNow() }
                    } label: {
                        HStack {
                            Image(systemName: isSubmittingAction ? "hourglass" : "hand.thumbsup.fill")
                            Text("Sono qui ora")
                        }
                        .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.honey)
                    .disabled(isSubmittingAction)

                    Button {
                        Task { await planTonight() }
                    } label: {
                        HStack {
                            Image(systemName: "calendar.badge.plus")
                            Text("Pianifica un'uscita")
                        }
                        .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.ghost)
                    .disabled(isSubmittingAction)

                    Button {
                        showsCreateStory = true
                    } label: {
                        HStack {
                            Image(systemName: "camera.circle.fill")
                            Text("Posta foto qui per 24h")
                        }
                        .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.ghost)

                    Button {
                        showsCreateTable = true
                    } label: {
                        HStack {
                            Image(systemName: "person.3.fill")
                            Text("Crea tavolo sociale")
                        }
                        .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.ghost)

                    Button {
                        showsVenueChat = true
                    } label: {
                        HStack {
                            Image(systemName: "bubble.left.and.bubble.right.fill")
                            Text("Chat del locale")
                        }
                        .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.ghost)

                    if let actionMessage {
                        Text(actionMessage)
                            .font(Theme.Font.caption(12, weight: .semibold))
                            .foregroundStyle(Theme.Palette.inkSoft)
                            .frame(maxWidth: .infinity, alignment: .leading)
                    }
                }
                .padding(.top, 4)

                // Address / contacts
                if !venue.addressLine.isEmpty {
                    rowItem(icon: "mappin.circle.fill", text: "\(venue.addressLine), \(venue.city)")
                }
                if let phone = venue.phoneNumber, !phone.isEmpty {
                    rowItem(icon: "phone.fill", text: phone)
                }
                if let hrs = venue.hoursSummary, !hrs.isEmpty {
                    rowItem(icon: "clock.fill", text: hrs)
                }
            }
            .padding(Theme.Spacing.lg)
        }
        .sheet(isPresented: $showsCreateStory) {
            CreateStoryView(venue: venue) {
                actionMessage = "Storia pubblicata sopra \(venue.name) per 24 ore."
                Task { await loadVenueStories() }
            }
        }
        .sheet(isPresented: $showsCreateTable) {
            CreateSocialTableSheet(venue: venue) { message in
                actionMessage = message
            }
        }
        .sheet(isPresented: $showsVenueChat) {
            NavigationStack {
                GroupChatRoomView(venueId: venue.venueId, title: "Chat di \(venue.name)")
            }
        }
        .fullScreenCover(item: $selectedStoryRoute) { route in
            StoryViewerView(stories: route.stories)
        }
        .task {
            await loadVenueStories()
            await loadRating()
            await loadRatingReviews()
        }
    }

    private func statTile(icon: String, value: String, label: String) -> some View {
        VStack(spacing: 2) {
            Image(systemName: icon)
                .font(.system(size: 16, weight: .bold))
                .foregroundStyle(Theme.Palette.honeyDeep)
            Text(value)
                .font(Theme.Font.title(20, weight: .heavy))
                .foregroundStyle(Theme.Palette.ink)
            Text(label)
                .font(Theme.Font.caption(11))
                .foregroundStyle(Theme.Palette.inkMuted)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 12)
        .background(
            RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                .fill(Theme.Palette.surfaceAlt)
        )
    }

    private func rowItem(icon: String, text: String) -> some View {
        HStack(spacing: 10) {
            Image(systemName: icon)
                .foregroundStyle(Theme.Palette.honeyDeep)
            Text(text)
                .font(Theme.Font.body(14))
                .foregroundStyle(Theme.Palette.ink)
            Spacer()
        }
    }

    private func iconForCategory(_ c: String) -> String {
        switch c.lowercased() {
        case "bar", "pub":            return "wineglass.fill"
        case "restaurant", "ristorante": return "fork.knife"
        case "cafe", "caffè":         return "cup.and.saucer.fill"
        case "club", "discoteca":     return "music.note"
        default:                      return "mappin.and.ellipse"
        }
    }

    private var currentUserId: UUID? {
        if case .loggedIn(let user) = auth.state {
            return user.userId
        }
        return nil
    }

    private func checkInNow() async {
        guard let currentUserId else { return }
        isSubmittingAction = true
        actionMessage = nil
        defer { isSubmittingAction = false }
        do {
            try await API.checkIn(venueId: venue.venueId, userId: currentUserId)
            Haptics.success()
            actionMessage = "Check-in attivo per le prossime ore."
        } catch {
            Haptics.error()
            actionMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func planTonight() async {
        guard let currentUserId else { return }
        let start = Date().addingTimeInterval(60 * 60)
        let end = start.addingTimeInterval(3 * 60 * 60)
        isSubmittingAction = true
        actionMessage = nil
        defer { isSubmittingAction = false }
        do {
            try await API.createIntention(
                venueId: venue.venueId,
                userId: currentUserId,
                startsAtUtc: start,
                endsAtUtc: end,
                note: "Ci sto pensando"
            )
            Haptics.success()
            actionMessage = "Piano aggiunto per stasera."
        } catch {
            Haptics.error()
            actionMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func loadVenueStories() async {
        do {
            venueStories = try await API.venueStories()
                .filter { $0.venueId == venue.venueId }
                .sorted { $0.createdAtUtc > $1.createdAtUtc }
        } catch {
            venueStories = []
        }
    }

    private func loadRating() async {
        ratingSummary = try? await API.venueRating(venueId: venue.venueId)
    }

    private func loadRatingReviews() async {
        ratingReviews = (try? await API.venueRatingReviews(venueId: venue.venueId)) ?? []
    }

    private func rateVenue(stars: Int) async {
        isSubmittingRating = true
        defer { isSubmittingRating = false }
        do {
            ratingSummary = try await API.rateVenue(venueId: venue.venueId, stars: stars)
            await loadRatingReviews()
            Haptics.success()
            if ratingSummary?.myRatingEarnsPoints == true {
                actionMessage = "Valutazione salvata. Se verificata, vale punti classifica."
            } else {
                actionMessage = "Valutazione salvata. I punti si sbloccano dopo un check-in, una story o un piano reale qui."
            }
            NotificationCenter.default.post(name: .cloudyBadgesShouldRefresh, object: nil)
        } catch {
            Haptics.error()
            actionMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func reportRating(_ review: VenueRatingReview) async {
        guard !review.isMine else { return }
        do {
            _ = try await API.reportVenueRating(
                venueId: venue.venueId,
                ratingId: review.ratingId,
                reasonCode: "fake_venue_rating",
                details: "Segnalata da scheda locale iOS."
            )
            ratingReviews.removeAll { $0.id == review.id }
            ratingSummary = try? await API.venueRating(venueId: venue.venueId)
            Haptics.success()
            actionMessage = "Recensione segnalata. Se confermata, comporta perdita punti."
        } catch {
            Haptics.error()
            actionMessage = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func storyViewerStories(startingFrom selected: VenueStory) -> [UserStory] {
        let ordered = venueStories.sorted { lhs, rhs in
            if lhs.id == selected.id { return true }
            if rhs.id == selected.id { return false }
            return lhs.createdAtUtc < rhs.createdAtUtc
        }
        return ordered.map { story in
            UserStory(
                id: story.id,
                userId: story.userId,
                nickname: story.nickname,
                displayName: story.displayName,
                avatarUrl: story.avatarUrl,
                mediaUrl: story.mediaUrl,
                caption: story.caption,
                venueId: story.venueId,
                venueName: story.venueName,
                likeCount: story.likeCount,
                commentCount: story.commentCount,
                hasLiked: story.hasLiked,
                createdAtUtc: story.createdAtUtc,
                expiresAtUtc: story.expiresAtUtc
            )
        }
    }
}

private struct VenueStoryViewerRoute: Identifiable {
    let id = UUID()
    let stories: [UserStory]
}

private struct PartyPulseCard: View {
    let pulse: PartyPulse

    private var energyColor: Color {
        switch pulse.energyScore {
        case 82...: return Theme.Palette.coral500
        case 62...: return Theme.Palette.densityHigh
        case 38...: return Theme.Palette.blue500
        case 18...: return Theme.Palette.mint500
        default: return Theme.Palette.inkMuted
        }
    }

    private var moodLabel: String {
        switch pulse.mood {
        case "peak": return "Sta esplodendo"
        case "rising": return "Si sta accendendo"
        case "alive": return "Vivo"
        case "warming": return "Si scalda"
        default: return "Calmo"
        }
    }

    var body: some View {
        SectionCard {
            HStack(alignment: .top, spacing: 14) {
                ZStack {
                    Circle()
                        .stroke(Theme.Palette.blue100, lineWidth: 8)
                    Circle()
                        .trim(from: 0, to: CGFloat(pulse.energyScore) / 100)
                        .stroke(energyColor, style: StrokeStyle(lineWidth: 8, lineCap: .round))
                        .rotationEffect(.degrees(-90))
                    VStack(spacing: 0) {
                        Text("\(pulse.energyScore)")
                            .font(Theme.Font.heroNumber(24).monospacedDigit())
                            .foregroundStyle(Theme.Palette.ink)
                        Text("pulse")
                            .font(Theme.Font.caption(10, weight: .heavy))
                            .foregroundStyle(Theme.Palette.inkMuted)
                    }
                }
                .frame(width: 76, height: 76)

                VStack(alignment: .leading, spacing: 10) {
                    HStack {
                        VStack(alignment: .leading, spacing: 2) {
                            Text("Party Pulse")
                                .font(Theme.Font.title(17, weight: .heavy))
                            Text(moodLabel)
                                .font(Theme.Font.body(13, weight: .semibold))
                                .foregroundStyle(energyColor)
                        }
                        Spacer()
                        Image(systemName: "waveform.path.ecg")
                            .font(.system(size: 18, weight: .heavy))
                            .foregroundStyle(energyColor)
                    }

                    PulseSparkline(values: pulse.sparkline, tint: energyColor)
                        .frame(height: 34)

                    HStack(spacing: 8) {
                        pulseMetric("\(pulse.arrivalsLast15)", "arrivi 15m")
                        pulseMetric("\(pulse.checkInsNow)", "qui ora")
                        pulseMetric("\(pulse.intentionsSoon)", "in rotta")
                    }
                }
            }
        }
    }

    private func pulseMetric(_ value: String, _ label: String) -> some View {
        VStack(alignment: .leading, spacing: 1) {
            Text(value)
                .font(Theme.Font.heroNumber(17).monospacedDigit())
                .foregroundStyle(Theme.Palette.ink)
            Text(label)
                .font(Theme.Font.caption(10, weight: .semibold))
                .foregroundStyle(Theme.Palette.inkMuted)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.horizontal, 10)
        .padding(.vertical, 8)
        .background(Theme.Palette.surfaceAlt, in: RoundedRectangle(cornerRadius: 13, style: .continuous))
    }
}

private struct PulseSparkline: View {
    let values: [Int]
    let tint: Color

    var body: some View {
        GeometryReader { proxy in
            let points = normalizedPoints(in: proxy.size)
            ZStack {
                RoundedRectangle(cornerRadius: 10, style: .continuous)
                    .fill(Theme.Palette.blue50.opacity(0.7))
                Path { path in
                    guard let first = points.first else { return }
                    path.move(to: first)
                    for point in points.dropFirst() {
                        path.addLine(to: point)
                    }
                }
                .stroke(tint, style: StrokeStyle(lineWidth: 3, lineCap: .round, lineJoin: .round))
            }
        }
    }

    private func normalizedPoints(in size: CGSize) -> [CGPoint] {
        let values = values.isEmpty ? [0, 0, 0, 0, 0] : values
        let maxValue = max(values.max() ?? 1, 1)
        let step = values.count <= 1 ? 0 : size.width / CGFloat(values.count - 1)
        return values.enumerated().map { index, value in
            let x = CGFloat(index) * step
            let y = size.height - (CGFloat(value) / CGFloat(maxValue) * (size.height - 8)) - 4
            return CGPoint(x: x, y: y)
        }
    }
}

private struct IntentRadarCard: View {
    let radar: IntentRadar

    var body: some View {
        SectionCard {
            HStack {
                VStack(alignment: .leading, spacing: 3) {
                    Text("Intent Radar Tonight")
                        .font(Theme.Font.title(17, weight: .heavy))
                    Text("Aggregato e anonimo")
                        .font(Theme.Font.caption(11, weight: .semibold))
                        .foregroundStyle(Theme.Palette.inkMuted)
                }
                Spacer()
                Image(systemName: "scope")
                    .font(.system(size: 20, weight: .heavy))
                    .foregroundStyle(Theme.Palette.blue500)
            }

            HStack(spacing: 8) {
                radarChip(value: radar.goingOut, label: "Escono", icon: "figure.walk")
                radarChip(value: radar.almostThere, label: "In arrivo", icon: "arrow.down.right.circle.fill")
                radarChip(value: radar.hereNow, label: "Qui", icon: "location.fill")
                radarChip(value: radar.coolingDown, label: "Rientro", icon: "moon.fill")
            }
        }
    }

    private func radarChip(value: Int, label: String, icon: String) -> some View {
        VStack(spacing: 5) {
            Image(systemName: icon)
                .font(.system(size: 14, weight: .bold))
                .foregroundStyle(value > 0 ? Theme.Palette.blue500 : Theme.Palette.inkMuted)
            Text("\(value)")
                .font(Theme.Font.heroNumber(18).monospacedDigit())
                .foregroundStyle(Theme.Palette.ink)
                .contentTransition(.numericText())
            Text(label)
                .font(Theme.Font.caption(10, weight: .semibold))
                .foregroundStyle(Theme.Palette.inkMuted)
                .lineLimit(1)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 10)
        .background(
            RoundedRectangle(cornerRadius: 14, style: .continuous)
                .fill(value > 0 ? Theme.Palette.blue50 : Theme.Palette.surfaceAlt)
        )
    }
}

private struct VenueRatingCard: View {
    let average: Double
    let count: Int
    let myRating: Int?
    let isVerified: Bool
    let reviews: [VenueRatingReview]
    let isSubmitting: Bool
    var onRate: (Int) -> Void
    var onReport: (VenueRatingReview) -> Void

    var body: some View {
        SectionCard {
            VStack(alignment: .leading, spacing: 14) {
                HStack(alignment: .center) {
                    VStack(alignment: .leading, spacing: 2) {
                        Text("Stelle del locale")
                            .font(Theme.Font.title(17, weight: .heavy))
                        Text(subtitle)
                            .font(Theme.Font.caption(11, weight: .semibold))
                            .foregroundStyle(Theme.Palette.inkMuted)
                    }
                    Spacer()
                    HStack(spacing: 4) {
                        Image(systemName: "star.fill")
                            .foregroundStyle(Theme.Palette.blue500)
                        Text(average > 0 ? String(format: "%.1f", average) : "—")
                            .font(Theme.Font.heroNumber(18).monospacedDigit())
                            .foregroundStyle(Theme.Palette.ink)
                    }
                }

                HStack(spacing: 8) {
                    ForEach(1...5, id: \.self) { star in
                        Button {
                            guard !isSubmitting else { return }
                            Haptics.tap()
                            onRate(star)
                        } label: {
                            Image(systemName: star <= (myRating ?? 0) ? "star.fill" : "star")
                                .font(.system(size: 25, weight: .heavy))
                                .foregroundStyle(star <= (myRating ?? 0) ? Theme.Palette.blue500 : Theme.Palette.inkMuted.opacity(0.45))
                                .frame(maxWidth: .infinity)
                                .padding(.vertical, 8)
                                .background(Theme.Palette.surfaceAlt, in: RoundedRectangle(cornerRadius: 14, style: .continuous))
                        }
                        .buttonStyle(.plain)
                    }
                }
                .disabled(isSubmitting)

                Divider()
                    .overlay(Theme.Palette.hairline)
                VStack(alignment: .leading, spacing: 10) {
                    HStack {
                        Text("Recensioni degli utenti")
                            .font(Theme.Font.title(15, weight: .heavy))
                        Spacer()
                        Text("\(reviews.count)")
                            .font(Theme.Font.caption(11, weight: .heavy))
                            .foregroundStyle(Theme.Palette.inkMuted)
                    }
                    if reviews.isEmpty {
                        Text("Ancora nessuna recensione pubblica. Le prime recensioni verificate compariranno qui.")
                            .font(Theme.Font.caption(12, weight: .semibold))
                            .foregroundStyle(Theme.Palette.inkMuted)
                            .padding(12)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .background(Theme.Palette.surfaceAlt, in: RoundedRectangle(cornerRadius: 14, style: .continuous))
                    } else {
                        ForEach(reviews.prefix(4)) { review in
                            VenueRatingReviewRow(review: review, onReport: { onReport(review) })
                        }
                    }
                }
            }
        }
    }

    private var subtitle: String {
        if let myRating {
            return isVerified
                ? "La tua valutazione \(myRating)/5 è verificata e vale punti."
                : "La tua valutazione è salvata. Serve un segnale reale qui per i punti."
        }
        if count > 0 {
            return "\(count) valutazioni. Le recensioni false vengono segnalate e penalizzate."
        }
        return "Valuta solo locali che hai vissuto: le recensioni false fanno perdere punti."
    }
}

private struct VenueRatingReviewRow: View {
    let review: VenueRatingReview
    var onReport: () -> Void

    var body: some View {
        HStack(alignment: .top, spacing: 10) {
            StoryAvatar(
                url: APIClient.shared.mediaURL(from: review.avatarUrl),
                size: 34,
                hasStory: false,
                initials: String((review.displayName ?? review.nickname).prefix(1)).uppercased()
            )
            VStack(alignment: .leading, spacing: 5) {
                HStack(spacing: 7) {
                    Text(review.displayName ?? review.nickname)
                        .font(Theme.Font.body(13, weight: .heavy))
                        .foregroundStyle(Theme.Palette.ink)
                        .lineLimit(1)
                    if review.isVerifiedVisit {
                        Label("verificata", systemImage: "checkmark.seal.fill")
                            .font(Theme.Font.caption(10, weight: .heavy))
                            .foregroundStyle(Theme.Palette.mint500)
                    }
                    Spacer()
                    HStack(spacing: 2) {
                        Image(systemName: "star.fill")
                            .font(.system(size: 10, weight: .heavy))
                        Text("\(review.stars)")
                            .font(Theme.Font.caption(11, weight: .heavy))
                    }
                    .foregroundStyle(Theme.Palette.blue500)
                }
                if let comment = review.comment, !comment.isEmpty {
                    Text(comment)
                        .font(Theme.Font.caption(12, weight: .semibold))
                        .foregroundStyle(Theme.Palette.inkSoft)
                        .lineLimit(3)
                } else {
                    Text("Ha lasciato una valutazione.")
                        .font(Theme.Font.caption(12, weight: .semibold))
                        .foregroundStyle(Theme.Palette.inkMuted)
                }
                HStack {
                    Text(review.createdAtUtc, style: .relative)
                        .font(Theme.Font.caption(10, weight: .semibold))
                        .foregroundStyle(Theme.Palette.inkMuted)
                    Spacer()
                    if !review.isMine {
                        Button("Segnala") {
                            Haptics.tap()
                            onReport()
                        }
                        .font(Theme.Font.caption(10, weight: .heavy))
                        .foregroundStyle(Theme.Palette.inkMuted)
                    }
                }
            }
        }
        .padding(10)
        .background(Theme.Palette.surfaceAlt, in: RoundedRectangle(cornerRadius: 16, style: .continuous))
    }
}

private struct VenueStoryThumbnail: View {
    let story: VenueStory

    var body: some View {
        ZStack(alignment: .bottomLeading) {
            Group {
                if let url = APIClient.shared.mediaURL(from: story.mediaUrl), url.isCloudyVideoURL {
                    Rectangle()
                        .fill(LinearGradient(colors: [Theme.Palette.blue100, Theme.Palette.blue500], startPoint: .topLeading, endPoint: .bottomTrailing))
                        .overlay(
                            Image(systemName: "play.fill")
                                .font(Theme.Font.title(24, weight: .bold))
                                .foregroundStyle(.white)
                                .padding(12)
                                .background(.black.opacity(0.28), in: Circle())
                        )
                } else {
                    AsyncImage(url: APIClient.shared.mediaURL(from: story.mediaUrl)) { phase in
                        switch phase {
                        case .success(let image):
                            image.resizable().scaledToFill()
                        default:
                            Rectangle()
                                .fill(Theme.Palette.blue50)
                                .overlay(
                                    Image(systemName: "photo.fill")
                                        .foregroundStyle(Theme.Palette.blue500)
                                )
                        }
                    }
                }
            }
            .frame(width: 86, height: 116)
            .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))

            Text(story.displayName ?? story.nickname)
                .font(Theme.Font.caption(10, weight: .bold))
                .foregroundStyle(.white)
                .lineLimit(1)
                .padding(7)
                .frame(width: 86, alignment: .leading)
                .background(
                    LinearGradient(colors: [.clear, .black.opacity(0.55)], startPoint: .top, endPoint: .bottom)
                        .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
                )
        }
    }
}

private struct CreateSocialTableSheet: View {
    let venue: VenueMarker
    var onCreated: (String) -> Void

    @Environment(\.dismiss) private var dismiss
    @Environment(AuthStore.self) private var auth
    @State private var title = ""
    @State private var description = ""
    @State private var startsAt = Date().addingTimeInterval(60 * 60)
    @State private var capacity = 4
    @State private var joinPolicy = "auto"
    @State private var isSaving = false
    @State private var error: String?

    var body: some View {
        NavigationStack {
            Form {
                Section("Locale") {
                    Text(venue.name)
                    Text(venue.city).foregroundStyle(.secondary)
                }
                Section("Dettagli") {
                    TextField("Titolo", text: $title)
                    TextField("Descrizione opzionale", text: $description, axis: .vertical)
                    DatePicker("Orario", selection: $startsAt, in: Date()..., displayedComponents: [.date, .hourAndMinute])
                    Stepper("Posti: \(capacity)", value: $capacity, in: 2...20)
                    Picker("Ingresso", selection: $joinPolicy) {
                        Text("Chi prima arriva entra").tag("auto")
                        Text("Approvazione host").tag("approval")
                    }
                }
                if let error {
                    Section {
                        Text(error).foregroundStyle(Theme.Palette.densityHigh)
                    }
                }
            }
            .navigationTitle("Nuovo tavolo")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Annulla") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button(isSaving ? "Creo…" : "Crea") {
                        Task { await create() }
                    }
                    .disabled(isSaving || cleanTitle.isEmpty)
                }
            }
        }
        .onAppear {
            if title.isEmpty {
                title = "Tavolo da \(venue.name)"
            }
        }
    }

    private var cleanTitle: String {
        title.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private var currentUserId: UUID? {
        if case .loggedIn(let user) = auth.state {
            return user.userId
        }
        return nil
    }

    private func create() async {
        guard let currentUserId else { return }
        isSaving = true
        error = nil
        defer { isSaving = false }
        do {
            let table = try await API.createTable(CreateSocialTableRequest(
                hostUserId: currentUserId,
                venueId: venue.venueId,
                title: cleanTitle,
                description: description.trimmingCharacters(in: .whitespacesAndNewlines).nilIfEmpty,
                startsAtUtc: startsAt,
                capacity: capacity,
                joinPolicy: joinPolicy
            ))
            Haptics.success()
            onCreated("Tavolo creato: \(table.acceptedCount)/\(table.capacity) posti occupati.")
            dismiss()
        } catch {
            Haptics.error()
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }
}

private extension String {
    var nilIfEmpty: String? {
        isEmpty ? nil : self
    }
}

// MARK: - Filter sheet

struct MapFiltersSheet: View {
    @Environment(MapStore.self) private var store
    @Environment(\.dismiss) private var dismiss
    @State private var query: String = ""
    @State private var category: String = "all"
    @State private var openNow: Bool = false

    private let categories: [(String, String)] = [
        ("all", "Tutti"),
        ("bar", "Bar"),
        ("restaurant", "Ristoranti"),
        ("cafe", "Caffè"),
        ("club", "Locali notturni")
    ]

    var body: some View {
        NavigationStack {
            VStack(alignment: .leading, spacing: Theme.Spacing.lg) {
                Text("Filtri")
                    .font(Theme.Font.display(28))

                TextField("Cerca un posto…", text: $query)
                    .textFieldStyle(.plain)
                    .padding(12)
                    .background(Theme.Palette.surfaceAlt, in: RoundedRectangle(cornerRadius: 12))

                Text("Categoria")
                    .font(Theme.Font.title(16))
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: 8) {
                        ForEach(categories, id: \.0) { (key, label) in
                            FilterChip(
                                label: label,
                                isSelected: category == key,
                                action: { category = key }
                            )
                        }
                    }
                }

                Toggle(isOn: $openNow) {
                    Text("Aperto ora")
                        .font(Theme.Font.body(15, weight: .semibold))
                }
                .tint(Theme.Palette.honey)

                Spacer()

                Button {
                    store.applyFilters(query: query, category: category, openNow: openNow)
                    dismiss()
                } label: {
                    Text("Applica filtri")
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(.honey)
            }
            .padding(Theme.Spacing.lg)
            .onAppear {
                query = store.query
                category = store.category
                openNow = store.openNowOnly
            }
        }
    }
}
