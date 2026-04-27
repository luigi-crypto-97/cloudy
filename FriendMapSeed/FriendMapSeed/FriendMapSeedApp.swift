//
//  FriendMapSeedApp.swift
//  Cloudy — App entry point.
//
//  Inietta gli store globali con i nuovi macro `@Environment` di SwiftUI
//  (Observable, iOS 17+).
//

import SwiftUI

@main
struct FriendMapSeedApp: App {
    @State private var auth = AuthStore()
    @State private var router = AppRouter()
    @State private var mapStore = MapStore()

    var body: some Scene {
        WindowGroup {
            RootView()
                .environment(auth)
                .environment(router)
                .environment(mapStore)
                .preferredColorScheme(.light) // identità visiva chiara
                .tint(Theme.Palette.honeyDeep)
        }
    }
}
