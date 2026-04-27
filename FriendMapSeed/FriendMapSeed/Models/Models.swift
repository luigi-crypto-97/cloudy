//
//  Models.swift
//  Cloudy — Domain models
//
//  Specchio degli AuthDtos / VenueDtos / SocialDtos / UserDtos del backend.
//  Tutti `Codable` per (de)serializzare il JSON (camelCase di default).
//

import Foundation
import CoreLocation

// MARK: - Auth

struct AuthUser: Codable, Hashable, Identifiable {
    let userId: UUID
    let nickname: String
    let displayName: String?

    var id: UUID { userId }
}

struct AuthTokenResponse: Codable {
    let accessToken: String
    let expiresAtUtc: Date
    let user: AuthUser
}

struct DevLoginRequest: Codable {
    let nickname: String
    let displayName: String?
}

// MARK: - Geo

struct GeoPoint: Codable, Hashable {
    let latitude: Double
    let longitude: Double

    var coordinate: CLLocationCoordinate2D {
        CLLocationCoordinate2D(latitude: latitude, longitude: longitude)
    }
}

// MARK: - Venue map

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
    let presencePreview: [PresencePreview]

    var id: UUID { venueId }
    var coordinate: CLLocationCoordinate2D {
        CLLocationCoordinate2D(latitude: latitude, longitude: longitude)
    }
}

struct PresencePreview: Codable, Hashable, Identifiable {
    let userId: UUID
    let displayName: String
    let nickname: String
    let avatarUrl: String?
    var id: UUID { userId }
}

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

// MARK: - User profile

struct UserProfile: Codable, Hashable, Identifiable {
    let userId: UUID
    let nickname: String
    let displayName: String?
    let avatarUrl: String?
    let bio: String?
    let birthYear: Int?
    let gender: String
    let isFriend: Bool
    let relationshipStatus: String
    let mutualFriendsCount: Int
    let friendsCount: Int
    let presenceState: String
    let statusLabel: String
    let currentVenueName: String?
    let currentVenueCategory: String?
    let canInviteToTable: Bool
    let canMessageDirectly: Bool
    let canEditProfile: Bool
    let isBlockedByViewer: Bool
    let hasBlockedViewer: Bool
    let interests: [String]

    var id: UUID { userId }
}

// MARK: - Social hub

struct SocialConnection: Codable, Hashable, Identifiable {
    let userId: UUID
    let nickname: String
    let displayName: String?
    let avatarUrl: String?
    let relationshipStatus: String
    let mutualFriendsCount: Int
    let presenceState: String
    let statusLabel: String
    let currentVenueName: String?
    let currentVenueCategory: String?
    var id: UUID { userId }
}

struct SocialTableInvite: Codable, Hashable, Identifiable {
    let tableId: UUID
    let title: String
    let startsAtUtc: Date
    let venueName: String
    let venueCategory: String
    let hostUserId: UUID
    let hostNickname: String
    let hostDisplayName: String?
    let hostAvatarUrl: String?
    var id: UUID { tableId }
}

struct SocialHub: Codable {
    let friends: [SocialConnection]
    let incomingRequests: [SocialConnection]
    let outgoingRequests: [SocialConnection]
    let tableInvites: [SocialTableInvite]
}

// MARK: - Social tables

struct SocialTableSummary: Codable, Hashable, Identifiable {
    let tableId: UUID
    let title: String
    let description: String?
    let startsAtUtc: Date
    let venueName: String
    let venueCategory: String
    let joinPolicy: String
    let isHost: Bool
    let membershipStatus: String
    let capacity: Int
    let requestedCount: Int
    let acceptedCount: Int
    let invitedCount: Int
    var id: UUID { tableId }
}

// MARK: - Stories

struct UserStory: Codable, Hashable, Identifiable {
    let id: UUID
    let userId: UUID
    let nickname: String
    let displayName: String?
    let avatarUrl: String?
    let mediaUrl: String?
    let caption: String?
    let createdAtUtc: Date
    let expiresAtUtc: Date
}

// MARK: - Notifications

struct NotificationItem: Codable, Hashable, Identifiable {
    let id: UUID
    let title: String
    let body: String
    let type: String
    let createdAtUtc: Date
    let isRead: Bool
    let deepLink: String?
}

// MARK: - Editable profile

struct EditableUserProfile: Codable, Hashable {
    let userId: UUID
    let nickname: String
    var displayName: String?
    var avatarUrl: String?
    var discoverablePhone: String?
    var discoverableEmail: String?
    var bio: String?
    var birthYear: Int?
    var gender: String
    var interests: [String]
}

struct UpdateMyProfileRequest: Codable {
    let displayName: String?
    let avatarUrl: String?
    let bio: String?
    let birthYear: Int?
    let gender: String?
    let interests: [String]?
}

// MARK: - User search

struct UserSearchResult: Codable, Hashable, Identifiable {
    let userId: UUID
    let nickname: String
    let displayName: String?
    let avatarUrl: String?
    let relationshipStatus: String
    let isBlockedByViewer: Bool
    let hasBlockedViewer: Bool
    let interests: [String]
    var id: UUID { userId }
}

// MARK: - Direct messages

struct DirectMessageThreadSummary: Codable, Hashable, Identifiable {
    let otherUserId: UUID
    let nickname: String
    let displayName: String?
    let avatarUrl: String?
    let lastMessagePreview: String
    let lastMessageAtUtc: Date
    var id: UUID { otherUserId }
}

struct DirectMessage: Codable, Hashable, Identifiable {
    let messageId: UUID
    let senderUserId: UUID
    let nickname: String
    let displayName: String?
    let avatarUrl: String?
    let body: String
    let sentAtUtc: Date
    let isMine: Bool
    var id: UUID { messageId }
}

struct DirectMessagePeer: Codable, Hashable {
    let userId: UUID
    let nickname: String
    let displayName: String?
    let avatarUrl: String?
    let isBlockedByViewer: Bool
    let hasBlockedViewer: Bool
}

struct DirectMessageThread: Codable {
    let otherUser: DirectMessagePeer
    let messages: [DirectMessage]
}

struct SendDirectMessageRequest: Codable {
    let body: String
}

// MARK: - Social table thread

struct SocialTableMessage: Codable, Hashable, Identifiable {
    let messageId: UUID
    let userId: UUID
    let nickname: String
    let displayName: String?
    let avatarUrl: String?
    let body: String
    let sentAtUtc: Date
    let isMine: Bool
    var id: UUID { messageId }
}

struct SocialTableRequest: Codable, Hashable, Identifiable {
    let userId: UUID
    let nickname: String
    let displayName: String?
    let avatarUrl: String?
    let status: String
    var id: UUID { userId }
}

struct SocialTableThread: Codable {
    let table: SocialTableSummary
    let requests: [SocialTableRequest]
    let messages: [SocialTableMessage]
}

struct SendSocialTableMessageRequest: Codable {
    let body: String
}

// MARK: - Flares

struct CreateFlareRequest: Codable {
    let latitude: Double
    let longitude: Double
    let message: String
}

// MARK: - Privacy

struct SocialMeState: Codable, Hashable {
    let isGhostModeEnabled: Bool
    let sharePresenceWithFriends: Bool
    let shareIntentionsWithFriends: Bool
    let activeCheckInVenueId: UUID?
    let activeCheckInVenueName: String?
    let activeIntentionVenueId: UUID?
    let activeIntentionVenueName: String?
}

struct UpdatePrivacySettingsRequest: Codable {
    let isGhostModeEnabled: Bool?
    let sharePresenceWithFriends: Bool?
    let shareIntentionsWithFriends: Bool?
}

// MARK: - Social action result

struct SocialActionResult: Codable {
    let status: String
    let message: String
}

// MARK: - Stories

struct CreateStoryRequest: Codable {
    let mediaUrl: String
    let caption: String?
}

// MARK: - Helpers

extension UUID {
    /// Empty UUID (000...) — usato per stato "non autenticato".
    static let empty = UUID(uuid: (0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0))
}
