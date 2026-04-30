//
//  FeedModels.swift
//  Cloudy
//
//  Feed V2: modelli locali per un decision engine virale e privacy-safe.
//  Restano separati dai DTO API per poter cambiare backend senza riscrivere UI
//  e ranking.
//

import Foundation
import CoreLocation

// MARK: - Feed item

struct FeedItem: Identifiable, Hashable {
    let id: String
    let kind: FeedCardKind
    let score: Double
    let timestamp: Date
    let venueId: UUID?
    let payload: FeedPayload
    let ctas: [FeedCTA]
    let privacy: FeedPrivacyEnvelope
    let tracking: FeedTracking
}

enum FeedCardKind: String, Codable, Hashable, CaseIterable {
    case hotspotVenue
    case friendsActivity
    case venueStoryStack
    case joinableTable
    case flareChain
    case arrivalForecast
    case ghostPing
    case emptyOnboarding
}

enum FeedPayload: Hashable {
    case hotspotVenue(HotspotVenuePayload)
    case friendsActivity(FriendsActivityPayload)
    case venueStoryStack(VenueStoryStackPayload)
    case joinableTable(TableSuggestionPayload)
    case flareChain(FlareChainPayload)
    case arrivalForecast(ArrivalForecastPayload)
    case ghostPing(GhostPingPayload)
    case empty(EmptyFeedPayload)
}

// MARK: - Payloads

struct HotspotVenuePayload: Identifiable, Hashable {
    let id: String
    let venueId: UUID?
    let name: String
    let category: String?
    let coverImageUrl: String?
    let energyScore: Int
    let liveState: VenueLiveState
    let estimatedCrowd: Int
    let friendsHere: Int
    let friendsArriving: Int
    let growthScore: Int
    let distanceMeters: Double?
    let trend: [Int]
    let friendActivities: [FriendActivity]
    let storyPreviews: [FeedStoryPreview]
    let pulseCopy: String
}

struct FriendsActivityPayload: Identifiable, Hashable {
    let id: String
    let title: String
    let subtitle: String
    let venueId: UUID?
    let venueName: String?
    let avatarUrls: [String?]
    let activityKind: FriendActivityKind
    let happenedAt: Date
    let isFuzzy: Bool
}

struct VenueStoryStackPayload: Identifiable, Hashable {
    let id: String
    let venueId: UUID?
    let venueName: String
    let coverMediaUrl: String?
    let storyCount: Int
    let friendNames: [String]
    let previews: [FeedStoryPreview]
    let createdAt: Date
}

struct TableSuggestionPayload: Identifiable, Hashable {
    let id: UUID
    let title: String
    let venueName: String
    let venueCategory: String
    let startsAt: Date
    let capacity: Int
    let acceptedCount: Int
    let invitedCount: Int
    let requestedCount: Int
    let membershipStatus: String
    let isHost: Bool

    var fillRatio: Double {
        guard capacity > 0 else { return 0 }
        return min(1, Double(acceptedCount) / Double(capacity))
    }
}

struct FlareChainPayload: Identifiable, Hashable {
    let id: UUID
    let authorName: String
    let avatarUrl: String?
    let message: String
    let responseCount: Int
    let createdAt: Date
    let expiresAt: Date
    let zoneLabel: String

    func remainingSeconds(now: Date = Date()) -> Int {
        max(0, Int(expiresAt.timeIntervalSince(now)))
    }
}

struct ArrivalForecastPayload: Identifiable, Hashable {
    let id: String
    let venueId: UUID?
    let venueName: String
    let minutesUntilPeak: Int
    let expectedPeople: Int
    let friendsArriving: Int
    let confidence: Double
    let buckets: [ArrivalBucket]
}

struct ArrivalBucket: Hashable {
    let label: String
    let count: Int
}

struct GhostPingPayload: Identifiable, Hashable {
    let id: String
    let title: String
    let subtitle: String
    let zoneLabel: String?
    let signalStrength: Int
}

struct EmptyFeedPayload: Hashable {
    let title: String
    let message: String
}

// MARK: - Shared feed atoms

struct FriendActivity: Identifiable, Hashable {
    let id: String
    let userId: UUID?
    let displayName: String
    let avatarUrl: String?
    let kind: FriendActivityKind
    let createdAt: Date
    let privacy: FeedPrivacyEnvelope

    var safeCopy: String {
        if privacy.visibility == .friendsFuzzy || privacy.visibility == .aggregated || privacy.visibility == .anonymous {
            switch kind {
            case .groupConverging(let count): return "\(count) persone del tuo giro stanno convergendo"
            case .going: return "Qualcuno del tuo giro sta andando"
            case .arrived: return "Qualcuno del tuo giro e arrivato"
            case .postedStory: return "Nuova foto da questo posto"
            }
        }

        switch kind {
        case .arrived: return "\(displayName) e appena arrivato"
        case .going: return "\(displayName) sta andando"
        case .postedStory: return "\(displayName) ha postato una foto qui"
        case .groupConverging(let count): return "\(count) amici stanno convergendo"
        }
    }
}

enum FriendActivityKind: Hashable {
    case arrived
    case going
    case postedStory
    case groupConverging(count: Int)
}

enum VenueLiveState: String, Codable, Hashable {
    case wakingUp
    case growing
    case hotNow
    case almostFull

    var copy: String {
        switch self {
        case .wakingUp: return "Si sta svegliando"
        case .growing: return "Sta crescendo"
        case .hotNow: return "Caldo ora"
        case .almostFull: return "Quasi pieno"
        }
    }
}

struct FeedStoryPreview: Identifiable, Hashable {
    let id: UUID
    let userId: UUID
    let displayName: String
    let avatarUrl: String?
    let mediaUrl: String?
    let caption: String?
    let venueId: UUID?
    let venueName: String?
    let createdAt: Date
}

// MARK: - CTA

struct FeedCTA: Identifiable, Hashable {
    let id: String
    let kind: FeedCTAKind
    let title: String
    let systemImage: String
    let deepLink: String?

    init(kind: FeedCTAKind, title: String, systemImage: String, deepLink: String? = nil) {
        self.id = "\(kind.rawValue)-\(title)"
        self.kind = kind
        self.title = title
        self.systemImage = systemImage
        self.deepLink = deepLink
    }
}

enum FeedCTAKind: String, Codable, Hashable {
    case openVenue
    case openMap
    case openStories
    case joinTable
    case openTable
    case inviteFriends
    case relayFlare
    case replyToFlare
    case createStory
    case openPrivacy
    case share
}

// MARK: - Privacy

struct FeedPrivacyEnvelope: Hashable {
    let visibility: FeedVisibilityLevel
    let precision: FeedLocationPrecision
    let explanation: String

    static let publicVenue = FeedPrivacyEnvelope(
        visibility: .publicVenue,
        precision: .exactVenue,
        explanation: "Mostrato perche il luogo e pubblico e aggregato."
    )

    static let aggregated = FeedPrivacyEnvelope(
        visibility: .aggregated,
        precision: .coarseVenue,
        explanation: "Dato aggregato, nessuna posizione precisa."
    )

    static func friends(reason: String) -> FeedPrivacyEnvelope {
        FeedPrivacyEnvelope(visibility: .friends, precision: .exactVenue, explanation: reason)
    }

    static func fuzzy(reason: String) -> FeedPrivacyEnvelope {
        FeedPrivacyEnvelope(visibility: .friendsFuzzy, precision: .coarseVenue, explanation: reason)
    }
}

enum FeedVisibilityLevel: String, Codable, Hashable {
    case publicVenue
    case friends
    case friendsFuzzy
    case aggregated
    case anonymous
}

enum FeedLocationPrecision: String, Codable, Hashable {
    case exactVenue
    case coarseVenue
    case district
    case hidden
}

// MARK: - Tracking / re-entry

struct FeedTracking: Hashable {
    let cardType: FeedCardKind
    let rankHint: Int
    let source: String
    let experimentBucket: String
    let impressionId: String
}

enum FeedReentryTrigger: String, Codable, Hashable {
    case venueEnergyThreshold
    case friendsConverging
    case tableAlmostFull
    case flareExpiring
    case nearbyFriendStory
    case nightRecapReady
}

enum FeedNotificationReason: String, Codable, Hashable {
    case hotVenue
    case socialProof
    case tableUrgency
    case flareCountdown
    case freshStory
    case recap
}

// MARK: - Demo data

enum FeedDemoFactory {
    #if DEBUG
    static func context(now: Date = Date()) -> FeedContext {
        let venueId = UUID()
        let tableId = UUID()
        let friendId = UUID()
        let story = UserStory(
            id: UUID(),
            userId: friendId,
            nickname: "gigi",
            displayName: "Gigi",
            avatarUrl: nil,
            mediaUrl: nil,
            caption: "sta salendo forte",
            venueId: venueId,
            venueName: "Le Madi's Cafe",
            likeCount: 12,
            commentCount: 3,
            hasLiked: false,
            createdAtUtc: now.addingTimeInterval(-360),
            expiresAtUtc: now.addingTimeInterval(3600)
        )
        let marker = VenueMarker(
            venueId: venueId,
            name: "Le Madi's Cafe",
            category: "Bar",
            addressLine: "Centro",
            city: "Mentone",
            phoneNumber: nil,
            websiteUrl: nil,
            hoursSummary: nil,
            coverImageUrl: nil,
            description: nil,
            tags: ["bar", "aperitivo"],
            latitude: 43.774,
            longitude: 7.497,
            isOpenNow: true,
            peopleEstimate: 24,
            densityLevel: "high",
            bubbleIntensity: 84,
            demographicDataAvailable: false,
            activeCheckIns: 9,
            activeIntentions: 6,
            openTables: 1,
            partyPulse: PartyPulse(
                energyScore: 84,
                mood: "sta salendo",
                arrivalsLast15: 5,
                checkInsNow: 9,
                intentionsSoon: 6,
                sparkline: [32, 38, 45, 56, 63, 72, 84]
            ),
            intentRadar: IntentRadar(
                goingOut: 4,
                almostThere: 2,
                hereNow: 9,
                coolingDown: 1,
                updatedAtUtc: now,
                privacyLevel: "aggregated"
            ),
            presencePreview: [
                PresencePreview(userId: friendId, displayName: "Gigi", nickname: "gigi", avatarUrl: nil)
            ]
        )
        let hub = SocialHub(
            friends: [
                SocialConnection(
                    userId: friendId,
                    nickname: "gigi",
                    displayName: "Gigi",
                    avatarUrl: nil,
                    relationshipStatus: "accepted",
                    mutualFriendsCount: 3,
                    presenceState: "here",
                    statusLabel: "al locale",
                    currentVenueName: marker.name,
                    currentVenueCategory: marker.category
                )
            ],
            incomingRequests: [],
            outgoingRequests: [],
            tableInvites: [
                SocialTableInvite(
                    tableId: tableId,
                    title: "Aperitivo veloce",
                    startsAtUtc: now.addingTimeInterval(1800),
                    venueName: marker.name,
                    venueCategory: marker.category,
                    hostUserId: friendId,
                    hostNickname: "gigi",
                    hostDisplayName: "Gigi",
                    hostAvatarUrl: nil
                )
            ]
        )
        let table = SocialTableSummary(
            tableId: tableId,
            title: "Aperitivo veloce",
            description: "Due posti rimasti",
            startsAtUtc: now.addingTimeInterval(1800),
            venueName: marker.name,
            venueCategory: marker.category,
            joinPolicy: "open",
            isHost: false,
            membershipStatus: "none",
            capacity: 6,
            requestedCount: 0,
            acceptedCount: 4,
            invitedCount: 1
        )
        let flare = FlareSignal(
            flareId: UUID(),
            userId: friendId,
            nickname: "gigi",
            displayName: "Gigi",
            avatarUrl: nil,
            latitude: marker.latitude,
            longitude: marker.longitude,
            message: "Chi viene ora?",
            responseCount: 2,
            createdAtUtc: now.addingTimeInterval(-300),
            expiresAtUtc: now.addingTimeInterval(900)
        )
        return FeedContext(
            stories: [story],
            socialHub: hub,
            profile: nil,
            venues: [marker],
            venueStories: [],
            flares: [flare],
            tables: [table],
            myLocation: nil,
            privacyState: nil,
            serverFeed: nil,
            gamification: GamificationSummary(
                totalPoints: 680,
                weeklyPoints: 210,
                level: 3,
                levelProgress: 0.72,
                primaryCity: "Mentone",
                badges: [
                    UserBadge(code: "story_maker", title: "Story maker", earnedAtUtc: now.addingTimeInterval(-7200))
                ],
                weeklyMissions: [
                    WeeklyMission(code: "weekly_explorer", title: "Giro nuovo", subtitle: "Visita 3 locali diversi questa settimana", icon: "mappin.and.ellipse", progress: 2, target: 3, rewardPoints: 120, isCompleted: false),
                    WeeklyMission(code: "weekly_story", title: "Racconta la serata", subtitle: "Posta 2 stories taggate a un locale", icon: "camera.fill", progress: 1, target: 2, rewardPoints: 100, isCompleted: false),
                    WeeklyMission(code: "weekly_table", title: "Accendi il tavolo", subtitle: "Crea o partecipa a un tavolo sociale", icon: "person.3.fill", progress: 1, target: 1, rewardPoints: 150, isCompleted: true)
                ],
                antiCheatNote: "Demo: cap anti-spam giornalieri attivi."
            ),
            now: now,
            isDebugDemo: true
        )
    }
    #endif
}
