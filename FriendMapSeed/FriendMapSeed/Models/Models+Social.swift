//
//  Models+Social.swift
//  Cloudy — Social models
//
//  Modelli per social features: stories, chat, tables, flares, friends.
//

import Foundation
import CoreLocation

// MARK: - Stories

struct UserStory: Codable, Hashable, Identifiable {
    let id: UUID
    let userId: UUID
    let nickname: String
    let displayName: String?
    let avatarUrl: String?
    let mediaUrl: String?
    let caption: String?
    let venueId: UUID?
    let venueName: String?
    var likeCount: Int
    var commentCount: Int
    var hasLiked: Bool
    let createdAtUtc: Date
    let expiresAtUtc: Date
}

struct VenueStory: Codable, Hashable, Identifiable {
    let id: UUID
    let userId: UUID
    let nickname: String
    let displayName: String?
    let avatarUrl: String?
    let mediaUrl: String?
    let caption: String?
    let venueId: UUID
    let venueName: String
    let latitude: Double
    let longitude: Double
    var likeCount: Int
    var commentCount: Int
    var hasLiked: Bool
    let createdAtUtc: Date
    let expiresAtUtc: Date

    var coordinate: CLLocationCoordinate2D {
        CLLocationCoordinate2D(latitude: latitude, longitude: longitude)
    }
}

struct StoryLikeResult: Codable, Hashable {
    let storyId: UUID
    let liked: Bool
    let likeCount: Int
}

struct StoryComment: Codable, Hashable, Identifiable {
    let commentId: UUID
    let storyId: UUID
    let userId: UUID
    let nickname: String
    let displayName: String?
    let avatarUrl: String?
    let body: String
    let createdAtUtc: Date
    let isMine: Bool
    var id: UUID { commentId }
}

struct CreateStoryRequest: Codable {
    let mediaUrl: String
    let caption: String?
    let venueId: UUID?
}

struct AddStoryCommentRequest: Codable {
    let body: String
}

struct ShareStoryRequest: Codable {
    let targetUserId: UUID
    let message: String?
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

struct NotificationUnreadCount: Codable, Hashable {
    let count: Int
}

// MARK: - Social Tables

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

struct CreateSocialTableRequest: Codable {
    let hostUserId: UUID
    let venueId: UUID
    let title: String
    let description: String?
    let startsAtUtc: Date
    let capacity: Int
    let joinPolicy: String
}

struct InviteToHostedTableRequest: Codable {
    let targetUserId: UUID
}

struct SendSocialTableMessageRequest: Codable {
    let body: String
}

// MARK: - Direct Messages (Chat 1:1)

struct DirectMessageThreadSummary: Codable, Hashable, Identifiable {
    let otherUserId: UUID
    let nickname: String
    let displayName: String?
    let avatarUrl: String?
    let lastMessagePreview: String
    let lastMessageAtUtc: Date
    let unreadCount: Int
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

// MARK: - Group Chats

struct CreateGroupChatRequest: Codable {
    let title: String
    let memberUserIds: [UUID]
}

struct GroupChatSummary: Codable, Hashable, Identifiable {
    let chatId: UUID
    let title: String
    let kind: String
    let venueId: UUID?
    let venueName: String?
    let memberCount: Int
    let lastMessagePreview: String
    let lastMessageAtUtc: Date
    let unreadCount: Int
    var id: UUID { chatId }
}

struct GroupChatMessage: Codable, Hashable, Identifiable {
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

struct GroupChatThread: Codable {
    let chat: GroupChatSummary
    let messages: [GroupChatMessage]
}

struct SendGroupChatMessageRequest: Codable {
    let body: String
}

// MARK: - Flares

struct CreateFlareRequest: Codable {
    let latitude: Double
    let longitude: Double
    let message: String
    let durationHours: Int?
}

struct FlareSignal: Codable, Hashable, Identifiable {
    let flareId: UUID
    let userId: UUID
    let nickname: String
    let displayName: String?
    let avatarUrl: String?
    let latitude: Double
    let longitude: Double
    let message: String
    let responseCount: Int
    let createdAtUtc: Date
    let expiresAtUtc: Date
    var id: UUID { flareId }

    var coordinate: CLLocationCoordinate2D {
        CLLocationCoordinate2D(latitude: latitude, longitude: longitude)
    }
}

struct RespondToFlareRequest: Codable {
    let body: String
}

struct RelayFlareRequest: Codable {
    let targetUserIds: [UUID]
}

// MARK: - Friends & Social Hub

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

// MARK: - User Profile

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

// MARK: - Privacy & Live Location

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

struct CreateCheckInRequest: Codable {
    let userId: UUID
    let venueId: UUID
    let ttlMinutes: Int
}

struct UpdateLiveLocationRequest: Codable {
    let userId: UUID
    let latitude: Double
    let longitude: Double
    let accuracyMeters: Double?
}

struct LiveLocationUpdateResult: Codable, Hashable {
    let status: String
    let venueId: UUID?
    let venueName: String?
    let expiresAtUtc: Date?
    let distanceMeters: Double?
}

struct CreateIntentionRequest: Codable {
    let userId: UUID
    let venueId: UUID
    let startsAtUtc: Date
    let endsAtUtc: Date
    let note: String?
}

struct RegisterDeviceTokenRequest: Codable {
    let userId: UUID
    let platform: String
    let deviceToken: String
}

struct DeviceTokenRegistrationResult: Codable {
    let id: UUID
    let userId: UUID
    let platform: String
    let isActive: Bool
}

// MARK: - Social Action Result

struct SocialActionResult: Codable {
    let status: String
    let message: String
}
