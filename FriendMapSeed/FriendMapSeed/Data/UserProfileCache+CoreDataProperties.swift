//
//  UserProfileCache+CoreDataProperties.swift
//  FriendMapSeed
//
//  Created by luigi negri on 5/4/26.
//
//

public import Foundation
public import CoreData


public typealias UserProfileCacheCoreDataPropertiesSet = NSSet

extension UserProfileCache {

    @nonobjc public class func fetchRequest() -> NSFetchRequest<UserProfileCache> {
        return NSFetchRequest<UserProfileCache>(entityName: "UserProfileCache")
    }

    @NSManaged public var avatarUrl: String?
    @NSManaged public var bio: String?
    @NSManaged public var displayName: String?
    @NSManaged public var lastUpdated: Date?
    @NSManaged public var nickname: String?
    @NSManaged public var userId: String?

}

extension UserProfileCache : Identifiable {

}
