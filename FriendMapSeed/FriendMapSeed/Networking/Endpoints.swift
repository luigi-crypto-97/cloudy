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
        openNow: Bool = false,
        centerLat: Double? = nil, centerLng: Double? = nil,
        maxDistanceKm: Double? = nil
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
        if let v = centerLat { q["centerLat"] = String(v) }
        if let v = centerLng { q["centerLng"] = String(v) }
        if let v = maxDistanceKm { q["maxDistanceKm"] = String(v) }
        return try await APIClient.shared.get("/api/venues/map-layer", query: q.mapValues { Optional($0) })
    }

    static func venueMarker(venueId: UUID) async throws -> VenueMarker {
        try await APIClient.shared.get("/api/venues/\(venueId.uuidString.lowercased())/marker")
    }

    static func venueRating(venueId: UUID) async throws -> VenueRatingSummary {
        try await APIClient.shared.get("/api/venues/\(venueId.uuidString.lowercased())/rating")
    }

    static func rateVenue(venueId: UUID, stars: Int, comment: String? = nil) async throws -> VenueRatingSummary {
        try await APIClient.shared.post(
            "/api/venues/\(venueId.uuidString.lowercased())/rating",
            body: RateVenueRequest(stars: stars, comment: comment)
        )
    }

    static func reportVenueRating(venueId: UUID, ratingId: UUID, reasonCode: String = "fake_venue_rating", details: String? = nil) async throws -> SocialActionResult {
        try await APIClient.shared.post(
            "/api/venues/\(venueId.uuidString.lowercased())/ratings/\(ratingId.uuidString.lowercased())/report",
            body: ReportVenueRatingRequest(reasonCode: reasonCode, details: details)
        )
    }

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

    static func deleteDirectMessageThread(otherUserId: UUID) async throws {
        try await APIClient.shared.delete("/api/messages/threads/\(otherUserId.uuidString.lowercased())")
    }

    static func uploadChatFile(data: Data, fileName: String, mimeType: String = "application/octet-stream") async throws -> String {
        let result: UploadMediaResult = try await APIClient.shared.upload(
            "/api/messages/files",
            data: data,
            fileName: fileName,
            mimeType: mimeType
        )
        return result.url
    }

    // MARK: - Group / venue chats

    static func groupChats() async throws -> [GroupChatSummary] {
        try await APIClient.shared.get("/api/messages/groups")
    }

    static func createGroupChat(title: String, memberUserIds: [UUID]) async throws -> GroupChatSummary {
        try await APIClient.shared.post(
            "/api/messages/groups",
            body: CreateGroupChatRequest(title: title, memberUserIds: memberUserIds)
        )
    }

    static func groupChatThread(chatId: UUID) async throws -> GroupChatThread {
        try await APIClient.shared.get("/api/messages/groups/\(chatId.uuidString.lowercased())")
    }

    static func sendGroupChatMessage(chatId: UUID, body: String) async throws -> GroupChatMessage {
        try await APIClient.shared.post(
            "/api/messages/groups/\(chatId.uuidString.lowercased())/messages",
            body: SendGroupChatMessageRequest(body: body)
        )
    }

    static func deleteGroupChat(chatId: UUID) async throws {
        try await APIClient.shared.delete("/api/messages/groups/\(chatId.uuidString.lowercased())")
    }

    static func venueChatThread(venueId: UUID) async throws -> GroupChatThread {
        try await APIClient.shared.get("/api/messages/venues/\(venueId.uuidString.lowercased())/chat")
    }

    static func sendVenueChatMessage(venueId: UUID, body: String) async throws -> GroupChatMessage {
        try await APIClient.shared.post(
            "/api/messages/venues/\(venueId.uuidString.lowercased())/chat/messages",
            body: SendGroupChatMessageRequest(body: body)
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

    // MARK: - Flare

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

    // MARK: - Privacy / Ghost mode

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

    static var currentUserId: UUID? {
        guard let token = APIClient.shared.bearerToken else { return nil }
        let parts = token.split(separator: ".")
        guard parts.count >= 2 else { return nil }
        var payload = String(parts[1])
            .replacingOccurrences(of: "-", with: "+")
            .replacingOccurrences(of: "_", with: "/")
        while payload.count % 4 != 0 { payload.append("=") }
        guard
            let data = Data(base64Encoded: payload),
            let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any]
        else { return nil }

        let id = json["sub"] as? String
            ?? json["nameid"] as? String
            ?? json["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"] as? String
        return id.flatMap(UUID.init(uuidString:))
    }
}
