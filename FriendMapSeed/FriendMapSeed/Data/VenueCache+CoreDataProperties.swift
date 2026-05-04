//
//  VenueCache+CoreDataProperties.swift
//  FriendMapSeed
//
//  Created by luigi negri on 5/4/26.
//
//

public import Foundation
public import CoreData


public typealias VenueCacheCoreDataPropertiesSet = NSSet

extension VenueCache {

    @nonobjc public class func fetchRequest() -> NSFetchRequest<VenueCache> {
        return NSFetchRequest<VenueCache>(entityName: "VenueCache")
    }

    @NSManaged public var addressLine: String?
    @NSManaged public var averageRating: Double
    @NSManaged public var bubbleIntensity: Int32
    @NSManaged public var category: String?
    @NSManaged public var city: String?
    @NSManaged public var coverImageUrl: String?
    @NSManaged public var densityLevel: String?
    @NSManaged public var descriptionText: String?
    @NSManaged public var hoursSummary: String?
    @NSManaged public var isOpenNow: Bool
    @NSManaged public var lastUpdated: Date?
    @NSManaged public var latitude: Double
    @NSManaged public var longitude: Double
    @NSManaged public var name: String?
    @NSManaged public var peopleEstimate: Int32
    @NSManaged public var phoneNumber: String?
    @NSManaged public var ratingCount: Int32
    @NSManaged public var venueId: String?
    @NSManaged public var websiteUrl: String?

}

extension VenueCache : Identifiable {

}
