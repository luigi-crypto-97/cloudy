//
//  Components.swift
//  Cloudy — Componenti base del sistema Cloudy Blue.
//

import SwiftUI

// MARK: - Buttons

struct CloudyButtonStyle: PrimitiveButtonStyle {
    enum Variant {
        case primary
        case secondary
        case ghost
        case destructive
        case solar
        case aurora
        case glass
    }

    var variant: Variant = .primary
    var isCompact: Bool = false

    func makeBody(configuration: Configuration) -> some View {
        CloudyButtonBody(configuration: configuration, variant: normalizedVariant, isCompact: isCompact)
    }

    private var normalizedVariant: Variant {
        switch variant {
        case .solar, .aurora: return .primary
        case .glass: return .ghost
        default: return variant
        }
    }
}

private struct CloudyButtonBody: View {
    @Environment(\.isEnabled) private var isEnabled
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @GestureState private var isPressed = false

    let configuration: PrimitiveButtonStyle.Configuration
    let variant: CloudyButtonStyle.Variant
    let isCompact: Bool

    var body: some View {
        configuration.label
            .font(Theme.Font.body(isCompact ? 14 : 15, weight: .semibold))
            .foregroundStyle(foreground)
            .padding(.vertical, isCompact ? 10 : 14)
            .padding(.horizontal, isCompact ? 16 : 20)
            .background(background)
            .overlay(stroke)
            .brightness(isPressed && isEnabled ? -0.05 : 0)
            .scaleEffect(isPressed && isEnabled && !reduceMotion ? 0.97 : 1)
            .opacity(isEnabled ? 1 : 0.45)
            .animation(CloudyMotion.snap(reduceMotion: reduceMotion), value: isPressed)
            .animation(CloudyMotion.snap(reduceMotion: reduceMotion), value: isEnabled)
            .contentShape(RoundedRectangle(cornerRadius: Theme.Radius.sm, style: .continuous))
            .gesture(
                DragGesture(minimumDistance: 0)
                    .updating($isPressed) { _, state, _ in state = true }
                    .onEnded { _ in
                        guard isEnabled else { return }
                        Haptics.tap()
                        configuration.trigger()
                    }
            )
    }

    private var foreground: Color {
        switch variant {
        case .primary, .destructive:
            return .white
        case .secondary, .ghost, .solar, .aurora, .glass:
            return Theme.Palette.blue700
        }
    }

    @ViewBuilder
    private var background: some View {
        RoundedRectangle(cornerRadius: Theme.Radius.sm, style: .continuous)
            .fill(backgroundStyle)
            .if(variant == .primary) { view in view.cardShadow(tint: Theme.Palette.blue500) }
    }

    private var backgroundStyle: AnyShapeStyle {
        switch variant {
        case .primary, .solar, .aurora:
            return AnyShapeStyle(Theme.Palette.blue500)
        case .secondary:
            return AnyShapeStyle(Theme.Palette.blue50)
        case .ghost, .glass:
            return AnyShapeStyle(Color.clear)
        case .destructive:
            return AnyShapeStyle(Theme.Palette.coral500)
        }
    }

    @ViewBuilder
    private var stroke: some View {
        RoundedRectangle(cornerRadius: Theme.Radius.sm, style: .continuous)
            .stroke(strokeColor, lineWidth: variant == .primary ? 0 : 1)
    }

    private var strokeColor: Color {
        switch variant {
        case .ghost, .glass:
            return Theme.Palette.blue100
        case .secondary:
            return Theme.Palette.blue100.opacity(0.7)
        case .destructive:
            return Theme.Palette.coral500
        default:
            return .clear
        }
    }
}

struct HoneyButtonStyle: PrimitiveButtonStyle {
    var isCompact: Bool = false
    func makeBody(configuration: Configuration) -> some View {
        CloudyButtonStyle(variant: .primary, isCompact: isCompact).makeBody(configuration: configuration)
    }
}

struct GhostButtonStyle: PrimitiveButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        CloudyButtonStyle(variant: .ghost).makeBody(configuration: configuration)
    }
}

extension PrimitiveButtonStyle where Self == CloudyButtonStyle {
    static var cloudyPrimary: CloudyButtonStyle { CloudyButtonStyle(variant: .primary) }
    static var cloudySecondary: CloudyButtonStyle { CloudyButtonStyle(variant: .secondary) }
    static var cloudyGhost: CloudyButtonStyle { CloudyButtonStyle(variant: .ghost) }
    static var cloudyDestructive: CloudyButtonStyle { CloudyButtonStyle(variant: .destructive) }
    static var cloudySolar: CloudyButtonStyle { CloudyButtonStyle(variant: .primary) }
    static var cloudyAurora: CloudyButtonStyle { CloudyButtonStyle(variant: .primary) }
    static var cloudyGlass: CloudyButtonStyle { CloudyButtonStyle(variant: .ghost) }
}

extension PrimitiveButtonStyle where Self == HoneyButtonStyle {
    static var honey: HoneyButtonStyle { HoneyButtonStyle() }
    static var honeyCompact: HoneyButtonStyle { HoneyButtonStyle(isCompact: true) }
}

extension PrimitiveButtonStyle where Self == GhostButtonStyle {
    static var ghost: GhostButtonStyle { GhostButtonStyle() }
}

// MARK: - Cards

struct CloudyCard<Content: View>: View {
    let padding: CGFloat
    let content: Content

    init(padding: CGFloat = 20, @ViewBuilder content: () -> Content) {
        self.padding = padding
        self.content = content()
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Theme.Spacing.md) {
            content
        }
        .padding(padding)
        .background(Theme.Palette.surface, in: RoundedRectangle(cornerRadius: Theme.Radius.lg, style: .continuous))
        .cardShadow(tint: Theme.Palette.blue500)
    }
}

struct SectionCard<Content: View>: View {
    let content: Content
    init(@ViewBuilder content: () -> Content) { self.content = content() }
    var body: some View { CloudyCard { content } }
}

struct HeroCard<Content: View>: View {
    let imageURL: URL?
    let title: String
    let subtitle: String?
    @ViewBuilder var content: Content

    var body: some View {
        ZStack(alignment: .bottomLeading) {
            CachedImage(url: imageURL, options: .venueCover)
            .overlay(
                LinearGradient(
                    colors: [.black.opacity(0.02), .black.opacity(0.66)],
                    startPoint: .top,
                    endPoint: .bottom
                )
            )

            VStack(alignment: .leading, spacing: 8) {
                if let subtitle {
                    Text(subtitle)
                        .font(Theme.Font.caption(12, weight: .medium))
                        .foregroundStyle(.white.opacity(0.82))
                }
                Text(title)
                    .font(Theme.Font.display(32))
                    .tracking(-0.5)
                    .foregroundStyle(.white)
                    .lineLimit(2)
                content
            }
            .padding(Theme.Spacing.xl)
        }
        .frame(minHeight: 236)
        .clipShape(RoundedRectangle(cornerRadius: Theme.Radius.lg, style: .continuous))
        .cardShadow(tint: Theme.Palette.blue500)
    }
}

// MARK: - Inputs

struct CloudyTextField: View {
    let title: String
    let placeholder: String
    @Binding var text: String
    var isSecure = false
    @FocusState private var isFocused: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(title)
                .font(Theme.Font.caption(12, weight: .medium))
                .foregroundStyle(isFocused ? Theme.Palette.blue600 : Theme.Palette.inkMuted)
            Group {
                if isSecure {
                    SecureField(placeholder, text: $text)
                } else {
                    TextField(placeholder, text: $text)
                }
            }
            .focused($isFocused)
            .font(Theme.Font.body(15))
            .textInputAutocapitalization(.never)
            .autocorrectionDisabled(true)
            .padding(.horizontal, 14)
            .padding(.vertical, 13)
            .background(Theme.Palette.surfaceAlt, in: RoundedRectangle(cornerRadius: Theme.Radius.sm, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: Theme.Radius.sm, style: .continuous)
                    .stroke(isFocused ? Theme.Palette.blue500 : Color.clear, lineWidth: 1.5)
            )
            .animation(.cloudySnap, value: isFocused)
        }
    }
}

// MARK: - Avatar / Stories

struct AvatarRing<Content: View>: View {
    let size: CGFloat
    let hasStory: Bool
    let watched: Bool
    @ViewBuilder let content: Content

    var body: some View {
        ZStack {
            if hasStory {
                Circle()
                    .stroke(watched ? Theme.Palette.hairline : Theme.Palette.blue500, lineWidth: 3)
                    .frame(width: size + 8, height: size + 8)
            }
            Circle()
                .fill(Theme.Palette.surface)
                .frame(width: size + 4, height: size + 4)
            content
                .frame(width: size, height: size)
                .clipShape(Circle())
        }
    }
}

struct StoryRingV2<Content: View>: View {
    let size: CGFloat
    let hasStory: Bool
    let watched: Bool
    @ViewBuilder let content: Content

    var body: some View {
        AvatarRing(size: size, hasStory: hasStory, watched: watched) {
            content
        }
    }
}

struct StoryAvatar: View {
    let url: URL?
    let size: CGFloat
    let hasStory: Bool
    let initials: String
    var watched: Bool = false

    init(url: URL?, size: CGFloat = 56, hasStory: Bool = false, initials: String = "?", watched: Bool = false) {
        self.url = url
        self.size = size
        self.hasStory = hasStory
        self.initials = initials
        self.watched = watched
    }

    var body: some View {
        AvatarRing(size: size, hasStory: hasStory, watched: watched) {
            avatarContent
        }
        .accessibilityLabel(Text(hasStory ? "Avatar con storia" : "Avatar"))
    }

    @ViewBuilder
    private var avatarContent: some View {
        if let url {
            CachedImage(url: url, options: .avatar)
        } else {
            initialsFallback
        }
    }

    private var initialsFallback: some View {
        ZStack {
            Circle().fill(Theme.Palette.blue50)
            Text(initials)
                .font(Theme.Font.title(size * 0.36, weight: .semibold))
                .foregroundStyle(Theme.Palette.blue700)
        }
    }
}

// MARK: - Sheet / Tabs

struct SheetHeader: View {
    let title: String
    var close: (() -> Void)?

    var body: some View {
        VStack(spacing: Theme.Spacing.md) {
            Capsule()
                .fill(Theme.Palette.hairline)
                .frame(width: 36, height: 4)
            HStack {
                Text(title)
                    .font(Theme.Font.display(22))
                    .tracking(-0.5)
                    .foregroundStyle(Theme.Palette.ink)
                Spacer()
                if let close {
                    Button(action: close) {
                        Image(systemName: "xmark")
                            .font(.system(size: 14, weight: .semibold))
                            .foregroundStyle(Theme.Palette.inkSoft)
                            .frame(width: 36, height: 36)
                            .background(Theme.Palette.surfaceAlt, in: Circle())
                    }
                    .buttonStyle(.plain)
                    .pressableScale()
                }
            }
        }
    }
}

struct CloudySegmentItem<Tab: Hashable>: Identifiable {
    let id: Tab
    let title: String
}

struct SegmentedTabs<Tab: Hashable>: View {
    @Binding var selection: Tab
    let items: [CloudySegmentItem<Tab>]
    @Namespace private var namespace

    var body: some View {
        HStack(spacing: 4) {
            ForEach(items) { item in
                Button {
                    Haptics.tap()
                    withAnimation(.cloudySnap) { selection = item.id }
                } label: {
                    Text(item.title)
                        .font(Theme.Font.caption(13, weight: .medium))
                        .foregroundStyle(selection == item.id ? .white : Theme.Palette.inkSoft)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 10)
                        .background {
                            if selection == item.id {
                                Capsule()
                                    .fill(Theme.Palette.blue500)
                                    .matchedGeometryEffect(id: "segmented", in: namespace)
                            }
                        }
                }
                .buttonStyle(.plain)
            }
        }
        .padding(4)
        .background(Theme.Palette.surfaceAlt, in: Capsule())
    }
}

struct CloudyTabItem<Tab: Hashable>: Identifiable {
    let id: Tab
    let title: String
    let icon: String
    var badge: Int = 0
}

struct AnimatedTabBar<Tab: Hashable>: View {
    @Binding var selection: Tab
    let items: [CloudyTabItem<Tab>]
    @Namespace private var namespace

    var body: some View {
        HStack(spacing: 4) {
            ForEach(items) { item in
                Button {
                    Haptics.tap()
                    withAnimation(.cloudySnap) { selection = item.id }
                } label: {
                    HStack(spacing: 6) {
                        ZStack(alignment: .topTrailing) {
                            Image(systemName: item.icon)
                                .font(.system(size: 16, weight: .semibold))
                                .symbolEffect(.bounce, value: selection == item.id)
                            if item.badge > 0 {
                                Text(item.badge > 99 ? "99+" : "\(item.badge)")
                                    .font(.system(size: 9, weight: .bold, design: .rounded))
                                    .foregroundStyle(.white)
                                    .padding(.horizontal, 5)
                                    .frame(minWidth: 17, minHeight: 17)
                                    .background(Capsule().fill(Theme.Palette.coral500))
                                    .offset(x: 13, y: -11)
                                    .transition(.scale.combined(with: .opacity))
                            }
                        }
                        if selection == item.id {
                            Text(item.title)
                                .font(Theme.Font.caption(12, weight: .medium))
                                .transition(.opacity.combined(with: .scale(scale: 0.96)))
                        }
                    }
                    .foregroundStyle(selection == item.id ? .white : Theme.Palette.inkMuted)
                    .frame(maxWidth: .infinity)
                    .frame(height: 46)
                    .background {
                        if selection == item.id {
                            Capsule()
                                .fill(Theme.Palette.blue500)
                                .matchedGeometryEffect(id: "tab-indicator", in: namespace)
                        }
                    }
                }
                .buttonStyle(.plain)
                .pressableScale()
            }
        }
        .padding(6)
        .background(.ultraThinMaterial, in: Capsule())
        .overlay(Capsule().stroke(Theme.Palette.hairline, lineWidth: 1))
        .shadow(color: Theme.Palette.blue500.opacity(0.10), radius: 20, x: 0, y: 8)
        .padding(.horizontal, Theme.Spacing.lg)
        .padding(.bottom, 10)
    }
}

// MARK: - Pills / Empty / Loading

struct CloudyPill: View {
    let text: String
    var icon: String? = nil
    var tone: Tone = .neutral

    enum Tone { case neutral, honey, success, warning, danger }

    var body: some View {
        HStack(spacing: 5) {
            if let icon { Image(systemName: icon).font(.system(size: 12, weight: .semibold)) }
            Text(text)
                .font(Theme.Font.caption(12, weight: .medium))
                .lineLimit(1)
        }
        .foregroundStyle(foreground)
        .padding(.horizontal, 12)
        .padding(.vertical, 7)
        .background(Capsule().fill(background))
        .overlay(Capsule().stroke(stroke, lineWidth: 1))
    }

    private var foreground: Color {
        switch tone {
        case .neutral: return Theme.Palette.inkSoft
        case .honey: return Theme.Palette.blue700
        case .success: return Theme.Palette.mint500
        case .warning, .danger: return Theme.Palette.coral500
        }
    }

    private var background: AnyShapeStyle {
        switch tone {
        case .neutral:
            return AnyShapeStyle(Theme.Palette.surfaceAlt)
        case .honey:
            return AnyShapeStyle(Theme.Palette.blue50)
        case .success:
            return AnyShapeStyle(Theme.Palette.mint400.opacity(0.15))
        case .warning, .danger:
            return AnyShapeStyle(Theme.Palette.coral500.opacity(0.12))
        }
    }

    private var stroke: Color {
        switch tone {
        case .honey: return Theme.Palette.blue100
        default: return .clear
        }
    }
}

struct FilterChip: View {
    let label: String
    let isSelected: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Text(label)
                .font(Theme.Font.caption(13, weight: .medium))
                .padding(.vertical, 10)
                .padding(.horizontal, 16)
                .background(Capsule().fill(isSelected ? AnyShapeStyle(Theme.Palette.blue500) : AnyShapeStyle(Theme.Palette.surfaceAlt)))
                .foregroundStyle(isSelected ? .white : Theme.Palette.inkSoft)
                .overlay(Capsule().stroke(isSelected ? Theme.Palette.blue500 : Theme.Palette.hairline, lineWidth: 1))
        }
        .buttonStyle(.plain)
        .pressableScale()
    }
}

struct CloudyEmptyState: View {
    let icon: String
    let title: String
    let message: String

    var body: some View {
        VStack(spacing: Theme.Spacing.md) {
            Image(systemName: icon)
                .symbolRenderingMode(.hierarchical)
                .font(.system(size: 48, weight: .semibold))
                .foregroundStyle(Theme.Palette.blue500)
                .frame(width: 88, height: 88)
                .background(Theme.Palette.blue50, in: Circle())

            Text(title)
                .font(Theme.Font.display(24))
                .tracking(-0.5)
                .foregroundStyle(Theme.Palette.ink)
                .multilineTextAlignment(.center)
            Text(message)
                .font(Theme.Font.body(15))
                .lineSpacing(5)
                .foregroundStyle(Theme.Palette.inkSoft)
                .multilineTextAlignment(.center)
                .lineLimit(3)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, Theme.Spacing.xxl)
    }
}

struct LoadingShimmer: View {
    var cornerRadius: CGFloat = Theme.Radius.md
    var body: some View { SkeletonShimmer(cornerRadius: cornerRadius) }
}

struct LoadingDots: View {
    var body: some View {
        HStack(spacing: 5) {
            ForEach(0..<3, id: \.self) { _ in
                Circle()
                    .fill(Theme.Palette.blue500)
                    .frame(width: 7, height: 7)
            }
        }
        .accessibilityLabel(Text("Caricamento"))
    }
}

struct DensityIndicator: View {
    let level: String
    let count: Int

    private var color: Color {
        switch level.lowercased() {
        case "low", "very_low": return Theme.Palette.densityLow
        case "medium": return Theme.Palette.densityMedium
        case "high": return Theme.Palette.densityHigh
        case "very_high": return Theme.Palette.densityPeak
        default: return Theme.Palette.inkMuted
        }
    }

    private var label: String {
        switch level.lowercased() {
        case "low", "very_low": return "Tranquillo"
        case "medium": return "Vivace"
        case "high": return "Pieno"
        case "very_high": return "Hotspot"
        default: return "In ascolto"
        }
    }

    var body: some View {
        HStack(spacing: 8) {
            Circle().fill(color).frame(width: 10, height: 10)
            Text(label)
                .font(Theme.Font.caption(12, weight: .medium))
                .foregroundStyle(Theme.Palette.inkSoft)
            if count > 0 {
                Text("\(count)")
                    .font(Theme.Font.heroNumber(18).monospacedDigit())
                    .contentTransition(.numericText())
                    .foregroundStyle(Theme.Palette.ink)
            }
        }
        .animation(.cloudySnap, value: count)
    }
}

private extension View {
    @ViewBuilder
    func `if`<Content: View>(_ condition: Bool, transform: (Self) -> Content) -> some View {
        if condition {
            transform(self)
        } else {
            self
        }
    }
}
