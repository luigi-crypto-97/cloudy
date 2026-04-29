//
//  FeedRankingService.swift
//  Cloudy
//
//  Ranking del feed decisionale. Non e cronologico: favorisce luoghi dove
//  stanno succedendo cose utili per decidere dove andare adesso.
//

import Foundation

struct FeedRankingService {
    struct Weights {
        var friendsHere: Double = 22
        var friendsArriving: Double = 18
        var growth: Double = 1.25
        var stories: Double = 9
        var crowd: Double = 0.85
        var energy: Double = 1.1
        var distancePenaltyPerKm: Double = 7
    }

    var weights = Weights()

    func rankedItems(
        hub: SocialHub?,
        stories: [UserStory],
        userDistanceProvider: ((VenueFeedCard) -> Double?)? = nil
    ) -> [FeedItem] {
        let cards = buildVenueCards(hub: hub, stories: stories, userDistanceProvider: userDistanceProvider)
        let rankedCards = cards.sorted { score($0) > score($1) }
        return interleaveSocialMoments(with: rankedCards)
    }

    func score(_ card: VenueFeedCard) -> Double {
        // Formula semplice e volutamente leggibile:
        // - amici presenti e in arrivo pesano piu del crowd anonimo
        // - growth/trend premia luoghi che stanno salendo ora
        // - stories recenti danno prova visiva del momento
        // - distanza penalizza solo se disponibile; oggi il feed resta privacy-safe.
        let distanceKm = (card.distanceMeters ?? 0) / 1000
        let distancePenalty = card.distanceMeters == nil ? 0 : distanceKm * weights.distancePenaltyPerKm
        return Double(card.friendsHere) * weights.friendsHere
            + Double(card.friendsArriving) * weights.friendsArriving
            + Double(card.growthScore) * weights.growth
            + Double(card.storyPreviews.count) * weights.stories
            + Double(card.estimatedCrowd) * weights.crowd
            + Double(card.energyScore) * weights.energy
            - distancePenalty
    }

    private func buildVenueCards(
        hub: SocialHub?,
        stories: [UserStory],
        userDistanceProvider: ((VenueFeedCard) -> Double?)?
    ) -> [VenueFeedCard] {
        var grouped = [String: VenueFeedDraft]()

        for friend in hub?.friends ?? [] {
            guard let venueName = friend.currentVenueName, !venueName.isEmpty else { continue }
            var draft = grouped[venueName] ?? VenueFeedDraft(name: venueName, category: friend.currentVenueCategory)
            let isArriving = friend.presenceState == "intention" || friend.presenceState == "going" || friend.statusLabel.lowercased().contains("arriv")
            if isArriving {
                draft.friendsArriving += 1
                draft.activities.append(FriendActivity(
                    id: "going-\(friend.userId.uuidString)-\(venueName)",
                    userId: friend.userId,
                    displayName: friend.displayName ?? friend.nickname,
                    avatarUrl: friend.avatarUrl,
                    kind: .going,
                    createdAt: Date(),
                    privacyLevel: .friendsFuzzy
                ))
            } else {
                draft.friendsHere += 1
                draft.activities.append(FriendActivity(
                    id: "here-\(friend.userId.uuidString)-\(venueName)",
                    userId: friend.userId,
                    displayName: friend.displayName ?? friend.nickname,
                    avatarUrl: friend.avatarUrl,
                    kind: .arrived,
                    createdAt: Date(),
                    privacyLevel: .friendsFuzzy
                ))
            }
            grouped[venueName] = draft
        }

        for story in stories {
            guard let venueName = story.venueName, !venueName.isEmpty else { continue }
            var draft = grouped[venueName] ?? VenueFeedDraft(name: venueName, category: nil, venueId: story.venueId)
            draft.venueId = draft.venueId ?? story.venueId
            draft.stories.append(FeedStoryPreview(
                id: story.id,
                userId: story.userId,
                displayName: story.displayName ?? story.nickname,
                avatarUrl: story.avatarUrl,
                mediaUrl: story.mediaUrl,
                caption: story.caption,
                createdAt: story.createdAtUtc
            ))
            draft.activities.append(FriendActivity(
                id: "story-\(story.id.uuidString)",
                userId: story.userId,
                displayName: story.displayName ?? story.nickname,
                avatarUrl: story.avatarUrl,
                kind: .postedStory,
                createdAt: story.createdAtUtc,
                privacyLevel: .friendsFuzzy
            ))
            grouped[venueName] = draft
        }

        var cards = grouped.values.map { draft in
            let crowd = max(draft.friendsHere + draft.friendsArriving, draft.stories.count)
            let growth = min(100, draft.friendsArriving * 18 + draft.stories.count * 11 + draft.activities.count * 6)
            let energy = min(100, 28 + crowd * 12 + growth / 2)
            let card = VenueFeedCard(
                id: draft.venueId?.uuidString ?? "venue-\(draft.name.lowercased())",
                venueId: draft.venueId,
                name: draft.name,
                category: draft.category,
                liveState: liveState(energy: energy, growth: growth, crowd: crowd),
                energyScore: energy,
                estimatedCrowd: crowd,
                friendsHere: draft.friendsHere,
                friendsArriving: draft.friendsArriving,
                growthScore: growth,
                distanceMeters: nil,
                trend: trend(seed: draft.name, energy: energy, growth: growth),
                friendActivities: Array(draft.activities.sorted { $0.createdAt > $1.createdAt }.prefix(4)),
                storyPreviews: Array(draft.stories.sorted { $0.createdAt > $1.createdAt }.prefix(4)),
                privacyLevel: .friendsFuzzy
            )

            if let distance = userDistanceProvider?(card) {
                return VenueFeedCard(
                    id: card.id,
                    venueId: card.venueId,
                    name: card.name,
                    category: card.category,
                    liveState: card.liveState,
                    energyScore: card.energyScore,
                    estimatedCrowd: card.estimatedCrowd,
                    friendsHere: card.friendsHere,
                    friendsArriving: card.friendsArriving,
                    growthScore: card.growthScore,
                    distanceMeters: distance,
                    trend: card.trend,
                    friendActivities: card.friendActivities,
                    storyPreviews: card.storyPreviews,
                    privacyLevel: card.privacyLevel
                )
            }
            return card
        }

        if cards.isEmpty {
            cards = Self.mockVenueCards
        }

        return cards
    }

    private func interleaveSocialMoments(with cards: [VenueFeedCard]) -> [FeedItem] {
        var items: [FeedItem] = []
        for (index, card) in cards.enumerated() {
            items.append(.venue(card))
            guard index % 2 == 0 else { continue }
            if let moment = socialMoment(for: card) {
                items.append(.social(moment))
            }
        }
        return items
    }

    private func socialMoment(for card: VenueFeedCard) -> SocialMomentCard? {
        if card.friendsArriving >= 2 {
            return SocialMomentCard(
                id: "moment-converge-\(card.id)",
                title: "Il tuo giro si sta concentrando",
                subtitle: "\(card.friendsArriving) amici stanno puntando \(card.name). Conviene decidere ora.",
                venueName: card.name,
                avatarUrls: card.friendActivities.map(\.avatarUrl),
                actionTitle: "Vai alla mappa",
                privacyLevel: .aggregated
            )
        }
        if let story = card.storyPreviews.first {
            return SocialMomentCard(
                id: "moment-story-\(story.id.uuidString)",
                title: "\(story.displayName) ha acceso il posto",
                subtitle: story.caption ?? "Nuova foto da \(card.name).",
                venueName: card.name,
                avatarUrls: [story.avatarUrl],
                actionTitle: "Apri storia",
                privacyLevel: .friendsFuzzy
            )
        }
        return nil
    }

    private func liveState(energy: Int, growth: Int, crowd: Int) -> VenueLiveState {
        if energy >= 82 || crowd >= 7 { return .almostFull }
        if energy >= 66 { return .hotNow }
        if growth >= 35 { return .growing }
        return .wakingUp
    }

    private func trend(seed: String, energy: Int, growth: Int) -> [Int] {
        let base = max(14, min(72, energy - growth / 3))
        return (0..<8).map { index in
            let wave = abs((seed.hashValue + index * 17) % 13)
            return min(100, max(6, base + index * max(1, growth / 12) + wave))
        }
    }

    static let mockVenueCards: [VenueFeedCard] = [
        VenueFeedCard(
            id: "mock-fellini",
            venueId: nil,
            name: "Ape al Fellini?",
            category: "Bar",
            liveState: .hotNow,
            energyScore: 78,
            estimatedCrowd: 9,
            friendsHere: 2,
            friendsArriving: 3,
            growthScore: 64,
            distanceMeters: nil,
            trend: [22, 28, 35, 48, 55, 63, 70, 78],
            friendActivities: [
                FriendActivity(id: "mock-a", userId: nil, displayName: "Marco", avatarUrl: nil, kind: .arrived, createdAt: Date(), privacyLevel: .mock),
                FriendActivity(id: "mock-b", userId: nil, displayName: "Giulia", avatarUrl: nil, kind: .postedStory, createdAt: Date(), privacyLevel: .mock)
            ],
            storyPreviews: [],
            privacyLevel: .mock
        ),
        VenueFeedCard(
            id: "mock-rooftop",
            venueId: nil,
            name: "Rooftop Centrale",
            category: "Cocktail",
            liveState: .growing,
            energyScore: 61,
            estimatedCrowd: 5,
            friendsHere: 0,
            friendsArriving: 4,
            growthScore: 58,
            distanceMeters: nil,
            trend: [12, 18, 21, 31, 38, 44, 53, 61],
            friendActivities: [
                FriendActivity(id: "mock-c", userId: nil, displayName: "Alcuni amici", avatarUrl: nil, kind: .groupConverging(count: 4), createdAt: Date(), privacyLevel: .mock)
            ],
            storyPreviews: [],
            privacyLevel: .mock
        )
    ]
}

private struct VenueFeedDraft {
    let name: String
    var category: String?
    var venueId: UUID?
    var friendsHere = 0
    var friendsArriving = 0
    var activities: [FriendActivity] = []
    var stories: [FeedStoryPreview] = []
}
