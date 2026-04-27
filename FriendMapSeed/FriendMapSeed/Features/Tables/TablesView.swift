//
//  TablesView.swift
//  Cloudy — Tavoli sociali (card swipe stile Bumble)
//
//  Mostra inviti a tavoli + tavoli aperti vicino. Le card hanno gesture
//  drag-rotate-swipe come Bumble.
//

import SwiftUI

@MainActor
@Observable
final class TablesStore {
    var myTables: [SocialTableSummary] = []
    var hub: SocialHub?
    var isLoading: Bool = false
    var error: String?

    func load() async {
        isLoading = true
        error = nil
        defer { isLoading = false }
        do {
            async let m = API.myTables()
            async let h = API.socialHub()
            self.myTables = try await m
            self.hub = try await h
        } catch {
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }
}

struct TablesView: View {
    @State private var store = TablesStore()
    @State private var swipingIndex: Int = 0

    var body: some View {
        NavigationStack {
            ZStack {
                Theme.Palette.surfaceAlt.ignoresSafeArea()

                ScrollView {
                    VStack(alignment: .leading, spacing: Theme.Spacing.xl) {

                        header

                        invitesSection

                        myTablesSection
                    }
                    .padding(.horizontal, Theme.Spacing.lg)
                    .padding(.top, Theme.Spacing.md)
                    .padding(.bottom, 130)
                }
            }
            .navigationTitle("Tavoli")
            .navigationBarTitleDisplayMode(.large)
            .refreshable { await store.load() }
            .task { await store.load() }
        }
    }

    // MARK: - Sections

    private var header: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("Trova il tuo tavolo")
                .font(Theme.Font.display(28))
                .foregroundStyle(Theme.Palette.ink)
            Text("Inviti, tavoli aperti vicino a te, e i tuoi piani.")
                .font(Theme.Font.body())
                .foregroundStyle(Theme.Palette.inkSoft)
        }
    }

    @ViewBuilder
    private var invitesSection: some View {
        if let invites = store.hub?.tableInvites, !invites.isEmpty {
            VStack(alignment: .leading, spacing: Theme.Spacing.md) {
                Text("Inviti")
                    .font(Theme.Font.title(18))
                    .foregroundStyle(Theme.Palette.ink)
                inviteStack(invites: invites)
            }
        }
    }

    private func inviteStack(invites: [SocialTableInvite]) -> some View {
        let pending = Array(invites.dropFirst(swipingIndex))
        return ZStack {
            ForEach(Array(pending.prefix(3).enumerated()).reversed(), id: \.offset) { (offset, invite) in
                InviteCard(
                    invite: invite,
                    onAccept: { advance() },
                    onReject: { advance() }
                )
                .scaleEffect(1 - CGFloat(offset) * 0.04)
                .offset(y: CGFloat(offset) * 8)
                .zIndex(Double(3 - offset))
                .animation(.spring(response: 0.4), value: swipingIndex)
            }
            if pending.isEmpty {
                CloudyEmptyState(
                    icon: "checkmark.circle.fill",
                    title: "Tutto fatto",
                    message: "Hai gestito tutti gli inviti. Bel lavoro."
                )
                .frame(height: 280)
            }
        }
        .frame(height: 320)
    }

    private func advance() {
        Haptics.tap()
        withAnimation { swipingIndex += 1 }
    }

    @ViewBuilder
    private var myTablesSection: some View {
        VStack(alignment: .leading, spacing: Theme.Spacing.md) {
            Text("I tuoi tavoli")
                .font(Theme.Font.title(18))

            if store.isLoading && store.myTables.isEmpty {
                ProgressView().padding()
            } else if store.myTables.isEmpty {
                CloudyEmptyState(
                    icon: "person.3",
                    title: "Nessun tavolo attivo",
                    message: "Quando crei o partecipi a un tavolo lo trovi qui."
                )
            } else {
                ForEach(store.myTables) { t in
                    MyTableRow(table: t)
                }
            }
        }
    }
}

// MARK: - Invite swipe card (Bumble-style)

struct InviteCard: View {
    let invite: SocialTableInvite
    let onAccept: () -> Void
    let onReject: () -> Void

    @State private var dragOffset: CGSize = .zero

    private var rotation: Angle {
        .degrees(Double(dragOffset.width / 18))
    }
    private var swipeProgress: Double {
        Double(dragOffset.width) / 140.0
    }

    var body: some View {
        ZStack(alignment: .top) {
            // Card
            VStack(alignment: .leading, spacing: Theme.Spacing.md) {
                HStack(spacing: 12) {
                    StoryAvatar(
                        url: URL(string: invite.hostAvatarUrl ?? ""),
                        size: 56,
                        hasStory: false,
                        initials: String((invite.hostDisplayName ?? invite.hostNickname).prefix(1)).uppercased()
                    )
                    VStack(alignment: .leading, spacing: 2) {
                        Text(invite.hostDisplayName ?? invite.hostNickname)
                            .font(Theme.Font.body(15, weight: .bold))
                        Text("ti ha invitato")
                            .font(Theme.Font.caption(12))
                            .foregroundStyle(Theme.Palette.inkSoft)
                    }
                    Spacer()
                }

                Text(invite.title)
                    .font(Theme.Font.display(24))
                    .foregroundStyle(Theme.Palette.ink)
                    .lineLimit(2)

                HStack(spacing: 8) {
                    CloudyPill(text: invite.venueName, icon: "mappin.circle.fill", tone: .neutral)
                    CloudyPill(text: invite.venueCategory.capitalized, tone: .neutral)
                }

                Text(invite.startsAtUtc, format: .dateTime.weekday(.wide).day().month().hour().minute())
                    .font(Theme.Font.body(14, weight: .semibold))
                    .foregroundStyle(Theme.Palette.honeyDeep)

                Spacer()

                HStack(spacing: 12) {
                    Button(action: onReject) {
                        HStack { Image(systemName: "xmark"); Text("Rifiuta") }
                            .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.ghost)

                    Button(action: onAccept) {
                        HStack { Image(systemName: "checkmark"); Text("Accetta") }
                            .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.honey)
                }
            }
            .padding(Theme.Spacing.lg)
            .frame(maxWidth: .infinity, minHeight: 300)
            .background(
                RoundedRectangle(cornerRadius: Theme.Radius.xl, style: .continuous)
                    .fill(Theme.Palette.surface)
            )
            .cardShadow()

            // Swipe overlays
            if swipeProgress > 0.1 {
                badge(text: "ACCETTO", color: Theme.Palette.densityLow, side: .leading)
                    .opacity(min(1, swipeProgress * 1.3))
            } else if swipeProgress < -0.1 {
                badge(text: "PASSO", color: Theme.Palette.densityHigh, side: .trailing)
                    .opacity(min(1, -swipeProgress * 1.3))
            }
        }
        .offset(dragOffset)
        .rotationEffect(rotation)
        .gesture(
            DragGesture()
                .onChanged { v in dragOffset = v.translation }
                .onEnded { v in
                    if v.translation.width > 110 {
                        withAnimation(.spring()) { dragOffset = CGSize(width: 600, height: v.translation.height) }
                        Task {
                            try? await Task.sleep(nanoseconds: 200_000_000)
                            onAccept()
                            dragOffset = .zero
                        }
                    } else if v.translation.width < -110 {
                        withAnimation(.spring()) { dragOffset = CGSize(width: -600, height: v.translation.height) }
                        Task {
                            try? await Task.sleep(nanoseconds: 200_000_000)
                            onReject()
                            dragOffset = .zero
                        }
                    } else {
                        withAnimation(.spring()) { dragOffset = .zero }
                    }
                }
        )
    }

    private func badge(text: String, color: Color, side: HorizontalAlignment) -> some View {
        HStack {
            if side == .trailing { Spacer() }
            Text(text)
                .font(Theme.Font.title(20, weight: .heavy))
                .foregroundStyle(color)
                .padding(.horizontal, 14).padding(.vertical, 8)
                .background(
                    RoundedRectangle(cornerRadius: 8)
                        .stroke(color, lineWidth: 3)
                )
                .rotationEffect(.degrees(side == .leading ? -16 : 16))
                .padding(.top, 24)
                .padding(.horizontal, 24)
            if side == .leading { Spacer() }
        }
    }
}

// MARK: - My table row

struct MyTableRow: View {
    let table: SocialTableSummary
    var body: some View {
        SectionCard {
            HStack(spacing: 12) {
                ZStack {
                    Circle().fill(Theme.Gradients.honeyCTA).frame(width: 48, height: 48)
                    Image(systemName: table.isHost ? "crown.fill" : "person.3.fill")
                        .foregroundStyle(.white)
                }
                VStack(alignment: .leading, spacing: 2) {
                    Text(table.title)
                        .font(Theme.Font.body(15, weight: .bold))
                    Text(table.venueName)
                        .font(Theme.Font.caption(12))
                        .foregroundStyle(Theme.Palette.inkSoft)
                    Text(table.startsAtUtc, format: .relative(presentation: .named))
                        .font(Theme.Font.caption(11))
                        .foregroundStyle(Theme.Palette.inkMuted)
                }
                Spacer()
                CloudyPill(
                    text: "\(table.acceptedCount)/\(table.capacity)",
                    icon: "person.2.fill",
                    tone: .honey
                )
            }
        }
    }
}
