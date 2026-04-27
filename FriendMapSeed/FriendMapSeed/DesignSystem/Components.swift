//
//  Components.swift
//  Cloudy — Reusable UI components
//
//  Componenti atomici riutilizzati in tutta l'app: avatar con anello stories,
//  pill / badge, primary button "honey", chip filtri, separatori.
//

import SwiftUI

// MARK: - Honey Primary Button (Bumble-like)

struct HoneyButtonStyle: ButtonStyle {
    var isCompact: Bool = false
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(Theme.Font.body(isCompact ? 14 : 16, weight: .bold))
            .foregroundStyle(Theme.Palette.ink)
            .padding(.vertical, isCompact ? 10 : 14)
            .padding(.horizontal, isCompact ? 18 : 22)
            .background(
                Capsule().fill(Theme.Gradients.honeyCTA)
            )
            .overlay(
                Capsule().stroke(.white.opacity(0.6), lineWidth: 1)
            )
            .scaleEffect(configuration.isPressed ? 0.97 : 1)
            .animation(.spring(response: 0.25, dampingFraction: 0.7), value: configuration.isPressed)
            .liftedShadow()
    }
}

extension ButtonStyle where Self == HoneyButtonStyle {
    static var honey: HoneyButtonStyle { HoneyButtonStyle() }
    static var honeyCompact: HoneyButtonStyle { HoneyButtonStyle(isCompact: true) }
}

// MARK: - Ghost / secondary

struct GhostButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(Theme.Font.body(15, weight: .semibold))
            .foregroundStyle(Theme.Palette.ink)
            .padding(.vertical, 12)
            .padding(.horizontal, 18)
            .background(
                Capsule().fill(Theme.Palette.surfaceAlt)
            )
            .overlay(
                Capsule().stroke(Theme.Palette.hairline, lineWidth: 1)
            )
            .scaleEffect(configuration.isPressed ? 0.97 : 1)
            .animation(.spring(response: 0.25), value: configuration.isPressed)
    }
}

extension ButtonStyle where Self == GhostButtonStyle {
    static var ghost: GhostButtonStyle { GhostButtonStyle() }
}

// MARK: - Avatar with optional Stories ring

struct StoryAvatar: View {
    let url: URL?
    let size: CGFloat
    let hasStory: Bool
    let initials: String

    init(url: URL?, size: CGFloat = 56, hasStory: Bool = false, initials: String = "?") {
        self.url = url
        self.size = size
        self.hasStory = hasStory
        self.initials = initials
    }

    var body: some View {
        ZStack {
            if hasStory {
                Circle()
                    .stroke(Theme.Gradients.storyRing, lineWidth: 2.5)
                    .frame(width: size + 8, height: size + 8)
            }
            Circle()
                .fill(Theme.Palette.surface)
                .frame(width: size + 4, height: size + 4)

            avatarContent
                .frame(width: size, height: size)
                .clipShape(Circle())
        }
        .accessibilityLabel(Text(hasStory ? "Avatar con storia" : "Avatar"))
    }

    @ViewBuilder
    private var avatarContent: some View {
        if let url {
            AsyncImage(url: url) { phase in
                switch phase {
                case .success(let image):
                    image.resizable().scaledToFill()
                case .failure:
                    initialsFallback
                case .empty:
                    Circle().fill(Theme.Palette.surfaceAlt)
                @unknown default:
                    initialsFallback
                }
            }
        } else {
            initialsFallback
        }
    }

    private var initialsFallback: some View {
        ZStack {
            Circle().fill(Theme.Gradients.honeyCTA)
            Text(initials)
                .font(Theme.Font.title(size * 0.38, weight: .heavy))
                .foregroundStyle(Theme.Palette.ink)
        }
    }
}

// MARK: - Pill / Tag

struct CloudyPill: View {
    let text: String
    var icon: String? = nil
    var tone: Tone = .neutral

    enum Tone { case neutral, honey, success, warning, danger }

    var body: some View {
        HStack(spacing: 4) {
            if let icon { Image(systemName: icon).font(.system(size: 11, weight: .bold)) }
            Text(text)
                .font(Theme.Font.caption(11, weight: .bold))
        }
        .foregroundStyle(foreground)
        .padding(.horizontal, 10)
        .padding(.vertical, 5)
        .background(
            Capsule().fill(background)
        )
    }

    private var foreground: Color {
        switch tone {
        case .neutral: return Theme.Palette.inkSoft
        case .honey:   return Theme.Palette.ink
        case .success: return .white
        case .warning: return .white
        case .danger:  return .white
        }
    }

    private var background: Color {
        switch tone {
        case .neutral: return Theme.Palette.surfaceAlt
        case .honey:   return Theme.Palette.honey
        case .success: return Theme.Palette.densityLow
        case .warning: return Theme.Palette.densityMedium
        case .danger:  return Theme.Palette.densityHigh
        }
    }
}

// MARK: - Filter chip

struct FilterChip: View {
    let label: String
    let isSelected: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Text(label)
                .font(Theme.Font.body(14, weight: .semibold))
                .padding(.vertical, 8)
                .padding(.horizontal, 14)
                .background(
                    Capsule().fill(isSelected ? Theme.Palette.ink : Theme.Palette.surface)
                )
                .foregroundStyle(isSelected ? Color.white : Theme.Palette.ink)
                .overlay(
                    Capsule().stroke(isSelected ? Color.clear : Theme.Palette.hairline, lineWidth: 1)
                )
        }
        .buttonStyle(.plain)
    }
}

// MARK: - Section card

struct SectionCard<Content: View>: View {
    let content: Content
    init(@ViewBuilder content: () -> Content) { self.content = content() }
    var body: some View {
        VStack(alignment: .leading, spacing: Theme.Spacing.md) {
            content
        }
        .padding(Theme.Spacing.lg)
        .background(
            RoundedRectangle(cornerRadius: Theme.Radius.lg, style: .continuous)
                .fill(Theme.Palette.surface)
        )
        .cardShadow()
    }
}

// MARK: - Empty state

struct CloudyEmptyState: View {
    let icon: String
    let title: String
    let message: String
    var body: some View {
        VStack(spacing: Theme.Spacing.md) {
            Image(systemName: icon)
                .font(.system(size: 40, weight: .light))
                .foregroundStyle(Theme.Palette.inkMuted)
            Text(title)
                .font(Theme.Font.title(18))
                .foregroundStyle(Theme.Palette.ink)
            Text(message)
                .font(Theme.Font.body())
                .foregroundStyle(Theme.Palette.inkSoft)
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, Theme.Spacing.xxl)
    }
}

// MARK: - Loading dots

struct LoadingDots: View {
    @State private var phase: CGFloat = 0
    var body: some View {
        TimelineView(.animation) { ctx in
            let t = ctx.date.timeIntervalSinceReferenceDate
            HStack(spacing: 6) {
                ForEach(0..<3, id: \.self) { i in
                    let p = sin(t * 3 + Double(i) * 0.8)
                    Circle()
                        .fill(Theme.Palette.honey)
                        .frame(width: 8, height: 8)
                        .opacity(0.4 + Double(p) * 0.3 + 0.3)
                        .scaleEffect(0.85 + p * 0.15)
                }
            }
        }
    }
}

// MARK: - Density indicator

struct DensityIndicator: View {
    let level: String     // "low" | "medium" | "high" | "?"
    let count: Int

    private var color: Color {
        switch level.lowercased() {
        case "low":    return Theme.Palette.densityLow
        case "medium": return Theme.Palette.densityMedium
        case "high":   return Theme.Palette.densityHigh
        default:       return Theme.Palette.inkMuted
        }
    }
    private var label: String {
        switch level.lowercased() {
        case "low":    return "Tranquillo"
        case "medium": return "Vivace"
        case "high":   return "Pieno"
        default:       return "—"
        }
    }

    var body: some View {
        HStack(spacing: 6) {
            Circle().fill(color).frame(width: 8, height: 8)
            Text(label)
                .font(Theme.Font.caption(12, weight: .bold))
                .foregroundStyle(Theme.Palette.inkSoft)
            if count > 0 {
                Text("· \(count)")
                    .font(Theme.Font.caption(12))
                    .foregroundStyle(Theme.Palette.inkMuted)
            }
        }
    }
}
