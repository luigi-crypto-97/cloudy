//
//  Models+Geo.swift
//  Cloudy — Geo models
//
//  Modelli per geolocalizzazione e coordinate.
//

import Foundation
import CoreLocation

// MARK: - Geo Point

struct GeoPoint: Codable, Hashable {
    let latitude: Double
    let longitude: Double

    var coordinate: CLLocationCoordinate2D {
        CLLocationCoordinate2D(latitude: latitude, longitude: longitude)
    }
}

// MARK: - Venue Marker

struct VenueMarker: Codable, Hashable, Identifiable {
    let venueId: UUID
    let name: String
    let category: String
    let addressLine: String
    let city: String
    let phoneNumber: String?
    let websiteUrl: String?
    let hoursSummary: String?
    let coverImageUrl: String?
    let description: String?
    let tags: [String]
    let latitude: Double
    let longitude: Double
    let isOpenNow: Bool
    let peopleEstimate: Int
    let densityLevel: String
    let bubbleIntensity: Int
    let demographicDataAvailable: Bool
    let activeCheckIns: Int
    let activeIntentions: Int
    let openTables: Int
    let partyPulse: PartyPulse
    let intentRadar: IntentRadar
    let presencePreview: [PresencePreview]
    let averageRating: Double
    let ratingCount: Int
    let myRating: Int?

    var id: UUID { venueId }
    
    var coordinate: CLLocationCoordinate2D {
        CLLocationCoordinate2D(latitude: latitude, longitude: longitude)
    }
}

// MARK: - Party Pulse

struct PartyPulse: Codable, Hashable {
    let energyScore: Int
    let mood: String
    let arrivalsLast15: Int
    let checkInsNow: Int
    let intentionsSoon: Int
    let sparkline: [Int]
}

// MARK: - Intent Radar

struct IntentRadar: Codable, Hashable {
    let goingOut: Int
    let almostThere: Int
    let hereNow: Int
    let coolingDown: Int
    let updatedAtUtc: Date
    let privacyLevel: String
}

// MARK: - Presence Preview

struct PresencePreview: Codable, Hashable, Identifiable {
    let userId: UUID
    let displayName: String
    let nickname: String
    let avatarUrl: String?
    var id: UUID { userId }
}

// MARK: - Venue Rating

struct VenueRatingSummary: Codable, Hashable {
    let venueId: UUID
    let averageRating: Double
    let ratingCount: Int
    let myRating: Int?
    let myRatingId: UUID?
    let myRatingIsVerified: Bool
    let myRatingEarnsPoints: Bool
}

struct VenueRatingReview: Codable, Hashable, Identifiable {
    let ratingId: UUID
    let userId: UUID
    let nickname: String
    let displayName: String?
    let avatarUrl: String?
    let stars: Int
    let comment: String?
    let isVerifiedVisit: Bool
    let isMine: Bool
    let createdAtUtc: Date

    var id: UUID { ratingId }
}

struct RateVenueRequest: Codable {
    let stars: Int
    let comment: String?
}

struct ReportVenueRatingRequest: Codable {
    let reasonCode: String
    let details: String?
}

// MARK: - Venue Map Layer

struct VenueMapArea: Codable, Hashable, Identifiable {
    let areaId: String
    let label: String
    let centroidLatitude: Double
    let centroidLongitude: Double
    let peopleCount: Int
    let densityLevel: String
    let bubbleIntensity: Int
    let venueCount: Int
    let activeCheckIns: Int
    let activeIntentions: Int
    let openTables: Int
    let presenceCount: Int
    let venueIds: [UUID]
    let polygon: [GeoPoint]
    let presencePreview: [PresencePreview]

    var id: String { areaId }
    
    var centroid: CLLocationCoordinate2D {
        CLLocationCoordinate2D(latitude: centroidLatitude, longitude: centroidLongitude)
    }
}

struct VenueMapLayer: Codable {
    let markers: [VenueMarker]
    let areas: [VenueMapArea]
}
