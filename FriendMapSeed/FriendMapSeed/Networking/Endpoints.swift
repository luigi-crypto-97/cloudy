//
//  Endpoints.swift
//  Cloudy — High-level API surface
//
//  Wrappa APIClient.shared esponendo metodi tipati per ogni feature.
//

import Foundation

enum API {

    // MARK: - Auth

    static func devLogin(nickname: String, displayName: String?) async throws -> AuthTokenResponse {
        let req = DevLoginRequest(nickname: nickname, displayName: displayName)
        return try await APIClient.shared.post("/api/auth/dev-login", body: req)
    }

    static func me() async throws -> AuthUser {
        try await APIClient.shared.get("/api/auth/me")
    }

    // MARK: - Map / Venues

    static func venueMap(
        minLat: Double, minLng: Double,
        maxLat: Double, maxLng: Double,
        query: String? = nil,
        category: String? = nil,
        openNow: Bool = false,
        centerLat: Double? = nil, centerLng: Double? = nil,
        maxDistanceKm: Double? = nil
    ) async throws -> [VenueMarker] {
        var q: [String: String] = [
            "minLat": String(minLat),
            "minLng": String(minLng),
            "maxLat": String(maxLat),
            "maxLng": String(maxLng)
        ]
        if let v = query { q["q"] = v }
        if let v = category { q["category"] = v }
        if openNow { q["openNow"] = "true" }
        if let v = centerLat { q["centerLat"] = String(v) }
        if let v = centerLng { q["centerLng"] = String(v) }
        if let v = maxDistanceKm { q["maxDistanceKm"] = String(v) }
        return try await APIClient.shared.get("/api/venues/map", query: q.mapValues { Optional($0) })
    }

    static func venueMapLayer(
        minLat: Double, minLng: Double,
        maxLat: Double, maxLng: Double,
        query: String? = nil,
        category: String? = nil,
        openNow: Bool = false
    ) async throws -> VenueMapLayer {
        var q: [String: String] = [
            "minLat": String(minLat),
            "minLng": String(minLng),
            "maxLat": String(maxLat),
            "maxLng": String(maxLng)
        ]
        if let v = query { q["q"] = v }
        if let v = category { q["category"] = v }
        if openNow { q["openNow"] = "true" }
        return try await APIClient.shared.get("/api/venues/map-layer", query: q.mapValues { Optional($0) })
    }

    // MARK: - Social hub

    static func socialHub() async throws -> SocialHub {
        try await APIClient.shared.get("/api/social/hub")
    }

    static func userProfile(userId: UUID) async throws -> UserProfile {
        try await APIClient.shared.get("/api/users/\(userId.uuidString.lowercased())")
    }

    // MARK: - Stories

    static func stories() async throws -> [UserStory] {
        try await APIClient.shared.get("/api/stories")
    }

    // MARK: - Notifications

    static func notifications() async throws -> [NotificationItem] {
        try await APIClient.shared.get("/api/notifications")
    }

    // MARK: - Tables

    static func myTables() async throws -> [SocialTableSummary] {
        try await APIClient.shared.get("/api/social/tables/mine")
    }

    static func tableThread(tableId: UUID) async throws -> SocialTableThread {
        try await APIClient.shared.get("/api/social/tables/\(tableId.uuidString.lowercased())/thread")
    }

    static func joinTable(tableId: UUID) async throws -> SocialActionResult {
        try await APIClient.shared.post("/api/social/tables/\(tableId.uuidString.lowercased())/join")
    }

    static func sendTableMessage(tableId: UUID, body: String) async throws -> SocialActionResult {
        let req = SendSocialTableMessageRequest(body: body)
        return try await APIClient.shared.post("/api/social/tables/\(tableId.uuidString.lowercased())/messages", body: req)
    }

    static func approveTableParticipant(tableId: UUID, userId: UUID) async throws -> SocialActionResult {
        try await APIClient.shared.post(
            "/api/social/tables/\(tableId.uuidString.lowercased())/participants/\(userId.uuidString.lowercased())/approve"
        )
    }

    static func rejectTableParticipant(tableId: UUID, userId: UUID) async throws -> SocialActionResult {
        try await APIClient.shared.post(
            "/api/social/tables/\(tableId.uuidString.lowercased())/participants/\(userId.uuidString.lowercased())/reject"
        )
    }

    // MARK: - Direct messages (chat 1:1)

    static func messageThreads() async throws -> [DirectMessageThreadSummary] {
        try await APIClient.shared.get("/api/messages/threads")
    }

    static func messageThread(otherUserId: UUID) async throws -> DirectMessageThread {
        try await APIClient.shared.get("/api/messages/threads/\(otherUserId.uuidString.lowercased())")
    }

    static func sendDirectMessage(otherUserId: UUID, body: String) async throws -> DirectMessage {
        let req = SendDirectMessageRequest(body: body)
        return try await APIClient.shared.post(
            "/api/messages/threads/\(otherUserId.uuidString.lowercased())",
            body: req
        )
    }

    // MARK: - Friends (search + request/accept/reject)

    static func searchUsers(query: String) async throws -> [UserSearchResult] {
        var q: [String: String] = [:]
        q["q"] = query
        return try await APIClient.shared.get("/api/users/search", query: q.mapValues { Optional($0) })
    }

    static func requestFriend(userId: UUID) async throws -> SocialActionResult {
        try await APIClient.shared.post("/api/social/friends/\(userId.uuidString.lowercased())/request")
    }

    static func acceptFriend(userId: UUID) async throws -> SocialActionResult {
        try await APIClient.shared.post("/api/social/friends/\(userId.uuidString.lowercased())/accept")
    }

    static func rejectFriend(userId: UUID) async throws -> SocialActionResult {
        try await APIClient.shared.post("/api/social/friends/\(userId.uuidString.lowercased())/reject")
    }

    // MARK: - Edit profile

    static func myEditableProfile() async throws -> EditableUserProfile {
        try await APIClient.shared.get("/api/users/me/profile")
    }

    static func updateMyProfile(_ req: UpdateMyProfileRequest) async throws -> EditableUserProfile {
        try await APIClient.shared.put("/api/users/me/profile", body: req)
    }

    // MARK: - Stories create

    static func createStory(mediaUrl: String, caption: String?) async throws -> UserStory {
        let req = CreateStoryRequest(mediaUrl: mediaUrl, caption: caption)
        return try await APIClient.shared.post("/api/stories", body: req)
    }

    static func deleteStory(id: UUID) async throws {
        try await APIClient.shared.delete("/api/stories/\(id.uuidString.lowercased())")
    }

    // MARK: - Flare

    static func launchFlare(latitude: Double, longitude: Double, message: String) async throws -> SocialActionResult {
        let req = CreateFlareRequest(latitude: latitude, longitude: longitude, message: message)
        return try await APIClient.shared.post("/api/social/flares", body: req)
    }

    // MARK: - Privacy / Ghost mode

    static func mySocialState() async throws -> SocialMeState {
        try await APIClient.shared.get("/api/social/me/state")
    }

    static func updatePrivacy(_ req: UpdatePrivacySettingsRequest) async throws -> SocialMeState {
        try await APIClient.shared.post("/api/social/me/privacy", body: req)
    }
}
