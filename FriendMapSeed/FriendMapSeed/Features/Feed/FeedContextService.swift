//
//  FeedContextService.swift
//  Cloudy
//
//  Aggrega segnali sociali gia presenti nell'app. FeedView resta UI pura:
//  orchestration, fallback e privacy stanno qui.
//

import CoreLocation
import Foundation
import MapKit

struct FeedContext {
    let stories: [UserStory]
    let socialHub: SocialHub?
    let profile: EditableUserProfile?
    let venues: [VenueMarker]
    let venueStories: [VenueStory]
    let flares: [FlareSignal]
    let tables: [SocialTableSummary]
    let myLocation: CLLocation?
    let privacyState: SocialMeState?
    let serverFeed: FeedServerResponse?
    let now: Date
    let isDebugDemo: Bool
}

struct FeedContextService {
    func load(location: CLLocation?, previousContext: FeedContext? = nil) async -> FeedContext {
        let now = Date()
        let bounds = bounds(around: location?.coordinate ?? MapStore.milanDefault.center)

        async let serverFeedTask: FeedServerResponse? = optional {
            try await API.feed(
                latitude: location?.coordinate.latitude,
                longitude: location?.coordinate.longitude
            )
        }
        async let storiesTask: [UserStory]? = optional { try await API.stories() }
        async let hubTask: SocialHub? = optional { try await API.socialHub() }
        async let profileTask: EditableUserProfile? = optional { try await API.myEditableProfile() }
        async let venueLayerTask: VenueMapLayer? = optional {
            try await API.venueMapLayer(
                minLat: bounds.minLat,
                minLng: bounds.minLng,
                maxLat: bounds.maxLat,
                maxLng: bounds.maxLng,
                centerLat: location?.coordinate.latitude,
                centerLng: location?.coordinate.longitude,
                maxDistanceKm: location == nil ? nil : 12
            )
        }
        async let venueStoriesTask: [VenueStory]? = optional {
            try await API.venueStories(
                minLat: bounds.minLat,
                minLng: bounds.minLng,
                maxLat: bounds.maxLat,
                maxLng: bounds.maxLng
            )
        }
        async let flaresTask: [FlareSignal]? = optional { try await API.flares() }
        async let tablesTask: [SocialTableSummary]? = optional { try await API.myTables() }
        async let privacyTask: SocialMeState? = optional { try await API.mySocialState() }

        let serverFeed = await serverFeedTask
        let stories = await storiesTask ?? previousContext?.stories ?? []
        let hub = await hubTask ?? previousContext?.socialHub
        let profile = await profileTask ?? previousContext?.profile
        let venueLayer = await venueLayerTask
        let venues = serverFeed?.venues ?? venueLayer?.markers ?? previousContext?.venues ?? []
        let venueStories = await venueStoriesTask ?? previousContext?.venueStories ?? []
        let fetchedFlares = await flaresTask
        let fetchedTables = await tablesTask
        let flares = serverFeed?.flares ?? fetchedFlares ?? previousContext?.flares ?? []
        let tables = serverFeed?.tables ?? fetchedTables ?? previousContext?.tables ?? []
        let privacy = await privacyTask ?? previousContext?.privacyState

        let context = FeedContext(
            stories: stories,
            socialHub: hub,
            profile: profile,
            venues: venues,
            venueStories: venueStories,
            flares: flares,
            tables: tables,
            myLocation: location,
            privacyState: privacy,
            serverFeed: serverFeed ?? previousContext?.serverFeed,
            now: now,
            isDebugDemo: false
        )

        #if DEBUG
        if isEmpty(context) {
            return FeedDemoFactory.context(now: now)
        }
        #endif

        return context
    }

    private func optional<T>(_ block: @escaping () async throws -> T) async -> T? {
        do {
            return try await block()
        } catch {
            return nil
        }
    }

    private func isEmpty(_ context: FeedContext) -> Bool {
        context.stories.isEmpty &&
        context.venues.isEmpty &&
        context.venueStories.isEmpty &&
        context.flares.isEmpty &&
        context.tables.isEmpty &&
        (context.socialHub?.friends.isEmpty ?? true) &&
        (context.socialHub?.tableInvites.isEmpty ?? true)
    }

    private func bounds(around coordinate: CLLocationCoordinate2D) -> (minLat: Double, minLng: Double, maxLat: Double, maxLng: Double) {
        let delta = 0.065
        return (
            minLat: coordinate.latitude - delta,
            minLng: coordinate.longitude - delta,
            maxLat: coordinate.latitude + delta,
            maxLng: coordinate.longitude + delta
        )
    }
}
