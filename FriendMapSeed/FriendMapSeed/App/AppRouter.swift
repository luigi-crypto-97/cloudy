//
//  AppRouter.swift
//  Cloudy — Routing tra tab e sheet a livello root
//

import SwiftUI

@MainActor
@Observable
final class AppRouter {

    enum Tab: Hashable {
        case map
        case feed
        case tables
        case notifications
        case profile
    }

    var selectedTab: Tab = .map
}
