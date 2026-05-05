//
//  Analytics.swift
//  Cloudy — Firebase Analytics wrapper type-safe
//
//  Usage:
//    AnalyticsService.shared.logLogin()
//    AnalyticsService.shared.logViewVenue(venueId: uuid, category: "bar")
//

import Foundation

#if canImport(FirebaseAnalytics)
import FirebaseAnalytics
#endif

#if canImport(FirebaseCore)
import FirebaseCore
#endif

#if canImport(FirebaseCrashlytics)
import FirebaseCrashlytics
#endif

// MARK: - Analytics Service

@MainActor
final class AnalyticsService {
    
    static let shared = AnalyticsService()
    
    private var isAnalyticsEnabled: Bool {
        #if canImport(FirebaseCore)
        guard FirebaseApp.app() != nil else { return false }
        #endif
        #if DEBUG
        return ProcessInfo.processInfo.environment["ENABLE_ANALYTICS"] == "1"
        #else
        return true
        #endif
    }
    
    private init() {}
    
    // MARK: - App Lifecycle
    
    func appDidLaunch() {
        logEvent("app_launch", parameters: [:])
    }
    
    func userDidLogin(userId: UUID) {
        #if canImport(FirebaseAnalytics)
        if isAnalyticsEnabled {
            Analytics.setUserID(userId.uuidString)
        }
        #endif
        
        logEvent("user_login", parameters: [:])
    }
    
    func userDidLogout() {
        #if canImport(FirebaseAnalytics)
        if isAnalyticsEnabled {
            Analytics.setUserID(nil)
        }
        #endif
        
        logEvent("user_logout", parameters: [:])
    }
    
    // MARK: - Navigation
    
    func logScreenView(screenName: String, screenClass: String? = nil) {
        #if canImport(FirebaseAnalytics)
        if isAnalyticsEnabled {
            Analytics.logEvent(AnalyticsEventScreenView, parameters: [
                AnalyticsParameterScreenName: screenName,
                AnalyticsParameterScreenClass: screenClass ?? "Unknown"
            ])
        }
        #endif
        
        logEvent("screen_view", parameters: [
            "screen_name": screenName,
            "screen_class": screenClass ?? "Unknown"
        ])
    }
    
    // MARK: - Map & Venues
    
    func logViewMap(venueCount: Int, hasLiveLocation: Bool) {
        logEvent("view_map", parameters: [
            "venue_count": venueCount,
            "has_live_location": hasLiveLocation
        ])
    }
    
    func logViewVenue(venueId: UUID, category: String, distanceKm: Double? = nil) {
        logEvent("view_venue", parameters: [
            "venue_id": venueId.uuidString,
            "category": category,
            "distance_km": distanceKm ?? 0
        ])
    }
    
    func logCheckIn(venueId: UUID, venueName _: String) {
        logEvent("check_in", parameters: [
            "venue_id": venueId.uuidString
        ])
    }
    
    func logLaunchFlare(venueId: UUID? = nil) {
        logEvent("launch_flare", parameters: [
            "has_venue": venueId != nil
        ])
    }
    
    // MARK: - Social
    
    func logJoinTable(tableId: UUID, tableTitle _: String) {
        logEvent("join_table", parameters: [
            "table_id": tableId.uuidString
        ])
    }
    
    func logCreateTable(tableId: UUID, capacity: Int) {
        logEvent("create_table", parameters: [
            "table_id": tableId.uuidString,
            "capacity": capacity
        ])
    }
    
    func logSendMessage(chatType: String, hasAttachment: Bool = false) {
        logEvent("send_message", parameters: [
            "chat_type": chatType,
            "has_attachment": hasAttachment
        ])
    }
    
    func logAddFriend(userId: UUID) {
        logEvent("add_friend", parameters: [
            "user_id": userId.uuidString
        ])
    }
    
    // MARK: - Stories
    
    func logCreateStory(hasCaption: Bool, hasVenue: Bool) {
        logEvent("create_story", parameters: [
            "has_caption": hasCaption,
            "has_venue": hasVenue
        ])
    }
    
    func logViewStory(storyId: UUID, isFriend: Bool) {
        logEvent("view_story", parameters: [
            "story_id": storyId.uuidString,
            "is_friend": isFriend
        ])
    }
    
    func logLikeStory(storyId: UUID) {
        logEvent("like_story", parameters: [
            "story_id": storyId.uuidString
        ])
    }
    
    func logCommentStory(storyId: UUID, commentLength: Int) {
        logEvent("comment_story", parameters: [
            "story_id": storyId.uuidString,
            "comment_length": commentLength
        ])
    }
    
    // MARK: - Gamification
    
    func logLevelUp(newLevel: Int, totalPoints: Int) {
        logEvent("level_up", parameters: [
            "new_level": newLevel,
            "total_points": totalPoints
        ])
    }
    
    func logMissionComplete(missionCode: String, rewardPoints: Int) {
        logEvent("mission_complete", parameters: [
            "mission_code": missionCode,
            "reward_points": rewardPoints
        ])
    }
    
    func logBadgeEarned(badgeCode: String) {
        logEvent("badge_earned", parameters: [
            "badge_code": badgeCode
        ])
    }
    
    // MARK: - Errors & Performance
    
    func logError(errorCode: String, message _: String, screen: String? = nil) {
        logEvent("error", parameters: [
            "error_code": errorCode,
            "screen": screen ?? "Unknown"
        ])
    }
    
    func logNetworkRequest(path: String, method: String, durationMs: Double, statusCode: Int) {
        // Solo per debug, non loggare in produzione per non sporcare analytics
        #if DEBUG
        logEvent("network_request", parameters: [
            "path": path,
            "method": method,
            "duration_ms": durationMs,
            "status_code": statusCode
        ])
        #endif
    }
    
    // MARK: - Private Helpers
    
    private func logEvent(_ name: String, parameters: [String: Any]) {
        #if canImport(FirebaseAnalytics)
        if isAnalyticsEnabled {
            var firebaseParams: [String: Any] = [:]
            for (key, value) in parameters {
                // Firebase accetta solo tipi primitivi
                if let string = value as? String {
                    firebaseParams[key] = string
                } else if let int = value as? Int {
                    firebaseParams[key] = int
                } else if let double = value as? Double {
                    firebaseParams[key] = double
                } else if let bool = value as? Bool {
                    firebaseParams[key] = bool
                }
            }
            Analytics.logEvent(name, parameters: firebaseParams)
        }
        #endif
        
        // Logging per debug
        #if DEBUG
        if ProcessInfo.processInfo.environment["LOG_ANALYTICS"] == "1" {
            print("[Analytics] \(name): \(parameters)")
        }
        #endif
    }
}

// MARK: - Crashlytics Integration

@MainActor
final class CrashReportingService {
    
    static let shared = CrashReportingService()
    
    private var isCrashlyticsEnabled: Bool {
        #if canImport(FirebaseCore)
        guard FirebaseApp.app() != nil else { return false }
        #endif
        #if DEBUG
        return ProcessInfo.processInfo.environment["ENABLE_CRASHLYTICS"] == "1"
        #else
        return true
        #endif
    }
    
    private init() {}
    
    func recordError(_ error: Error, context: String? = nil) {
        #if canImport(FirebaseCrashlytics)
        if isCrashlyticsEnabled {
            Crashlytics.crashlytics().record(error: error)
            if let context {
                Crashlytics.crashlytics().setCustomValue(context, forKey: "context")
            }
        }
        #endif
    }
    
    func recordMessage(_ message: String) {
        #if canImport(FirebaseCrashlytics)
        if isCrashlyticsEnabled {
            Crashlytics.crashlytics().log(message)
        }
        #endif
    }
    
    func setUserId(_ userId: String) {
        #if canImport(FirebaseCrashlytics)
        if isCrashlyticsEnabled {
            Crashlytics.crashlytics().setUserID(userId)
        }
        #endif
    }
    
    func setCustomValue(_ value: Any, forKey key: String) {
        #if canImport(FirebaseCrashlytics)
        if isCrashlyticsEnabled {
            Crashlytics.crashlytics().setCustomValue(value, forKey: key)
        }
        #endif
    }
}
