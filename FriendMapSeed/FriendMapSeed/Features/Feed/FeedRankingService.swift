//
//  FeedRankingService.swift
//  Cloudy
//
//  Ranking virale, non cronologico. La formula privilegia social proof,
//  PartyPulse, freshness, prova visiva e azionabilita, con distanza/fatigue
//  come penalizzazioni privacy-safe.
//

import CoreLocation
import Foundation

struct FeedRankingService {
    struct Weights {
        var socialProof: Double = 0.30
        var venueHeat: Double = 0.25
        var freshness: Double = 0.15
        var visualProof: Double = 0.10
        var actionability: Double = 0.15
        var distancePenalty: Double = 0.10
        var fatiguePenalty: Double = 0.20
        var duplicationPenalty: Double = 0.18
    }

    var weights = Weights()

    func rankedItems(context: FeedContext, fatigue: [String: Int] = [:], maxItems: Int = 28) -> [FeedItem] {
        var candidates: [FeedItem] = []
        let privacy = privacyPolicy(from: context.privacyState)

        let storiesByVenue = groupStoriesByVenue(context.stories, venueStories: context.venueStories)
        let friendsByVenueName = groupFriendsByVenueName(context.socialHub?.friends ?? [], privacy: privacy)

        candidates.append(contentsOf: hotspotVenueItems(context: context, storiesByVenue: storiesByVenue, friendsByVenueName: friendsByVenueName, privacy: privacy))
        candidates.append(contentsOf: friendsActivityItems(context: context, friendsByVenueName: friendsByVenueName, privacy: privacy))
        candidates.append(contentsOf: venueStoryStackItems(context: context, storiesByVenue: storiesByVenue, privacy: privacy))
        candidates.append(contentsOf: tableItems(context: context, privacy: privacy))
        candidates.append(contentsOf: flareItems(context: context, privacy: privacy))
        candidates.append(contentsOf: arrivalForecastItems(context: context, friendsByVenueName: friendsByVenueName, privacy: privacy))
        candidates.append(contentsOf: ghostPingItems(context: context, friendsByVenueName: friendsByVenueName, privacy: privacy))

        let serverItems = context.serverFeed?.items ?? []
        let serverScoreById = Dictionary(uniqueKeysWithValues: serverItems.map { ($0.id, $0.score) })
        let signedLinkById = Dictionary(uniqueKeysWithValues: serverItems.compactMap { item in
            item.deepLink.map { (item.id, $0) }
        })

        let scored = candidates.map { item -> FeedItem in
            var copy = item
            let rawScore = score(item, context: context)
                + (serverScoreById[item.id].map { $0 * 0.35 } ?? 0)
                - fatigueScore(for: item, fatigue: fatigue)
                - duplicationSeedPenalty(for: item, in: candidates)
            let ctas = item.ctas.map { cta in
                guard cta.deepLink == nil,
                      let signed = signedLinkById[item.id],
                      cta.kind == .openVenue || cta.kind == .openTable || cta.kind == .replyToFlare
                else { return cta }
                return FeedCTA(kind: cta.kind, title: cta.title, systemImage: cta.systemImage, deepLink: signed)
            }
            copy = FeedItem(
                id: item.id,
                kind: item.kind,
                score: rawScore,
                timestamp: item.timestamp,
                venueId: item.venueId,
                payload: item.payload,
                ctas: ctas,
                privacy: item.privacy,
                tracking: item.tracking
            )
            return copy
        }

        let ranked = scored.sorted { lhs, rhs in
            if lhs.score == rhs.score { return lhs.timestamp > rhs.timestamp }
            return lhs.score > rhs.score
        }

        let diversified = diversify(ranked)
        if diversified.isEmpty {
            return [emptyItem(context: context)]
        }
        return Array(diversified.prefix(maxItems))
    }

    func score(_ item: FeedItem, context: FeedContext) -> Double {
        // Normalized 0...100 components:
        // score =
        // socialProof * 0.30 + venueHeat * 0.25 + freshness * 0.15
        // + visualProof * 0.10 + actionability * 0.15
        // - distancePenalty * 0.10 - fatigue/duplication penalties.
        let components = components(for: item, context: context)
        return components.socialProof * weights.socialProof
            + components.venueHeat * weights.venueHeat
            + components.freshness * weights.freshness
            + components.visualProof * weights.visualProof
            + components.actionability * weights.actionability
            - components.distancePenalty * weights.distancePenalty
    }

    private func components(for item: FeedItem, context: FeedContext) -> ScoreComponents {
        switch item.payload {
        case .hotspotVenue(let payload):
            return ScoreComponents(
                socialProof: clamp(Double(payload.friendsHere * 18 + payload.friendsArriving * 22 + payload.friendActivities.count * 8)),
                venueHeat: clamp(Double(payload.energyScore + payload.growthScore) / 2),
                freshness: freshnessScore(item.timestamp, now: context.now),
                visualProof: clamp(Double(payload.storyPreviews.count * 25 + (payload.coverImageUrl == nil ? 0 : 20))),
                actionability: payload.ctaStrength,
                distancePenalty: distancePenalty(payload.distanceMeters)
            )
        case .friendsActivity(let payload):
            return ScoreComponents(
                socialProof: payload.isFuzzy ? 62 : 78,
                venueHeat: 42,
                freshness: freshnessScore(payload.happenedAt, now: context.now),
                visualProof: clamp(Double(payload.avatarUrls.count * 20)),
                actionability: 72,
                distancePenalty: 0
            )
        case .venueStoryStack(let payload):
            return ScoreComponents(
                socialProof: clamp(Double(payload.friendNames.count * 22)),
                venueHeat: 45,
                freshness: freshnessScore(payload.createdAt, now: context.now),
                visualProof: clamp(Double(payload.storyCount * 18 + (payload.coverMediaUrl == nil ? 0 : 30))),
                actionability: 82,
                distancePenalty: 0
            )
        case .joinableTable(let payload):
            return ScoreComponents(
                socialProof: clamp(Double(payload.acceptedCount * 16 + payload.invitedCount * 7)),
                venueHeat: payload.fillRatio >= 0.66 ? 72 : 42,
                freshness: freshnessScore(payload.startsAt, now: context.now, futureHalfLifeMinutes: 90),
                visualProof: 28,
                actionability: payload.membershipStatus == "accepted" ? 42 : 90,
                distancePenalty: 0
            )
        case .flareChain(let payload):
            let remainingMinutes = Double(payload.remainingSeconds(now: context.now)) / 60
            return ScoreComponents(
                socialProof: clamp(Double(payload.responseCount * 18)),
                venueHeat: remainingMinutes < 15 ? 72 : 48,
                freshness: freshnessScore(payload.createdAt, now: context.now),
                visualProof: payload.avatarUrl == nil ? 18 : 40,
                actionability: remainingMinutes <= 20 ? 95 : 76,
                distancePenalty: 0
            )
        case .arrivalForecast(let payload):
            return ScoreComponents(
                socialProof: clamp(Double(payload.friendsArriving * 24 + payload.expectedPeople * 8)),
                venueHeat: payload.minutesUntilPeak <= 25 ? 74 : 55,
                freshness: 68,
                visualProof: 18,
                actionability: 84,
                distancePenalty: 0
            )
        case .ghostPing(let payload):
            return ScoreComponents(
                socialProof: clamp(Double(payload.signalStrength * 16)),
                venueHeat: 34,
                freshness: 50,
                visualProof: 10,
                actionability: 58,
                distancePenalty: 0
            )
        case .empty:
            return ScoreComponents(socialProof: 0, venueHeat: 0, freshness: 0, visualProof: 0, actionability: 10, distancePenalty: 0)
        }
    }

    private func hotspotVenueItems(
        context: FeedContext,
        storiesByVenue: [UUID: [FeedStoryPreview]],
        friendsByVenueName: [String: [FriendActivity]],
        privacy: FeedPrivacyPolicy
    ) -> [FeedItem] {
        context.venues.map { venue in
            let friends = friendsByVenueName[venue.name] ?? []
            let friendsHere = venue.intentRadar.hereNow + friends.filter { $0.kind == .arrived }.count
            let friendsArriving = venue.intentRadar.almostThere + venue.intentRadar.goingOut + friends.filter {
                if case .going = $0.kind { return true }
                return false
            }.count
            let previews = storiesByVenue[venue.venueId] ?? []
            let distance = context.myLocation.map { $0.distance(from: CLLocation(latitude: venue.latitude, longitude: venue.longitude)) }
            let payload = HotspotVenuePayload(
                id: venue.venueId.uuidString,
                venueId: venue.venueId,
                name: venue.name,
                category: venue.category,
                coverImageUrl: venue.coverImageUrl,
                energyScore: venue.partyPulse.energyScore,
                liveState: liveState(energy: venue.partyPulse.energyScore, growth: venue.partyPulse.arrivalsLast15 * 12 + venue.activeIntentions * 6, crowd: venue.peopleEstimate),
                estimatedCrowd: venue.peopleEstimate,
                friendsHere: friendsHere,
                friendsArriving: friendsArriving,
                growthScore: min(100, venue.partyPulse.arrivalsLast15 * 14 + venue.activeIntentions * 8),
                distanceMeters: distance,
                trend: venue.partyPulse.sparkline.isEmpty ? trend(seed: venue.name, energy: venue.partyPulse.energyScore, growth: venue.activeIntentions * 10) : venue.partyPulse.sparkline,
                friendActivities: Array(friends.prefix(4)),
                storyPreviews: Array(previews.prefix(4)),
                pulseCopy: pulseCopy(for: venue)
            )
            return item(
                kind: .hotspotVenue,
                id: "hotspot-\(venue.venueId.uuidString)",
                venueId: venue.venueId,
                timestamp: context.now,
                payload: .hotspotVenue(payload),
                ctas: [
                    FeedCTA(kind: .openVenue, title: "Apri luogo", systemImage: "mappin.circle.fill", deepLink: deepLink("venue", venue.venueId)),
                    FeedCTA(kind: .openStories, title: "Stories", systemImage: "play.circle.fill", deepLink: deepLink("story-stack", venue.venueId)),
                    FeedCTA(kind: .inviteFriends, title: "Invita", systemImage: "person.badge.plus")
                ],
                privacy: privacy.hotspotExplanation,
                source: context.isDebugDemo ? "debug_demo" : "venue_map_layer"
            )
        }
    }

    private func friendsActivityItems(
        context: FeedContext,
        friendsByVenueName: [String: [FriendActivity]],
        privacy: FeedPrivacyPolicy
    ) -> [FeedItem] {
        friendsByVenueName.flatMap { venueName, activities in
            Array(activities.prefix(2)).map { activity in
                let title = privacy.canShowPrecisePresence ? activity.safeCopy : fuzzyActivityTitle(activity)
                let payload = FriendsActivityPayload(
                    id: activity.id,
                    title: title,
                    subtitle: venueName,
                    venueId: nil,
                    venueName: venueName,
                    avatarUrls: activities.map(\.avatarUrl),
                    activityKind: activity.kind,
                    happenedAt: activity.createdAt,
                    isFuzzy: !privacy.canShowPrecisePresence
                )
                return item(
                    kind: .friendsActivity,
                    id: "activity-\(activity.id)",
                    venueId: nil,
                    timestamp: activity.createdAt,
                    payload: .friendsActivity(payload),
                    ctas: [
                        FeedCTA(kind: .openMap, title: "Apri mappa", systemImage: "map.fill"),
                        FeedCTA(kind: .inviteFriends, title: "Invita", systemImage: "person.badge.plus"),
                        FeedCTA(kind: .openPrivacy, title: "Privacy", systemImage: "lock.shield.fill")
                    ],
                    privacy: activity.privacy,
                    source: "social_hub"
                )
            }
        }
    }

    private func venueStoryStackItems(
        context: FeedContext,
        storiesByVenue: [UUID: [FeedStoryPreview]],
        privacy: FeedPrivacyPolicy
    ) -> [FeedItem] {
        storiesByVenue.compactMap { venueId, previews in
            guard let first = previews.sorted(by: { $0.createdAt > $1.createdAt }).first else { return nil }
            let venueName = first.venueName ?? context.venues.first(where: { $0.venueId == venueId })?.name ?? "Questo posto"
            let payload = VenueStoryStackPayload(
                id: "story-stack-\(venueId.uuidString)",
                venueId: venueId,
                venueName: venueName,
                coverMediaUrl: first.mediaUrl,
                storyCount: previews.count,
                friendNames: Array(Set(previews.map(\.displayName))).sorted(),
                previews: Array(previews.sorted { $0.createdAt > $1.createdAt }.prefix(5)),
                createdAt: first.createdAt
            )
            return item(
                kind: .venueStoryStack,
                id: payload.id,
                venueId: venueId,
                timestamp: first.createdAt,
                payload: .venueStoryStack(payload),
                ctas: [
                    FeedCTA(kind: .openStories, title: "Guarda stack", systemImage: "play.fill", deepLink: deepLink("story-stack", venueId)),
                    FeedCTA(kind: .createStory, title: "Posta qui", systemImage: "camera.fill")
                ],
                privacy: privacy.storyExplanation,
                source: context.isDebugDemo ? "debug_demo" : "stories"
            )
        }
    }

    private func tableItems(context: FeedContext, privacy: FeedPrivacyPolicy) -> [FeedItem] {
        let fromTables = context.tables.map { table -> FeedItem in
            tableItem(table, privacy: privacy, source: "tables")
        }
        let fromInvites: [FeedItem] = context.socialHub?.tableInvites.map { invite in
            let table = SocialTableSummary(
                tableId: invite.tableId,
                title: invite.title,
                description: nil,
                startsAtUtc: invite.startsAtUtc,
                venueName: invite.venueName,
                venueCategory: invite.venueCategory,
                joinPolicy: "invite",
                isHost: false,
                membershipStatus: "invited",
                capacity: 6,
                requestedCount: 0,
                acceptedCount: 3,
                invitedCount: 1
            )
            return tableItem(table, privacy: privacy, source: "table_invites")
        } ?? []
        return fromTables + fromInvites
    }

    private func tableItem(_ table: SocialTableSummary, privacy: FeedPrivacyPolicy, source: String) -> FeedItem {
        let payload = TableSuggestionPayload(
            id: table.tableId,
            title: table.title,
            venueName: table.venueName,
            venueCategory: table.venueCategory,
            startsAt: table.startsAtUtc,
            capacity: table.capacity,
            acceptedCount: table.acceptedCount,
            invitedCount: table.invitedCount,
            requestedCount: table.requestedCount,
            membershipStatus: table.membershipStatus,
            isHost: table.isHost
        )
        return item(
            kind: .joinableTable,
            id: "table-\(table.tableId.uuidString)",
            venueId: nil,
            timestamp: table.startsAtUtc,
            payload: .joinableTable(payload),
            ctas: [
                FeedCTA(kind: table.membershipStatus == "accepted" ? .openTable : .joinTable, title: table.membershipStatus == "accepted" ? "Apri" : "Join", systemImage: "person.crop.circle.badge.plus", deepLink: deepLink("table", table.tableId)),
                FeedCTA(kind: .inviteFriends, title: "Invita", systemImage: "person.badge.plus")
            ],
            privacy: privacy.tableExplanation,
            source: source
        )
    }

    private func flareItems(context: FeedContext, privacy: FeedPrivacyPolicy) -> [FeedItem] {
        context.flares
            .filter { $0.expiresAtUtc > context.now }
            .map { flare in
                let payload = FlareChainPayload(
                    id: flare.flareId,
                    authorName: flare.displayName ?? flare.nickname,
                    avatarUrl: flare.avatarUrl,
                    message: flare.message,
                    responseCount: flare.responseCount,
                    createdAt: flare.createdAtUtc,
                    expiresAt: flare.expiresAtUtc,
                    zoneLabel: "zona attiva"
                )
                return item(
                    kind: .flareChain,
                    id: "flare-\(flare.flareId.uuidString)",
                    venueId: nil,
                    timestamp: flare.createdAtUtc,
                    payload: .flareChain(payload),
                    ctas: [
                        FeedCTA(kind: .replyToFlare, title: "Io ci sono", systemImage: "hand.raised.fill", deepLink: deepLink("flare", flare.flareId)),
                        FeedCTA(kind: .relayFlare, title: "Rilancia", systemImage: "arrowshape.turn.up.right.fill")
                    ],
                    privacy: privacy.flareExplanation,
                    source: "flares"
                )
            }
    }

    private func arrivalForecastItems(
        context: FeedContext,
        friendsByVenueName: [String: [FriendActivity]],
        privacy: FeedPrivacyPolicy
    ) -> [FeedItem] {
        context.venues.compactMap { venue in
            let arriving = venue.intentRadar.almostThere + venue.intentRadar.goingOut
            guard arriving >= 2 || (friendsByVenueName[venue.name]?.count ?? 0) >= 2 else { return nil }
            let payload = ArrivalForecastPayload(
                id: "arrival-\(venue.venueId.uuidString)",
                venueId: venue.venueId,
                venueName: venue.name,
                minutesUntilPeak: max(8, min(35, 32 - arriving * 4)),
                expectedPeople: venue.intentRadar.hereNow + arriving + venue.activeCheckIns,
                friendsArriving: arriving,
                confidence: min(0.92, 0.48 + Double(arriving) * 0.08),
                buckets: [
                    ArrivalBucket(label: "in zona", count: venue.intentRadar.goingOut),
                    ArrivalBucket(label: "quasi qui", count: venue.intentRadar.almostThere),
                    ArrivalBucket(label: "qui ora", count: venue.intentRadar.hereNow)
                ]
            )
            return item(
                kind: .arrivalForecast,
                id: payload.id,
                venueId: venue.venueId,
                timestamp: context.now,
                payload: .arrivalForecast(payload),
                ctas: [
                    FeedCTA(kind: .openMap, title: "Apri mappa", systemImage: "map.fill"),
                    FeedCTA(kind: .inviteFriends, title: "Invita", systemImage: "person.badge.plus")
                ],
                privacy: privacy.intentExplanation,
                source: "intent_radar"
            )
        }
    }

    private func ghostPingItems(
        context: FeedContext,
        friendsByVenueName: [String: [FriendActivity]],
        privacy: FeedPrivacyPolicy
    ) -> [FeedItem] {
        guard privacy.shouldFuzzPresence else { return [] }
        let activeCount = friendsByVenueName.values.reduce(0) { $0 + $1.count }
        guard activeCount > 0 else { return [] }
        let payload = GhostPingPayload(
            id: "ghost-ping-\(activeCount)",
            title: "C'e movimento nel tuo giro",
            subtitle: activeCount == 1 ? "Qualcuno e qui vicino" : "\(activeCount) persone sono attive in zona",
            zoneLabel: "zona vicina",
            signalStrength: min(5, activeCount)
        )
        return [
            item(
                kind: .ghostPing,
                id: payload.id,
                venueId: nil,
                timestamp: context.now,
                payload: .ghostPing(payload),
                ctas: [
                    FeedCTA(kind: .openMap, title: "Apri mappa", systemImage: "map.fill"),
                    FeedCTA(kind: .openPrivacy, title: "Privacy", systemImage: "lock.shield.fill")
                ],
                privacy: privacy.ghostExplanation,
                source: "privacy_fuzzy"
            )
        ]
    }

    private func groupStoriesByVenue(_ stories: [UserStory], venueStories: [VenueStory]) -> [UUID: [FeedStoryPreview]] {
        var result: [UUID: [FeedStoryPreview]] = [:]
        var seenStoryIdsByVenue: [UUID: Set<UUID>] = [:]

        func append(_ preview: FeedStoryPreview, to venueId: UUID) {
            if seenStoryIdsByVenue[venueId, default: []].contains(preview.id) {
                return
            }
            seenStoryIdsByVenue[venueId, default: []].insert(preview.id)
            result[venueId, default: []].append(preview)
        }

        for story in stories {
            guard let venueId = story.venueId else { continue }
            append(FeedStoryPreview(
                id: story.id,
                userId: story.userId,
                displayName: story.displayName ?? story.nickname,
                avatarUrl: story.avatarUrl,
                mediaUrl: story.mediaUrl,
                caption: story.caption,
                venueId: story.venueId,
                venueName: story.venueName,
                createdAt: story.createdAtUtc
            ), to: venueId)
        }
        for story in venueStories {
            append(FeedStoryPreview(
                id: story.id,
                userId: story.userId,
                displayName: story.displayName ?? story.nickname,
                avatarUrl: story.avatarUrl,
                mediaUrl: story.mediaUrl,
                caption: story.caption,
                venueId: story.venueId,
                venueName: story.venueName,
                createdAt: story.createdAtUtc
            ), to: story.venueId)
        }
        return result.mapValues { $0.sorted { $0.createdAt > $1.createdAt } }
    }

    private func groupFriendsByVenueName(_ friends: [SocialConnection], privacy: FeedPrivacyPolicy) -> [String: [FriendActivity]] {
        var result: [String: [FriendActivity]] = [:]
        guard privacy.canUsePresenceSignal else { return result }
        for friend in friends {
            guard let venueName = friend.currentVenueName, !venueName.isEmpty else { continue }
            let isArriving = friend.presenceState == "intention"
                || friend.presenceState == "going"
                || friend.statusLabel.lowercased().contains("arriv")
            let envelope = privacy.canShowPrecisePresence
                ? FeedPrivacyEnvelope.friends(reason: "Mostrato perche siete amici e la presenza e condivisa.")
                : FeedPrivacyEnvelope.fuzzy(reason: "Mostrato in modo fuzzy in base alle impostazioni privacy.")
            result[venueName, default: []].append(FriendActivity(
                id: "\(isArriving ? "going" : "here")-\(friend.userId.uuidString)-\(venueName)",
                userId: privacy.canShowPrecisePresence ? friend.userId : nil,
                displayName: privacy.canShowPrecisePresence ? (friend.displayName ?? friend.nickname) : "Alcuni amici",
                avatarUrl: privacy.canShowPrecisePresence ? friend.avatarUrl : nil,
                kind: isArriving ? .going : .arrived,
                createdAt: Date(),
                privacy: envelope
            ))
        }
        return result
    }

    private func diversify(_ items: [FeedItem]) -> [FeedItem] {
        var output: [FeedItem] = []
        var delayed: [FeedItem] = []
        var lastKind: FeedCardKind?
        var lastVenue: UUID?

        for item in items {
            if item.kind == lastKind || (item.venueId != nil && item.venueId == lastVenue) {
                delayed.append(item)
            } else {
                output.append(item)
                lastKind = item.kind
                lastVenue = item.venueId
            }
            if output.count % 3 == 0, let next = delayed.first {
                delayed.removeFirst()
                output.append(next)
                lastKind = next.kind
                lastVenue = next.venueId
            }
        }

        return output + delayed
    }

    private func item(
        kind: FeedCardKind,
        id: String,
        venueId: UUID?,
        timestamp: Date,
        payload: FeedPayload,
        ctas: [FeedCTA],
        privacy: FeedPrivacyEnvelope,
        source: String
    ) -> FeedItem {
        FeedItem(
            id: id,
            kind: kind,
            score: 0,
            timestamp: timestamp,
            venueId: venueId,
            payload: payload,
            ctas: ctas,
            privacy: privacy,
            tracking: FeedTracking(
                cardType: kind,
                rankHint: 0,
                source: source,
                experimentBucket: "feed_v2",
                impressionId: "\(kind.rawValue)-\(id)"
            )
        )
    }

    private func emptyItem(context: FeedContext) -> FeedItem {
        item(
            kind: .emptyOnboarding,
            id: "empty-feed",
            venueId: nil,
            timestamp: context.now,
            payload: .empty(EmptyFeedPayload(
                title: "La citta si sta ancora svegliando",
                message: "Segui amici, apri la mappa o posta una story in un luogo per accendere il feed."
            )),
            ctas: [
                FeedCTA(kind: .openMap, title: "Apri mappa", systemImage: "map.fill"),
                FeedCTA(kind: .createStory, title: "Crea story", systemImage: "camera.fill"),
                FeedCTA(kind: .openPrivacy, title: "Privacy", systemImage: "lock.shield.fill")
            ],
            privacy: FeedPrivacyEnvelope(visibility: .anonymous, precision: .hidden, explanation: "Nessun dato personale mostrato."),
            source: "empty_state"
        )
    }

    private func privacyPolicy(from state: SocialMeState?) -> FeedPrivacyPolicy {
        FeedPrivacyPolicy(
            isGhostMode: state?.isGhostModeEnabled ?? false,
            sharePresence: state?.sharePresenceWithFriends ?? false,
            shareIntentions: state?.shareIntentionsWithFriends ?? false
        )
    }

    private func liveState(energy: Int, growth: Int, crowd: Int) -> VenueLiveState {
        if energy >= 84 || crowd >= 18 { return .almostFull }
        if energy >= 68 { return .hotNow }
        if growth >= 36 { return .growing }
        return .wakingUp
    }

    private func pulseCopy(for venue: VenueMarker) -> String {
        if venue.partyPulse.energyScore >= 80 { return "Qui si sta accendendo" }
        if venue.partyPulse.arrivalsLast15 >= 3 { return "Sta salendo ora" }
        if venue.intentRadar.almostThere >= 2 { return "Amici quasi qui" }
        return venue.partyPulse.mood.isEmpty ? venue.densityLevel : venue.partyPulse.mood
    }

    private func trend(seed: String, energy: Int, growth: Int) -> [Int] {
        let base = max(12, min(70, energy - growth / 3))
        return (0..<8).map { index in
            let wave = abs((seed.hashValue + index * 17) % 12)
            return min(100, max(6, base + index * max(1, growth / 14) + wave))
        }
    }

    private func fuzzyActivityTitle(_ activity: FriendActivity) -> String {
        switch activity.kind {
        case .arrived: return "Qualcuno del tuo giro e qui vicino"
        case .going: return "Alcuni amici stanno convergendo"
        case .postedStory: return "Nuova foto da un posto vicino"
        case .groupConverging(let count): return "\(count) persone del tuo giro si muovono"
        }
    }

    private func freshnessScore(_ date: Date, now: Date, futureHalfLifeMinutes: Double? = nil) -> Double {
        let minutes = abs(now.timeIntervalSince(date)) / 60
        let halfLife = futureHalfLifeMinutes ?? 45
        return clamp(100 * pow(0.5, minutes / halfLife))
    }

    private func distancePenalty(_ distanceMeters: Double?) -> Double {
        guard let distanceMeters else { return 0 }
        let km = distanceMeters / 1000
        return clamp(km * 18)
    }

    private func fatigueScore(for item: FeedItem, fatigue: [String: Int]) -> Double {
        Double(fatigue[item.id, default: 0]) * weights.fatiguePenalty * 100
    }

    private func duplicationSeedPenalty(for item: FeedItem, in candidates: [FeedItem]) -> Double {
        guard let venueId = item.venueId else { return 0 }
        let count = candidates.filter { $0.venueId == venueId }.count
        return Double(max(0, count - 2)) * weights.duplicationPenalty * 100
    }

    private func deepLink(_ type: String, _ id: UUID) -> String {
        "cloudy://l/\(type)/\(id.uuidString.lowercased())"
    }

    private func clamp(_ value: Double) -> Double {
        min(100, max(0, value))
    }
}

private struct ScoreComponents {
    let socialProof: Double
    let venueHeat: Double
    let freshness: Double
    let visualProof: Double
    let actionability: Double
    let distancePenalty: Double
}

private extension HotspotVenuePayload {
    var ctaStrength: Double {
        var value = 50.0
        if friendsArriving > 0 { value += 18 }
        if !storyPreviews.isEmpty { value += 14 }
        if liveState == .hotNow || liveState == .almostFull { value += 18 }
        return min(100, value)
    }
}

private struct FeedPrivacyPolicy {
    let isGhostMode: Bool
    let sharePresence: Bool
    let shareIntentions: Bool

    var canUsePresenceSignal: Bool { !isGhostMode }
    var canShowPrecisePresence: Bool { !isGhostMode && sharePresence }
    var shouldFuzzPresence: Bool { isGhostMode || !sharePresence }

    var hotspotExplanation: FeedPrivacyEnvelope {
        FeedPrivacyEnvelope(visibility: .aggregated, precision: .coarseVenue, explanation: "Dato aggregato, nessuna posizione precisa.")
    }

    var storyExplanation: FeedPrivacyEnvelope {
        FeedPrivacyEnvelope(visibility: .friends, precision: .exactVenue, explanation: "Mostrato perche sono stories condivise con il tuo giro.")
    }

    var tableExplanation: FeedPrivacyEnvelope {
        FeedPrivacyEnvelope(visibility: .friends, precision: .exactVenue, explanation: "Mostrato perche il tavolo e visibile ai partecipanti o agli invitati.")
    }

    var flareExplanation: FeedPrivacyEnvelope {
        FeedPrivacyEnvelope(visibility: .friendsFuzzy, precision: .district, explanation: "Flare mostrato in zona, senza coordinate utente precise.")
    }

    var intentExplanation: FeedPrivacyEnvelope {
        FeedPrivacyEnvelope(
            visibility: shareIntentions && !isGhostMode ? .friendsFuzzy : .aggregated,
            precision: .coarseVenue,
            explanation: shareIntentions && !isGhostMode
                ? "Mostrato in modo fuzzy in base alle intenzioni condivise."
                : "Dato aggregato: nessuna intenzione personale precisa."
        )
    }

    var ghostExplanation: FeedPrivacyEnvelope {
        FeedPrivacyEnvelope(visibility: .anonymous, precision: .district, explanation: "Segnale fuzzy: niente nomi, niente posizione precisa.")
    }
}
