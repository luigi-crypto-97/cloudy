//
//  RootView.swift
//  Cloudy — Root composer: gestisce auth gate e tab bar.
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
                    .transition(.opacity)
            case .loggedIn:
                MainTabs()
                    .transition(.opacity)
            }
        }
        .animation(.easeInOut(duration: 0.25), value: stateKey)
        .task {
            if case .loading = auth.state {
                await auth.restore()
            }
        }
    }

    private var splash: some View {
        ZStack {
            Theme.Palette.surfaceAlt.ignoresSafeArea()
            VStack(spacing: 16) {
                Image(systemName: "cloud.fill")
                    .font(.system(size: 60))
                    .foregroundStyle(Theme.Palette.honey)
                ProgressView()
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

    var body: some View {
        @Bindable var router = router
        TabView(selection: $router.selectedTab) {
            MapView()
                .tabItem {
                    Label("Mappa", systemImage: "map.fill")
                }
                .tag(AppRouter.Tab.map)

            FeedView()
                .tabItem {
                    Label("In giro", systemImage: "sparkles")
                }
                .tag(AppRouter.Tab.feed)

            TablesView()
                .tabItem {
                    Label("Tavoli", systemImage: "person.3.fill")
                }
                .tag(AppRouter.Tab.tables)

            NotificationsView()
                .tabItem {
                    Label("Attività", systemImage: "bell.fill")
                }
                .tag(AppRouter.Tab.notifications)

            ProfileView()
                .tabItem {
                    Label("Profilo", systemImage: "person.circle.fill")
                }
                .tag(AppRouter.Tab.profile)
        }
        .tint(Theme.Palette.honeyDeep)
    }
}
