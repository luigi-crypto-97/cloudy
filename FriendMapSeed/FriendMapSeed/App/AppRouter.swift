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
    var presentedChat: ChatRoute?
    var presentedTable: TableRoute?
    var isTabBarHidden = false

    func open(deepLink: String?) {
        guard let deepLink, let url = URL(string: deepLink) else { return }
        let parts = url.pathComponents.filter { $0 != "/" }
        guard let linkIndex = parts.firstIndex(of: "l"), parts.count > linkIndex + 2 else { return }
        let type = parts[linkIndex + 1].lowercased()
        guard let id = UUID(uuidString: parts[linkIndex + 2]) else { return }

        switch type {
        case "chat":
            selectedTab = .feed
            presentedChat = ChatRoute(userId: id, title: "Chat")
        case "table":
            selectedTab = .tables
            presentedTable = TableRoute(tableId: id)
        case "flare", "venue":
            selectedTab = .map
        default:
            selectedTab = .notifications
        }
    }
}

struct ChatRoute: Identifiable, Hashable {
    let userId: UUID
    let title: String
    var id: UUID { userId }
}

struct TableRoute: Identifiable, Hashable {
    let tableId: UUID
    var id: UUID { tableId }
}
