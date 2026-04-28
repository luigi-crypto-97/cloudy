//
//  FriendMapSeedApp.swift
//  Cloudy — App entry point.
//
//  Inietta gli store globali con i nuovi macro `@Environment` di SwiftUI
//  (Observable, iOS 17+).
//

import SwiftUI
import UIKit
import UserNotifications

@main
struct FriendMapSeedApp: App {
    @UIApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @State private var auth = AuthStore()
    @State private var router = AppRouter()
    @State private var mapStore = MapStore()
    @State private var liveLocation = LiveLocationStore()

    var body: some Scene {
        WindowGroup {
            RootView()
                .environment(auth)
                .environment(router)
                .environment(mapStore)
                .environment(liveLocation)
                .preferredColorScheme(.light) // identità visiva chiara
                .tint(Theme.Palette.honeyDeep)
                .onChange(of: auth.state) { _, newState in
                    if case .loggedIn(let user) = newState {
                        NotificationBridge.shared.activate(for: user.userId)
                        liveLocation.configure(userId: user.userId)
                    } else {
                        liveLocation.configure(userId: nil)
                    }
                }
        }
    }
}

final class AppDelegate: NSObject, UIApplicationDelegate, UNUserNotificationCenterDelegate {
    func application(
        _ application: UIApplication,
        didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]? = nil
    ) -> Bool {
        UNUserNotificationCenter.current().delegate = self
        return true
    }

    func application(_ application: UIApplication, didRegisterForRemoteNotificationsWithDeviceToken deviceToken: Data) {
        let token = deviceToken.map { String(format: "%02.2hhx", $0) }.joined()
        NotificationBridge.shared.updateDeviceToken(token)
    }

    func application(_ application: UIApplication, didFailToRegisterForRemoteNotificationsWithError error: Error) {
        print("APNs registration failed: \(error.localizedDescription)")
    }

    func userNotificationCenter(
        _ center: UNUserNotificationCenter,
        willPresent notification: UNNotification
    ) async -> UNNotificationPresentationOptions {
        [.banner, .sound, .badge]
    }
}

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
        } catch {
            print("Device token registration failed: \(error.localizedDescription)")
        }
    }
}
