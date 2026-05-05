//
//  StoryCache+CoreDataProperties.swift
//  FriendMapSeed
//
//  Created by luigi negri on 5/4/26.
//
//

public import Foundation
public import CoreData


public typealias StoryCacheCoreDataPropertiesSet = NSSet

extension StoryCache {

    @nonobjc public class func fetchRequest() -> NSFetchRequest<StoryCache> {
        return NSFetchRequest<StoryCache>(entityName: "StoryCache")
    }

    @NSManaged public var avatarUrl: String?
    @NSManaged public var caption: String?
    @NSManaged public var commentCount: Int32
    @NSManaged public var createdAt: Date?
    @NSManaged public var displayName: String?
    @NSManaged public var expiresAt: Date?
    @NSManaged public var hasLiked: Bool
    @NSManaged public var lastUpdated: Date?
    @NSManaged public var likeCount: Int32
    @NSManaged public var mediaUrl: String?
    @NSManaged public var nickname: String?
    @NSManaged public var storyId: String?
    @NSManaged public var userId: String?
    @NSManaged public var venueId: String?
    @NSManaged public var venueName: String?

}

extension StoryCache : Identifiable {

}
