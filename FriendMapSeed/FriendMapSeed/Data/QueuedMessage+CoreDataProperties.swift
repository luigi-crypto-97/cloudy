//
//  QueuedMessage+CoreDataProperties.swift
//  FriendMapSeed
//
//  Created by luigi negri on 5/4/26.
//
//

public import Foundation
public import CoreData


public typealias QueuedMessageCoreDataPropertiesSet = NSSet

extension QueuedMessage {

    @nonobjc public class func fetchRequest() -> NSFetchRequest<QueuedMessage> {
        return NSFetchRequest<QueuedMessage>(entityName: "QueuedMessage")
    }

    @NSManaged public var attempts: Int32
    @NSManaged public var body: String?
    @NSManaged public var createdAt: Date?
    @NSManaged public var fileName: String?
    @NSManaged public var localFilePath: String?
    @NSManaged public var messageId: String?
    @NSManaged public var mimeType: String?
    @NSManaged public var threadId: String?
    @NSManaged public var userId: String?

}

extension QueuedMessage : Identifiable {

}
