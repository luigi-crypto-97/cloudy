//
//  VenueDetailSheet.swift
//  Cloudy — Bottom sheet di dettaglio venue (Bumble-like card)
//

import SwiftUI

struct VenueDetailSheet: View {
    let venue: VenueMarker
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Theme.Spacing.lg) {

                // Hero
                ZStack(alignment: .topTrailing) {
                    AsyncImage(url: URL(string: venue.coverImageUrl ?? "")) { phase in
                        switch phase {
                        case .success(let img):
                            img.resizable().scaledToFill()
                        default:
                            ZStack {
                                Theme.Gradients.honeyCTA
                                Image(systemName: iconForCategory(venue.category))
                                    .font(.system(size: 56, weight: .semibold))
                                    .foregroundStyle(.white.opacity(0.9))
                            }
                        }
                    }
                    .frame(height: 180)
                    .frame(maxWidth: .infinity)
                    .clipped()
                    .clipShape(RoundedRectangle(cornerRadius: Theme.Radius.lg, style: .continuous))

                    // Open badge
                    HStack(spacing: 6) {
                        Circle()
                            .fill(venue.isOpenNow ? Theme.Palette.densityLow : Theme.Palette.densityHigh)
                            .frame(width: 8, height: 8)
                        Text(venue.isOpenNow ? "Aperto ora" : "Chiuso")
                            .font(Theme.Font.caption(11, weight: .bold))
                    }
                    .padding(.horizontal, 10)
                    .padding(.vertical, 6)
                    .background(.thinMaterial, in: Capsule())
                    .padding(10)
                }

                // Title
                VStack(alignment: .leading, spacing: 4) {
                    Text(venue.name)
                        .font(Theme.Font.display(26, weight: .heavy))
                        .foregroundStyle(Theme.Palette.ink)
                    Text("\(venue.category.capitalized) · \(venue.city)")
                        .font(Theme.Font.body())
                        .foregroundStyle(Theme.Palette.inkSoft)
                }

                // Density / stats
                HStack(spacing: Theme.Spacing.md) {
                    statTile(icon: "person.2.fill", value: "\(venue.peopleEstimate)", label: "Persone")
                    statTile(icon: "checkmark.circle.fill", value: "\(venue.activeCheckIns)", label: "Check-in")
                    statTile(icon: "hand.raised.fill", value: "\(venue.activeIntentions)", label: "Piani")
                    statTile(icon: "person.3.fill", value: "\(venue.openTables)", label: "Tavoli")
                }

                // Density indicator + tags
                VStack(alignment: .leading, spacing: 8) {
                    DensityIndicator(level: venue.densityLevel, count: venue.peopleEstimate)
                    if !venue.tags.isEmpty {
                        ScrollView(.horizontal, showsIndicators: false) {
                            HStack(spacing: 6) {
                                ForEach(venue.tags, id: \.self) { tag in
                                    CloudyPill(text: tag, tone: .neutral)
                                }
                            }
                        }
                    }
                }

                // Description
                if let desc = venue.description, !desc.isEmpty {
                    Text(desc)
                        .font(Theme.Font.body(15))
                        .foregroundStyle(Theme.Palette.ink)
                        .lineSpacing(2)
                }

                // Presence preview (Instagram-like avatars row)
                if !venue.presencePreview.isEmpty {
                    SectionCard {
                        Text("Amici qui ora")
                            .font(Theme.Font.title(16))
                        HStack(spacing: -10) {
                            ForEach(venue.presencePreview.prefix(6)) { p in
                                StoryAvatar(
                                    url: URL(string: p.avatarUrl ?? ""),
                                    size: 44,
                                    hasStory: true,
                                    initials: String(p.displayName.prefix(1))
                                )
                            }
                            if venue.presencePreview.count > 6 {
                                Text("+\(venue.presencePreview.count - 6)")
                                    .font(Theme.Font.caption(13, weight: .bold))
                                    .padding(10)
                                    .background(Circle().fill(Theme.Palette.surfaceAlt))
                            }
                        }
                    }
                }

                // CTAs
                VStack(spacing: 10) {
                    Button {
                        // TODO: check-in flow
                    } label: {
                        HStack {
                            Image(systemName: "hand.thumbsup.fill")
                            Text("Sono qui ora")
                        }
                        .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.honey)

                    Button {
                        // TODO: intention flow
                    } label: {
                        HStack {
                            Image(systemName: "calendar.badge.plus")
                            Text("Pianifica un'uscita")
                        }
                        .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.ghost)
                }
                .padding(.top, 4)

                // Address / contacts
                if !venue.addressLine.isEmpty {
                    rowItem(icon: "mappin.circle.fill", text: "\(venue.addressLine), \(venue.city)")
                }
                if let phone = venue.phoneNumber, !phone.isEmpty {
                    rowItem(icon: "phone.fill", text: phone)
                }
                if let hrs = venue.hoursSummary, !hrs.isEmpty {
                    rowItem(icon: "clock.fill", text: hrs)
                }
            }
            .padding(Theme.Spacing.lg)
        }
    }

    private func statTile(icon: String, value: String, label: String) -> some View {
        VStack(spacing: 2) {
            Image(systemName: icon)
                .font(.system(size: 16, weight: .bold))
                .foregroundStyle(Theme.Palette.honeyDeep)
            Text(value)
                .font(Theme.Font.title(20, weight: .heavy))
                .foregroundStyle(Theme.Palette.ink)
            Text(label)
                .font(Theme.Font.caption(11))
                .foregroundStyle(Theme.Palette.inkMuted)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 12)
        .background(
            RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                .fill(Theme.Palette.surfaceAlt)
        )
    }

    private func rowItem(icon: String, text: String) -> some View {
        HStack(spacing: 10) {
            Image(systemName: icon)
                .foregroundStyle(Theme.Palette.honeyDeep)
            Text(text)
                .font(Theme.Font.body(14))
                .foregroundStyle(Theme.Palette.ink)
            Spacer()
        }
    }

    private func iconForCategory(_ c: String) -> String {
        switch c.lowercased() {
        case "bar", "pub":            return "wineglass.fill"
        case "restaurant", "ristorante": return "fork.knife"
        case "cafe", "caffè":         return "cup.and.saucer.fill"
        case "club", "discoteca":     return "music.note"
        default:                      return "mappin.and.ellipse"
        }
    }
}

// MARK: - Filter sheet

struct MapFiltersSheet: View {
    @Environment(MapStore.self) private var store
    @Environment(\.dismiss) private var dismiss
    @State private var query: String = ""
    @State private var category: String = "all"
    @State private var openNow: Bool = false

    private let categories: [(String, String)] = [
        ("all", "Tutti"),
        ("bar", "Bar"),
        ("restaurant", "Ristoranti"),
        ("cafe", "Caffè"),
        ("club", "Locali notturni")
    ]

    var body: some View {
        NavigationStack {
            VStack(alignment: .leading, spacing: Theme.Spacing.lg) {
                Text("Filtri")
                    .font(Theme.Font.display(28))

                TextField("Cerca un posto…", text: $query)
                    .textFieldStyle(.plain)
                    .padding(12)
                    .background(Theme.Palette.surfaceAlt, in: RoundedRectangle(cornerRadius: 12))

                Text("Categoria")
                    .font(Theme.Font.title(16))
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: 8) {
                        ForEach(categories, id: \.0) { (key, label) in
                            FilterChip(
                                label: label,
                                isSelected: category == key,
                                action: { category = key }
                            )
                        }
                    }
                }

                Toggle(isOn: $openNow) {
                    Text("Aperto ora")
                        .font(Theme.Font.body(15, weight: .semibold))
                }
                .tint(Theme.Palette.honey)

                Spacer()

                Button {
                    store.applyFilters(query: query, category: category, openNow: openNow)
                    dismiss()
                } label: {
                    Text("Applica filtri")
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(.honey)
            }
            .padding(Theme.Spacing.lg)
            .onAppear {
                query = store.query
                category = store.category
                openNow = store.openNowOnly
            }
        }
    }
}
