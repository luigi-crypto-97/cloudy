//
//  Endpoints+Social.swift
//  Cloudy — Social API endpoints (Stories, Notifications, Tables, Flares, Friends, Privacy)
//

import Foundation

extension API {
    
    // MARK: - Social Hub
    
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

    static func storyArchive() async throws -> [UserStory] {
        try await APIClient.shared.get("/api/stories/archive")
    }

    static func venueStories(
        minLat: Double? = nil,
        minLng: Double? = nil,
        maxLat: Double? = nil,
        maxLng: Double? = nil
    ) async throws -> [VenueStory] {
        var q: [String: String] = [:]
        if let minLat { q["minLat"] = String(minLat) }
        if let minLng { q["minLng"] = String(minLng) }
        if let maxLat { q["maxLat"] = String(maxLat) }
        if let maxLng { q["maxLng"] = String(maxLng) }
        return try await APIClient.shared.get("/api/stories/venues", query: q.mapValues { Optional($0) })
    }

    static func createStory(mediaUrl: String, caption: String?, venueId: UUID? = nil) async throws -> UserStory {
        let req = CreateStoryRequest(mediaUrl: mediaUrl, caption: caption, venueId: venueId)
        return try await APIClient.shared.post("/api/stories", body: req)
    }

    static func toggleStoryLike(storyId: UUID) async throws -> StoryLikeResult {
        try await APIClient.shared.post("/api/stories/\(storyId.uuidString.lowercased())/like")
    }

    static func storyComments(storyId: UUID) async throws -> [StoryComment] {
        try await APIClient.shared.get("/api/stories/\(storyId.uuidString.lowercased())/comments")
    }

    static func addStoryComment(storyId: UUID, body: String) async throws -> StoryComment {
        try await APIClient.shared.post(
            "/api/stories/\(storyId.uuidString.lowercased())/comments",
            body: AddStoryCommentRequest(body: body)
        )
    }

    static func shareStory(storyId: UUID, targetUserId: UUID, message: String?) async throws -> SocialActionResult {
        try await APIClient.shared.post(
            "/api/stories/\(storyId.uuidString.lowercased())/share",
            body: ShareStoryRequest(targetUserId: targetUserId, message: message)
        )
    }

    static func uploadStoryMedia(data: Data, fileName: String, mimeType: String = "image/jpeg") async throws -> String {
        let result: UploadMediaResult = try await APIClient.shared.upload(
            "/api/stories/media",
            data: data,
            fileName: fileName,
            mimeType: mimeType
        )
        return result.url
    }

    static func uploadAvatar(data: Data, fileName: String, mimeType: String = "image/jpeg") async throws -> EditableUserProfile {
        return try await APIClient.shared.upload(
            "/api/users/me/avatar",
            data: data,
            fileName: fileName,
            mimeType: mimeType
        )
    }

    static func deleteStory(id: UUID) async throws {
        try await APIClient.shared.delete("/api/stories/\(id.uuidString.lowercased())")
    }

    // MARK: - Notifications

    static func notifications() async throws -> [NotificationItem] {
        try await APIClient.shared.get("/api/notifications")
    }

    static func notificationUnreadCount() async throws -> NotificationUnreadCount {
        try await APIClient.shared.get("/api/notifications/unread-count")
    }

    static func markNotificationsRead() async throws {
        let _: EmptyResponse = try await APIClient.shared.post("/api/notifications/mark-read")
    }

    static func deleteNotification(id: UUID) async throws {
        try await APIClient.shared.delete("/api/notifications/\(id.uuidString.lowercased())")
    }

    static func deleteAllNotifications() async throws {
        try await APIClient.shared.delete("/api/notifications")
    }

    // MARK: - Tables

    static func myTables() async throws -> [SocialTableSummary] {
        try await APIClient.shared.get("/api/social/tables/mine")
    }

    static func tableThread(tableId: UUID) async throws -> SocialTableThread {
        try await APIClient.shared.get("/api/social/tables/\(tableId.uuidString.lowercased())/thread")
    }

    static func createTable(_ req: CreateSocialTableRequest) async throws -> SocialTableSummary {
        try await APIClient.shared.post("/api/social/tables", body: req)
    }

    static func inviteToHostedTable(targetUserId: UUID) async throws -> SocialActionResult {
        try await APIClient.shared.post(
            "/api/social/tables/mine/invite",
            body: InviteToHostedTableRequest(targetUserId: targetUserId)
        )
    }

    static func inviteToTable(tableId: UUID, targetUserId: UUID) async throws -> SocialActionResult {
        try await APIClient.shared.post(
            "/api/social/tables/\(tableId.uuidString.lowercased())/invite",
            body: InviteToHostedTableRequest(targetUserId: targetUserId)
        )
    }

    static func joinTable(tableId: UUID) async throws -> SocialActionResult {
        guard let userId = currentUserId else { throw APIError.unauthorized }
        return try await APIClient.shared.post(
            "/api/social/tables/\(tableId.uuidString.lowercased())/join",
            query: ["userId": userId.uuidString.lowercased()]
        )
    }

    static func sendTableMessage(tableId: UUID, body: String) async throws -> SocialActionResult {
        let req = SendSocialTableMessageRequest(body: body)
        return try await APIClient.shared.post("/api/social/tables/\(tableId.uuidString.lowercased())/messages", body: req)
    }

    static func deleteTable(tableId: UUID) async throws -> SocialActionResult {
        try await APIClient.shared.deleteWithResponse("/api/social/tables/\(tableId.uuidString.lowercased())")
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

    // MARK: - Flares

    static func launchFlare(latitude: Double, longitude: Double, message: String, durationHours: Int) async throws -> FlareSignal {
        let req = CreateFlareRequest(latitude: latitude, longitude: longitude, message: message, durationHours: durationHours)
        return try await APIClient.shared.post("/api/social/flares", body: req)
    }

    static func flares() async throws -> [FlareSignal] {
        try await APIClient.shared.get("/api/social/flares")
    }

    static func respondToFlare(flareId: UUID, body: String) async throws -> SocialActionResult {
        try await APIClient.shared.post(
            "/api/social/flares/\(flareId.uuidString.lowercased())/responses",
            body: RespondToFlareRequest(body: body)
        )
    }

    static func deleteFlare(flareId: UUID) async throws -> SocialActionResult {
        try await APIClient.shared.deleteWithResponse("/api/social/flares/\(flareId.uuidString.lowercased())")
    }

    static func relayFlare(flareId: UUID, targetUserIds: [UUID]) async throws -> SocialActionResult {
        try await APIClient.shared.post(
            "/api/social/flares/\(flareId.uuidString.lowercased())/relay",
            body: RelayFlareRequest(targetUserIds: targetUserIds)
        )
    }

    // MARK: - Friends

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

    // MARK: - Privacy & Live Location

    static func mySocialState() async throws -> SocialMeState {
        try await APIClient.shared.get("/api/social/me/state")
    }

    static func updatePrivacy(_ req: UpdatePrivacySettingsRequest) async throws -> SocialMeState {
        try await APIClient.shared.post("/api/social/me/privacy", body: req)
    }

    static func checkIn(venueId: UUID, userId: UUID, ttlMinutes: Int = 180) async throws {
        let req = CreateCheckInRequest(userId: userId, venueId: venueId, ttlMinutes: ttlMinutes)
        let _: IgnoredResponse = try await APIClient.shared.post("/api/social/check-ins", body: req)
    }

    static func updateLiveLocation(userId: UUID, latitude: Double, longitude: Double, accuracyMeters: Double?) async throws -> LiveLocationUpdateResult {
        let req = UpdateLiveLocationRequest(
            userId: userId,
            latitude: latitude,
            longitude: longitude,
            accuracyMeters: accuracyMeters
        )
        return try await APIClient.shared.post("/api/social/live-location", body: req)
    }

    static func stopLiveLocation() async throws {
        let _: SocialActionResult = try await APIClient.shared.post("/api/social/live-location/stop")
    }

    static func createIntention(venueId: UUID, userId: UUID, startsAtUtc: Date, endsAtUtc: Date, note: String?) async throws {
        let req = CreateIntentionRequest(userId: userId, venueId: venueId, startsAtUtc: startsAtUtc, endsAtUtc: endsAtUtc, note: note)
        let _: IgnoredResponse = try await APIClient.shared.post("/api/social/intentions", body: req)
    }

    static func registerDeviceToken(userId: UUID, token: String) async throws {
        let req = RegisterDeviceTokenRequest(userId: userId, platform: "ios", deviceToken: token)
        let _: DeviceTokenRegistrationResult = try await APIClient.shared.post("/api/notifications/device-tokens", body: req)
    }
}
