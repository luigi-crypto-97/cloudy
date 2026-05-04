//
//  Endpoints+Feed.swift
//  Cloudy — Feed & Gamification API endpoints
//

import Foundation

extension API {
    
    // MARK: - Feed

    static func feed(latitude: Double? = nil, longitude: Double? = nil) async throws -> FeedServerResponse {
        var q: [String: String] = [:]
        if let latitude { q["latitude"] = String(latitude) }
        if let longitude { q["longitude"] = String(longitude) }
        return try await APIClient.shared.get("/api/feed", query: q.mapValues { Optional($0) })
    }

    static func updateFeedFatigue(cardKey: String, dismissed: Bool = false) async throws -> FeedCardFatigueSnapshot {
        try await APIClient.shared.post("/api/feed/fatigue", body: FeedFatigueUpdateRequest(cardKey: cardKey, dismissed: dismissed))
    }

    static func signedDeepLink(type: String, targetId: UUID, expiresInMinutes: Int? = 240, maxUses: Int? = 30) async throws -> SignedDeepLink {
        try await APIClient.shared.post(
            "/api/feed/links",
            body: SignedDeepLinkRequest(type: type, targetId: targetId, expiresInMinutes: expiresInMinutes, maxUses: maxUses)
        )
    }

    // MARK: - Gamification

    static func gamificationSummary() async throws -> GamificationSummary {
        try await APIClient.shared.get("/api/gamification/me")
    }

    static func weeklyMissions() async throws -> [WeeklyMission] {
        try await APIClient.shared.get("/api/gamification/missions")
    }

    static func leaderboard(city: String? = nil, zone: String? = nil, limit: Int = 30) async throws -> Leaderboard {
        var q: [String: String] = ["limit": String(limit)]
        if let city, !city.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            q["city"] = city
        }
        if let zone, !zone.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            q["zone"] = zone
        }
        return try await APIClient.shared.get("/api/gamification/leaderboard", query: q.mapValues { Optional($0) })
    }

    static func checkGamificationAchievements() async throws {
        let _: IgnoredResponse = try await APIClient.shared.post("/api/gamification/check")
    }

    // MARK: - Profile

    static func myEditableProfile() async throws -> EditableUserProfile {
        try await APIClient.shared.get("/api/users/me/profile")
    }

    static func updateMyProfile(_ req: UpdateMyProfileRequest) async throws -> EditableUserProfile {
        try await APIClient.shared.put("/api/users/me/profile", body: req)
    }
}
