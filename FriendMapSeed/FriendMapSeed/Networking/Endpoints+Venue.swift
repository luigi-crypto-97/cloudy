//
//  Endpoints+Venue.swift
//  Cloudy — Venue API endpoints
//

import Foundation

extension API {
    
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

    static func venueRatingReviews(venueId: UUID) async throws -> [VenueRatingReview] {
        try await APIClient.shared.get("/api/venues/\(venueId.uuidString.lowercased())/ratings")
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
}
