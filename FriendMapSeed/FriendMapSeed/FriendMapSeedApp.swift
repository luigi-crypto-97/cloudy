//
//  FriendMapSeedApp.swift
//  Cloudy — App entry point con Firebase, Analytics, Crashlytics
//

import SwiftUI
import UIKit
import UserNotifications
import CoreData

#if canImport(FirebaseCore)
import FirebaseCore
#endif

#if canImport(FirebaseCrashlytics)
import FirebaseCrashlytics
#endif

@main
struct FriendMapSeedApp: App {
    @UIApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @State private var auth = AuthStore()
    @State private var router = AppRouter()
    @State private var mapStore = MapStore()
    @State private var liveLocation = LiveLocationStore()
    private let dataController = DataController.shared

    var body: some Scene {
        WindowGroup {
            RootView()
                .environment(\.managedObjectContext, dataController.viewContext)
                .environment(auth)
                .environment(router)
                .environment(mapStore)
                .environment(liveLocation)
                .preferredColorScheme(.light)
                .tint(Theme.Palette.honeyDeep)
                .onOpenURL { url in
                    router.open(deepLink: url.absoluteString)
                }
                .onChange(of: auth.state) { _, newState in
                    if case .loggedIn(let user) = newState {
                        NotificationBridge.shared.activate(for: user.userId)
                        liveLocation.configure(userId: user.userId)
                        
                        // Analytics user ID
                        Task { @MainActor in
                            AnalyticsService.shared.userDidLogin(userId: user.userId)
                            CrashReportingService.shared.setUserId(user.userId.uuidString)
                            DeviceCacheService.shared.cleanup()
                        }
                    } else {
                        liveLocation.configure(userId: nil)
                        Task { @MainActor in
                            AnalyticsService.shared.userDidLogout()
                        }
                    }
                }
        }
    }
}

// MARK: - AppDelegate

final class AppDelegate: NSObject, UIApplicationDelegate, UNUserNotificationCenterDelegate {
    
    func application(
        _ application: UIApplication,
        didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]? = nil
    ) -> Bool {
        // Inizializza Firebase se configurato
        #if canImport(FirebaseCore)
        let isFirebaseEnabled = ProcessInfo.processInfo.environment["ENABLE_FIREBASE"] == "1"
        if isFirebaseEnabled {
            FirebaseApp.configure()
            print("[Firebase] Initialized ✅")
        } else {
            print("[Firebase] Disabled (development)")
        }
        #endif
        
        UNUserNotificationCenter.current().delegate = self
        
        // Analytics: app launch
        Task { @MainActor in
            AnalyticsService.shared.appDidLaunch()
        }
        
        return true
    }

    func application(_ application: UIApplication, didRegisterForRemoteNotificationsWithDeviceToken deviceToken: Data) {
        let token = deviceToken.map { String(format: "%02.2hhx", $0) }.joined()
        NotificationBridge.shared.updateDeviceToken(token)
    }

    func application(_ application: UIApplication, didFailToRegisterForRemoteNotificationsWithError error: Error) {
        print("[APNs] Registration failed: \(error.localizedDescription)")
        CrashReportingService.shared.recordError(error, context: "APNs registration")
    }
    
    func applicationDidEnterBackground(_ application: UIApplication) {
        Task { @MainActor in
            DataController.shared.saveIfNeeded()
        }
    }

    func userNotificationCenter(
        _ center: UNUserNotificationCenter,
        willPresent notification: UNNotification
    ) async -> UNNotificationPresentationOptions {
        [.banner, .sound, .badge]
    }
    
    func userNotificationCenter(
        _ center: UNUserNotificationCenter,
        didReceive response: UNNotificationResponse,
        withCompletionHandler completionHandler: @escaping () -> Void
    ) {
        // Handle notification tap
        let userInfo = response.notification.request.content.userInfo
        
        if let deepLink = userInfo["deepLink"] as? String {
            NotificationCenter.default.post(name: .cloudyNotificationTapped, object: deepLink)
        }
        
        completionHandler()
    }
}

// MARK: - Notification Bridge

@MainActor
final class NotificationBridge {
    static let shared = NotificationBridge()

    private var currentUserId: UUID?
    private var deviceToken: String?
    private var didRequestAuthorization = false

    func activate(for userId: UUID) {
        currentUserId = userId
        requestAuthorizationIfNeeded()
        Task { await registerIfPossible() }
    }

    func updateDeviceToken(_ token: String) {
        deviceToken = token
        Task { await registerIfPossible() }
    }

    private func requestAuthorizationIfNeeded() {
        guard !didRequestAuthorization else { return }
        didRequestAuthorization = true
        UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .badge, .sound]) { granted, _ in
            guard granted else { return }
            DispatchQueue.main.async {
                UIApplication.shared.registerForRemoteNotifications()
            }
        }
    }

    private func registerIfPossible() async {
        guard let currentUserId, let deviceToken, !deviceToken.isEmpty else { return }
        do {
            try await API.registerDeviceToken(userId: currentUserId, token: deviceToken)
            print("[Notifications] Device token registered ✅")
        } catch {
            print("[Notifications] Registration failed: \(error.localizedDescription)")
            CrashReportingService.shared.recordError(error, context: "Device token registration")
        }
    }
}

// MARK: - Notification Name Extension

extension Notification.Name {
    static let cloudyNotificationTapped = Notification.Name("cloudyNotificationTapped")
    static let cloudyNewChatMessage = Notification.Name("cloudyNewChatMessage")
}
