//
//  FeedModels.swift
//  Cloudy
//
//  Modelli locali per il feed intelligente. Sono volutamente separati dai DTO
//  API: quando arrivera un endpoint reale potremo fare mapping senza riscrivere
//  ranking e UI.
//

import Foundation
import CoreLocation

enum FeedItem: Identifiable, Hashable {
    case venue(VenueFeedCard)
    case social(SocialMomentCard)

    var id: String {
        switch self {
        case .venue(let card): return card.id
        case .social(let card): return card.id
        }
    }
}

struct VenueFeedCard: Identifiable, Hashable {
    let id: String
    let venueId: UUID?
    let name: String
    let category: String?
    let liveState: VenueLiveState
    let energyScore: Int
    let estimatedCrowd: Int
    let friendsHere: Int
    let friendsArriving: Int
    let growthScore: Int
    let distanceMeters: Double?
    let trend: [Int]
    let friendActivities: [FriendActivity]
    let storyPreviews: [FeedStoryPreview]
    let privacyLevel: FeedPrivacyLevel

    var primaryCopy: String {
        if friendsHere > 0 && friendsArriving > 0 {
            return "\(friendsHere) del tuo giro qui, \(friendsArriving) in arrivo"
        }
        if friendsHere > 0 {
            return "\(friendsHere) del tuo giro sono qui"
        }
        if friendsArriving > 0 {
            return "\(friendsArriving) amici stanno convergendo qui"
        }
        return liveState.copy
    }
}

struct SocialMomentCard: Identifiable, Hashable {
    let id: String
    let title: String
    let subtitle: String
    let venueName: String?
    let avatarUrls: [String?]
    let actionTitle: String
    let privacyLevel: FeedPrivacyLevel
}

struct FriendActivity: Identifiable, Hashable {
    let id: String
    let userId: UUID?
    let displayName: String
    let avatarUrl: String?
    let kind: FriendActivityKind
    let createdAt: Date
    let privacyLevel: FeedPrivacyLevel

    var copy: String {
        switch kind {
        case .arrived:
            return "\(displayName) e appena arrivato"
        case .going:
            return "\(displayName) sta andando"
        case .postedStory:
            return "\(displayName) ha postato una foto qui"
        case .groupConverging(let count):
            return "\(count) persone del tuo giro stanno convergendo"
        }
    }
}

enum FriendActivityKind: Hashable {
    case arrived
    case going
    case postedStory
    case groupConverging(count: Int)
}

enum VenueLiveState: String, Hashable {
    case wakingUp
    case growing
    case hotNow
    case almostFull

    var copy: String {
        switch self {
        case .wakingUp: return "Si sta svegliando"
        case .growing: return "Sta crescendo"
        case .hotNow: return "Caldo ora"
        case .almostFull: return "Quasi pieno"
        }
    }
}

struct FeedStoryPreview: Identifiable, Hashable {
    let id: UUID
    let userId: UUID
    let displayName: String
    let avatarUrl: String?
    let mediaUrl: String?
    let caption: String?
    let createdAt: Date
}

enum FeedPrivacyLevel: String, Hashable {
    case publicVenue
    case friendsFuzzy
    case aggregated
    case mock
}
