//
//  RootView.swift
//  Cloudy — Root composer + custom tab bar.
//

import SwiftUI

struct RootView: View {
    @Environment(AuthStore.self) private var auth
    @Environment(AppRouter.self) private var router
    @Environment(MapStore.self) private var mapStore

    var body: some View {
        Group {
            switch auth.state {
            case .loading:
                splash
            case .loggedOut:
                LoginView()
                    .transition(.opacity.combined(with: .scale(scale: 0.98)))
            case .loggedIn:
                MainTabs()
                    .transition(.opacity.combined(with: .scale(scale: 0.98)))
            }
        }
        .animation(.cloudySoft, value: stateKey)
        .task {
            if case .loading = auth.state {
                await auth.restore()
            }
        }
    }

    // MARK: — Hero moment
    // Splash minimale ma vivo: mesh notturna + logo che respira invece di spinner.
    private var splash: some View {
        ZStack {
            MeshGradientBackground(preset: .auroraNight, speed: 0.10)
            VStack(spacing: 18) {
                Image(systemName: "cloud.fill")
                    .font(.system(size: 64, weight: .black))
                    .foregroundStyle(Theme.Gradients.solar)
                    .breathingScale(amount: 1.08, duration: 1.8)
                LoadingDots()
            }
        }
    }

    private var stateKey: String {
        switch auth.state {
        case .loading: return "loading"
        case .loggedOut: return "out"
        case .loggedIn: return "in"
        }
    }
}

// MARK: - Main tabs

struct MainTabs: View {
    @Environment(AppRouter.self) private var router
    @State private var unreadMessages = 0
    @State private var unreadNotifications = 0

    private var items: [CloudyTabItem<AppRouter.Tab>] {
        [
            .init(id: .map, title: "Mappa", icon: "map.fill"),
            .init(id: .feed, title: "In giro", icon: "sparkles", badge: unreadMessages),
            .init(id: .tables, title: "Tavoli", icon: "person.3.fill"),
            .init(id: .leaderboard, title: "Classifica", icon: "rosette"),
            .init(id: .profile, title: "Profilo", icon: "person.crop.circle.fill")
        ]
    }

    var body: some View {
        @Bindable var router = router
        ZStack(alignment: .bottom) {
            Group {
                switch router.selectedTab {
                case .map:
                    MapView()
                case .feed:
                    FeedView()
                case .tables:
                    TablesView()
                case .leaderboard:
                    GamificationView()
                case .notifications:
                    NotificationsView()
                case .profile:
                    ProfileView()
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .transition(.opacity.combined(with: .scale(scale: 0.985)))

            if !router.isTabBarHidden {
                AnimatedTabBar(selection: $router.selectedTab, items: items)
                    .transition(.move(edge: .bottom).combined(with: .opacity))
            }
        }
        .ignoresSafeArea(.keyboard, edges: .bottom)
        .animation(.cloudySnappy, value: router.selectedTab)
        .animation(.cloudySnappy, value: router.isTabBarHidden)
        .task { await refreshBadges() }
        .onChange(of: router.selectedTab) {
            Task { await refreshBadges() }
        }
        .onReceive(NotificationCenter.default.publisher(for: .cloudyBadgesShouldRefresh)) { _ in
            Task { await refreshBadges() }
        }
        .sheet(item: $router.presentedChat) { route in
            NavigationStack {
                ChatRoomView(otherUserId: route.userId, peerName: route.title)
            }
        }
        .sheet(item: $router.presentedTable) { route in
            NavigationStack {
                TableThreadView(tableId: route.tableId)
            }
        }
    }

    private func refreshBadges() async {
        do {
            async let notifications = API.notificationUnreadCount()
            async let threads = API.messageThreads()
            unreadNotifications = try await notifications.count
            unreadMessages = try await threads.reduce(0) { $0 + $1.unreadCount }
        } catch {
            // Badge refresh is non-blocking UI metadata.
        }
    }
}

extension Notification.Name {
    static let cloudyBadgesShouldRefresh = Notification.Name("cloudyBadgesShouldRefresh")
    static let cloudyStoriesDidChange = Notification.Name("cloudyStoriesDidChange")
}
