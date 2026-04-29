//
//  FeedAnalytics.swift
//  Cloudy
//
//  Analytics privacy-safe: nessuna coordinata raw, niente nomi, niente corpi
//  messaggi/stories. In Debug stampa eventi sintetici; in Release no-op.
//

import Foundation

protocol AnalyticsTracking {
    func track(_ event: AnalyticsEvent)
}

struct AnalyticsEvent: Hashable {
    let name: AnalyticsEventName
    let metadata: [String: String]
    let timestamp: Date
}

enum AnalyticsEventName: String, Hashable {
    case feedOpened = "feed_opened"
    case feedRefreshed = "feed_refreshed"
    case feedCardImpression = "feed_card_impression"
    case feedCardTap = "feed_card_tap"
    case feedCTATap = "feed_cta_tap"
    case feedStoryStackOpened = "feed_story_stack_opened"
    case feedTableJoinStarted = "feed_table_join_started"
    case feedFlareRelayStarted = "feed_flare_relay_started"
    case feedShareStarted = "feed_share_started"
    case privacyExplainerOpened = "privacy_explainer_opened"
    case emptyStateSeen = "empty_state_seen"
    case deepLinkOpened = "deep_link_opened"
}

struct DebugAnalyticsTracker: AnalyticsTracking {
    func track(_ event: AnalyticsEvent) {
        #if DEBUG
        print("[analytics] \(event.name.rawValue) \(event.metadata)")
        #endif
    }
}

struct NoopAnalyticsTracker: AnalyticsTracking {
    func track(_ event: AnalyticsEvent) {}
}

struct FeedAnalytics {
    private let tracker: AnalyticsTracking

    init(tracker: AnalyticsTracking = DebugAnalyticsTracker()) {
        self.tracker = tracker
    }

    func track(_ name: AnalyticsEventName, item: FeedItem? = nil, cta: FeedCTA? = nil, rank: Int? = nil) {
        var metadata: [String: String] = [
            "bucket": "feed_v2"
        ]
        if let item {
            metadata["cardType"] = item.kind.rawValue
            metadata["source"] = item.tracking.source
            metadata["impressionId"] = item.tracking.impressionId
            if let venueId = item.venueId {
                metadata["venueId"] = venueId.uuidString
            }
        }
        if let cta {
            metadata["ctaKind"] = cta.kind.rawValue
        }
        if let rank {
            metadata["rank"] = "\(rank)"
        }
        tracker.track(AnalyticsEvent(name: name, metadata: metadata, timestamp: Date()))
    }
}
