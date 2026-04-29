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

    func markRead() async {
        try? await API.markNotificationsRead()
        NotificationCenter.default.post(name: .cloudyBadgesShouldRefresh, object: nil)
    }

    func delete(_ item: NotificationItem) async {
        do {
            try await API.deleteNotification(id: item.id)
            withAnimation(.cloudySnappy) {
                items.removeAll { $0.id == item.id }
            }
            NotificationCenter.default.post(name: .cloudyBadgesShouldRefresh, object: nil)
            Haptics.success()
        } catch {
            Haptics.error()
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    func deleteAll() async {
        do {
            try await API.deleteAllNotifications()
            withAnimation(.cloudySnappy) {
                items.removeAll()
            }
            NotificationCenter.default.post(name: .cloudyBadgesShouldRefresh, object: nil)
            Haptics.success()
        } catch {
            Haptics.error()
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }
}

struct NotificationsView: View {
    @Environment(AppRouter.self) private var router
    @State private var store = NotificationsStore()

    var body: some View {
        NavigationStack {
            List {
                if store.items.isEmpty && !store.isLoading {
                    Section {
                        CloudyEmptyState(
                            icon: "bell",
                            title: "Tutto silenzioso",
                            message: "Quando qualcuno ti invita o accetta i tuoi piani, lo trovi qui."
                        )
                    }
                    .listRowBackground(Color.clear)
                    .listRowSeparator(.hidden)
                }

                ForEach(store.items) { item in
                    Button {
                        router.open(deepLink: item.deepLink)
                    } label: {
                        NotificationRow(item: item)
                    }
                    .buttonStyle(.plain)
                        .listRowInsets(EdgeInsets(top: 6, leading: Theme.Spacing.lg, bottom: 6, trailing: Theme.Spacing.lg))
                        .listRowBackground(Color.clear)
                        .listRowSeparator(.hidden)
                        .swipeActions(edge: .trailing, allowsFullSwipe: true) {
                            Button(role: .destructive) {
                                Task { await store.delete(item) }
                            } label: {
                                Label("Elimina", systemImage: "trash")
                            }
                        }
                }

                if let error = store.error {
                    Section {
                        Text(error)
                            .font(Theme.Font.caption(12))
                            .foregroundStyle(Theme.Palette.densityHigh)
                    }
                    .listRowBackground(Color.clear)
                    .listRowSeparator(.hidden)
                }
            }
            .listStyle(.plain)
            .scrollContentBackground(.hidden)
            .safeAreaPadding(.bottom, 130)
            .background(Theme.Palette.surfaceAlt.ignoresSafeArea())
            .navigationTitle("Attività")
            .navigationBarTitleDisplayMode(.large)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        Task { await store.deleteAll() }
                    } label: {
                        Image(systemName: "trash")
                    }
                    .disabled(store.items.isEmpty)
                }
            }
            .refreshable {
                await store.load()
                await store.markRead()
            }
            .task {
                await store.load()
                await store.markRead()
            }
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
        case let x where x.contains("flare"):  return "bolt.horizontal.circle.fill"
        default:                               return "bell.fill"
        }
    }
}
