//
//  Theme.swift
//  Cloudy — Blue design system
//
//  Identita visiva: urbana, adulta, pulita. Il blu e il colore primario;
//  mint/corallo sono accenti funzionali. Gli alias legacy restano solo per
//  compatibilita durante il refactor incrementale.
//

import SwiftUI
#if canImport(UIKit)
import UIKit
#endif

enum Theme {

    enum Palette {
        static let blue50 = Color(hex: 0xEAF2FF)
        static let blue100 = Color(hex: 0xC8DCFF)
        static let blue200 = Color(hex: 0x93B9FF)
        static let blue400 = Color(hex: 0x4A7DFF)
        static let blue500 = Color(hex: 0x2B5BFF)
        static let blue600 = Color(hex: 0x1E47E0)
        static let blue700 = Color(hex: 0x1838B5)
        static let blue900 = Color(hex: 0x0B1E66)

        static let mint400 = Color(hex: 0x4FD9C4)
        static let mint500 = Color(hex: 0x16C4A8)
        static let coral500 = Color(hex: 0xFF5C7A)

        static let surface = Color(light: 0xFFFFFF, dark: 0x141925)
        static let surfaceAlt = Color(light: 0xF1F3F8, dark: 0x1B2230)
        static let surfaceRaised = Color(light: 0xFFFFFF, dark: 0x141925)
        static let appBackground = Color(light: 0xF7F8FB, dark: 0x0B0E14)
        static let hairline = Color(light: 0xE4E7EE, dark: 0x232B3C)
        static let glassStroke = Color(light: 0xC8DCFF, dark: 0x2A3550).opacity(0.72)

        static let ink = Color(light: 0x0E1422, dark: 0xF4F6FB)
        static let inkSoft = Color(light: 0x4A5169, dark: 0xB8BFD1)
        static let inkMuted = Color(light: 0x8089A0, dark: 0x6F7894)

        static let densityLow = Color(hex: 0xB8D4FF)
        static let densityMedium = Color(hex: 0x4A7DFF)
        static let densityHigh = Color(hex: 0x7C5CFF)
        static let densityPeak = coral500

        // Legacy aliases remapped to Cloudy Blue.
        static let solarStart = blue500
        static let solarEnd = blue600
        static let auroraViolet = densityHigh
        static let auroraPink = coral500
        static let auroraBlue = blue400
        static let honey = blue500
        static let honeyDeep = blue600
        static let honeySoft = blue50
        static let cloudWhite = surface
        static let cloudFog = surfaceAlt
        static let skyTint = blue100
        static let skyDeep = blue500
        static let igPink = blue500
        static let igPurple = blue600
        static let igOrange = coral500
    }

    enum Gradients {
        static let primary = LinearGradient(
            colors: [Palette.blue500, Palette.blue600],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        )

        static let blueSoft = LinearGradient(
            colors: [Palette.blue50, Palette.blue100],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        )

        static let loginBlue = LinearGradient(
            colors: [Color(hex: 0x0B1E66), Palette.blue600, Palette.blue400],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        )

        static let densityHeat = LinearGradient(
            colors: [Palette.densityLow, Palette.densityMedium, Palette.densityHigh, Palette.densityPeak],
            startPoint: .leading,
            endPoint: .trailing
        )

        static let storyRing = LinearGradient(
            colors: [Palette.blue500, Palette.blue500],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        )

        static let storyAngular = AngularGradient(colors: [Palette.blue500, Palette.blue500], center: .center)
        static let cloudBody = blueSoft
        static let glassGlow = LinearGradient(colors: [Palette.blue100.opacity(0.6), .white.opacity(0.08)], startPoint: .topLeading, endPoint: .bottomTrailing)

        // Legacy aliases.
        static let solar = primary
        static let aurora = primary
        static let honeyCTA = primary
    }

    enum Font {
        static func display(_ size: CGFloat = 34, weight: SwiftUI.Font.Weight = .heavy) -> SwiftUI.Font {
            .system(size: max(size, 12), weight: weight, design: .rounded)
        }

        static func heroNumber(_ size: CGFloat = 44) -> SwiftUI.Font {
            .system(size: max(size, 12), weight: .heavy, design: .default)
        }

        static func title(_ size: CGFloat = 22, weight: SwiftUI.Font.Weight = .semibold) -> SwiftUI.Font {
            .system(size: max(size, 12), weight: weight, design: .rounded)
        }

        static func body(_ size: CGFloat = 15, weight: SwiftUI.Font.Weight = .regular) -> SwiftUI.Font {
            .system(size: max(size, 12), weight: weight, design: .default)
        }

        static func caption(_ size: CGFloat = 12, weight: SwiftUI.Font.Weight = .medium) -> SwiftUI.Font {
            .system(size: max(size, 12), weight: weight, design: .rounded)
        }
    }

    enum Radius {
        static let sm: CGFloat = 14
        static let md: CGFloat = 20
        static let lg: CGFloat = 20
        static let xl: CGFloat = 24
        static let sheet: CGFloat = 28
        static let pill: CGFloat = 999
    }

    enum Spacing {
        static let xs: CGFloat = 4
        static let sm: CGFloat = 8
        static let md: CGFloat = 12
        static let lg: CGFloat = 16
        static let xl: CGFloat = 20
        static let xxl: CGFloat = 28
        static let hero: CGFloat = 40
    }

    enum Shadow {
        static func card(tint: Color = Palette.blue500) -> some ViewModifier {
            CloudyShadowModifier(tint: tint, opacity: 0.08, radius: 20, y: 8)
        }

        static func lifted(tint: Color = Palette.blue500) -> some ViewModifier {
            CloudyShadowModifier(tint: tint, opacity: 0.12, radius: 24, y: 10)
        }

        static func glow(tint: Color = Palette.blue500) -> some ViewModifier {
            CloudyShadowModifier(tint: tint, opacity: 0.10, radius: 18, y: 7)
        }

        static func soft(color: Color = Palette.blue500.opacity(0.08)) -> some ViewModifier {
            CloudyShadowModifier(tint: color, opacity: 1, radius: 18, y: 8)
        }
    }
}

private struct CloudyShadowModifier: ViewModifier {
    let tint: Color
    let opacity: Double
    let radius: CGFloat
    let y: CGFloat

    func body(content: Content) -> some View {
        content.shadow(color: tint.opacity(opacity), radius: radius, x: 0, y: y)
    }
}

extension View {
    func cardShadow(tint: Color = Theme.Palette.blue500) -> some View {
        modifier(Theme.Shadow.card(tint: tint))
    }

    func liftedShadow(tint: Color = Theme.Palette.blue500) -> some View {
        modifier(Theme.Shadow.lifted(tint: tint))
    }

    func cloudyGlow(tint: Color = Theme.Palette.blue500) -> some View {
        modifier(Theme.Shadow.glow(tint: tint))
    }
}

extension Color {
    init(hex: UInt32, opacity: Double = 1) {
        self.init(
            .sRGB,
            red: Double((hex >> 16) & 0xFF) / 255,
            green: Double((hex >> 8) & 0xFF) / 255,
            blue: Double(hex & 0xFF) / 255,
            opacity: opacity
        )
    }

    init(light: UInt32, dark: UInt32) {
        #if canImport(UIKit)
        self.init(UIColor { traits in
            let value = traits.userInterfaceStyle == .dark ? dark : light
            return UIColor(
                red: CGFloat((value >> 16) & 0xFF) / 255,
                green: CGFloat((value >> 8) & 0xFF) / 255,
                blue: CGFloat(value & 0xFF) / 255,
                alpha: 1
            )
        })
        #else
        self.init(hex: light)
        #endif
    }
}
