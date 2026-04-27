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
        try await APIClient.shared.get(
            "/api/venues/map",
            query: [
                "minLat": String(minLat),
                "minLng": String(minLng),
                "maxLat": String(maxLat),
                "maxLng": String(maxLng),
                "q": query,
                "category": category,
                "openNow": openNow ? "true" : nil,
                "centerLat": centerLat.map(String.init),
                "centerLng": centerLng.map(String.init),
                "maxDistanceKm": maxDistanceKm.map(String.init)
            ]
        )
    }

    static func venueMapLayer(
        minLat: Double, minLng: Double,
        maxLat: Double, maxLng: Double,
        query: String? = nil,
        category: String? = nil,
        openNow: Bool = false
    ) async throws -> VenueMapLayer {
        try await APIClient.shared.get(
            "/api/venues/map-layer",
            query: [
                "minLat": String(minLat),
                "minLng": String(minLng),
                "maxLat": String(maxLat),
                "maxLng": String(maxLng),
                "q": query,
                "category": category,
                "openNow": openNow ? "true" : nil
            ]
        )
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
}
