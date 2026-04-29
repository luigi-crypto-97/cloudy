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
    var pendingVenue: VenueRoute?
    var isTabBarHidden = false

    func openVenue(_ venueId: UUID) {
        selectedTab = .map
        pendingVenue = VenueRoute(venueId: venueId)
    }

    func open(deepLink: String?) {
        guard let deepLink, let url = URL(string: deepLink) else { return }
        if url.scheme?.lowercased() == "cloudy" {
            let type = (url.host ?? "").lowercased()
            let rawId = url.pathComponents.filter { $0 != "/" }.first
            guard let rawId, let id = UUID(uuidString: rawId) else { return }
            open(type: type, id: id)
            return
        }

        let parts = url.pathComponents.filter { $0 != "/" }
        guard let linkIndex = parts.firstIndex(of: "l"), parts.count > linkIndex + 2 else { return }
        let type = parts[linkIndex + 1].lowercased()
        guard let id = UUID(uuidString: parts[linkIndex + 2]) else { return }
        open(type: type, id: id)
    }

    private func open(type: String, id: UUID) {
        switch type {
        case "chat":
            selectedTab = .feed
            presentedChat = ChatRoute(userId: id, title: "Chat")
        case "table":
            selectedTab = .tables
            presentedTable = TableRoute(tableId: id)
        case "venue":
            openVenue(id)
        case "flare":
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

struct VenueRoute: Identifiable, Hashable {
    let venueId: UUID
    var id: UUID { venueId }
}
