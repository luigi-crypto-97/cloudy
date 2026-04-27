//
//  NotificationsView.swift
//  Cloudy — Centro notifiche
//

import SwiftUI

@MainActor
@Observable
final class NotificationsStore {
    var items: [NotificationItem] = []
    var isLoading: Bool = false
    var error: String?

    func load() async {
        isLoading = true
        error = nil
        defer { isLoading = false }
        do {
            items = try await API.notifications()
        } catch {
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }
}

struct NotificationsView: View {
    @State private var store = NotificationsStore()

    var body: some View {
        NavigationStack {
            ScrollView {
                LazyVStack(spacing: Theme.Spacing.sm) {
                    if store.items.isEmpty && !store.isLoading {
                        CloudyEmptyState(
                            icon: "bell",
                            title: "Tutto silenzioso",
                            message: "Quando qualcuno ti invita o accetta i tuoi piani, lo trovi qui."
                        )
                        .padding(.top, 60)
                    }
                    ForEach(store.items) { item in
                        NotificationRow(item: item)
                    }
                }
                .padding(Theme.Spacing.lg)
                .padding(.bottom, 130)
            }
            .background(Theme.Palette.surfaceAlt.ignoresSafeArea())
            .navigationTitle("Attività")
            .navigationBarTitleDisplayMode(.large)
            .refreshable { await store.load() }
            .task { await store.load() }
        }
    }
}

struct NotificationRow: View {
    let item: NotificationItem
    var body: some View {
        SectionCard {
            HStack(spacing: 12) {
                ZStack {
                    Circle().fill(Theme.Palette.honeySoft).frame(width: 40, height: 40)
                    Image(systemName: iconForType(item.type))
                        .foregroundStyle(Theme.Palette.honeyDeep)
                }
                VStack(alignment: .leading, spacing: 2) {
                    Text(item.title).font(Theme.Font.body(14, weight: .bold))
                    Text(item.body)
                        .font(Theme.Font.caption(12))
                        .foregroundStyle(Theme.Palette.inkSoft)
                        .lineLimit(2)
                    Text(item.createdAtUtc, format: .relative(presentation: .named))
                        .font(Theme.Font.caption(11))
                        .foregroundStyle(Theme.Palette.inkMuted)
                }
                Spacer()
                if !item.isRead {
                    Circle().fill(Theme.Palette.honey).frame(width: 8, height: 8)
                }
            }
        }
    }

    private func iconForType(_ t: String) -> String {
        switch t.lowercased() {
        case let x where x.contains("friend"): return "person.badge.plus"
        case let x where x.contains("table"):  return "person.3.fill"
        case let x where x.contains("message"):return "bubble.left.fill"
        default:                               return "bell.fill"
        }
    }
}
