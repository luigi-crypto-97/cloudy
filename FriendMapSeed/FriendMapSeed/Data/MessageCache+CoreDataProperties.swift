//
//  MessageCache+CoreDataProperties.swift
//  FriendMapSeed
//
//  Created by luigi negri on 5/4/26.
//
//

public import Foundation
public import CoreData


public typealias MessageCacheCoreDataPropertiesSet = NSSet

extension MessageCache {

    @nonobjc public class func fetchRequest() -> NSFetchRequest<MessageCache> {
        return NSFetchRequest<MessageCache>(entityName: "MessageCache")
    }

    @NSManaged public var avatarUrl: String?
    @NSManaged public var body: String?
    @NSManaged public var displayName: String?
    @NSManaged public var isMine: Bool
    @NSManaged public var lastUpdated: Date?
    @NSManaged public var messageId: String?
    @NSManaged public var nickname: String?
    @NSManaged public var senderUserId: String?
    @NSManaged public var sentAt: String?
    @NSManaged public var userId: String?

}

extension MessageCache : Identifiable {

}
