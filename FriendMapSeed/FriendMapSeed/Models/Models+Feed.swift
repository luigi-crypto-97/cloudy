//
//  Models+Feed.swift
//  Cloudy — Feed & Gamification models
//
//  Modelli per feed attività e gamification.
//

import Foundation

// MARK: - Feed

struct FeedServerResponse: Codable {
    let items: [FeedServerItem]
    let venues: [VenueMarker]
    let flares: [FlareSignal]
    let tables: [SocialTableSummary]
    let fatigue: [FeedCardFatigueSnapshot]
    let generatedAtUtc: Date
}

struct FeedServerItem: Codable, Hashable, Identifiable {
    let id: String
    let kind: String
    let score: Double
    let timestamp: Date
    let venueId: UUID?
    let entityId: UUID?
    let title: String
    let subtitle: String
    let privacyExplanation: String
    let source: String
    let deepLink: String?
    let shareUrl: String?
}

struct FeedCardFatigueSnapshot: Codable, Hashable {
    let cardKey: String
    let seenCount: Int
    let dismissedCount: Int
    let lastSeenAtUtc: Date?
    let lastDismissedAtUtc: Date?
}

struct FeedFatigueUpdateRequest: Codable {
    let cardKey: String
    let dismissed: Bool
}

struct SignedDeepLinkRequest: Codable {
    let type: String
    let targetId: UUID
    let expiresInMinutes: Int?
    let maxUses: Int?
}

struct SignedDeepLink: Codable, Hashable {
    let url: String
    let expiresAtUtc: Date
}

// MARK: - Gamification

struct GamificationSummary: Codable, Hashable {
    let totalPoints: Int
    let weeklyPoints: Int
    let level: Int
    let levelProgress: Double
    let primaryCity: String?
    let badges: [UserBadge]
    let weeklyMissions: [WeeklyMission]
    let antiCheatNote: String
}

struct UserBadge: Codable, Hashable, Identifiable {
    let code: String
    let title: String
    let earnedAtUtc: Date
    var id: String { code }
}

struct WeeklyMission: Codable, Hashable, Identifiable {
    let code: String
    let title: String
    let subtitle: String
    let icon: String
    let progress: Int
    let target: Int
    let rewardPoints: Int
    let isCompleted: Bool
    var id: String { code }

    var progressRatio: Double {
        guard target > 0 else { return 0 }
        return min(1, Double(progress) / Double(target))
    }
}

struct Leaderboard: Codable, Hashable {
    let scopeName: String
    let zone: String?
    let generatedAtUtc: Date
    let entries: [LeaderboardEntry]
}

struct LeaderboardEntry: Codable, Hashable, Identifiable {
    let rank: Int
    let userId: UUID
    let nickname: String
    let displayName: String?
    let avatarUrl: String?
    let totalPoints: Int
    let weeklyPoints: Int
    let level: Int
    let primaryCity: String?
    let isMe: Bool
    var id: UUID { userId }
}

// MARK: - Helpers

public struct IgnoredResponse: Codable {}
public struct EmptyResponse: Decodable {}

// MARK: - UUID Extension

extension UUID {
    /// Empty UUID (000...) — usato per stato "non autenticato".
    static let empty = UUID(uuid: (0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0))
}
