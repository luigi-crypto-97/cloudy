//
//  Theme.swift
//  Cloudy — Design System
//
//  Palette ispirata a Bumble (giallo/miele) + accenti social Instagram-like.
//  Usata in tutta l'app per garantire coerenza visiva.
//

import SwiftUI

enum Theme {

    // MARK: - Colors

    enum Palette {
        /// Honey / Bumble yellow — colore signature.
        static let honey       = Color(red: 1.00, green: 0.78, blue: 0.20)
        static let honeyDeep   = Color(red: 0.98, green: 0.65, blue: 0.10)
        static let honeySoft   = Color(red: 1.00, green: 0.92, blue: 0.65)

        /// Cloud whites & sky blues — identità "nuvola".
        static let cloudWhite  = Color(red: 0.98, green: 0.99, blue: 1.00)
        static let cloudFog    = Color(red: 0.92, green: 0.94, blue: 0.97)
        static let skyTint     = Color(red: 0.78, green: 0.88, blue: 1.00)
        static let skyDeep     = Color(red: 0.36, green: 0.55, blue: 0.85)

        /// Instagram-like gradient stops per accenti social (stories, like).
        static let igPink      = Color(red: 0.93, green: 0.27, blue: 0.49)
        static let igPurple    = Color(red: 0.51, green: 0.21, blue: 0.85)
        static let igOrange    = Color(red: 0.99, green: 0.55, blue: 0.16)

        /// Neutrali.
        static let ink         = Color(red: 0.10, green: 0.11, blue: 0.13)
        static let inkSoft     = Color(red: 0.32, green: 0.34, blue: 0.38)
        static let inkMuted    = Color(red: 0.55, green: 0.58, blue: 0.62)
        static let surface     = Color(red: 1.00, green: 1.00, blue: 1.00)
        static let surfaceAlt  = Color(red: 0.96, green: 0.96, blue: 0.97)
        static let hairline    = Color(red: 0.90, green: 0.90, blue: 0.92)

        /// Density / status semantica.
        static let densityLow    = Color(red: 0.55, green: 0.80, blue: 0.55)
        static let densityMedium = Color(red: 0.99, green: 0.70, blue: 0.30)
        static let densityHigh   = Color(red: 0.95, green: 0.34, blue: 0.34)
    }

    // MARK: - Gradients

    enum Gradients {
        /// Stories ring gradient — Instagram-like.
        static let storyRing = LinearGradient(
            colors: [Palette.igOrange, Palette.igPink, Palette.igPurple],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        )

        /// Honey CTA — Bumble-like.
        static let honeyCTA = LinearGradient(
            colors: [Palette.honey, Palette.honeyDeep],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        )

        /// Cloud body — soft radial.
        static let cloudBody = RadialGradient(
            colors: [Color.white.opacity(0.98), Palette.cloudFog.opacity(0.85)],
            center: .center,
            startRadius: 6,
            endRadius: 80
        )
    }

    // MARK: - Typography (SF Pro Rounded)

    enum Font {
        static func display(_ size: CGFloat = 28, weight: SwiftUI.Font.Weight = .heavy) -> SwiftUI.Font {
            .system(size: size, weight: weight, design: .rounded)
        }
        static func title(_ size: CGFloat = 22, weight: SwiftUI.Font.Weight = .bold) -> SwiftUI.Font {
            .system(size: size, weight: weight, design: .rounded)
        }
        static func body(_ size: CGFloat = 15, weight: SwiftUI.Font.Weight = .medium) -> SwiftUI.Font {
            .system(size: size, weight: weight, design: .rounded)
        }
        static func caption(_ size: CGFloat = 12, weight: SwiftUI.Font.Weight = .semibold) -> SwiftUI.Font {
            .system(size: size, weight: weight, design: .rounded)
        }
    }

    // MARK: - Layout

    enum Radius {
        static let sm: CGFloat = 8
        static let md: CGFloat = 14
        static let lg: CGFloat = 22
        static let xl: CGFloat = 32
        static let pill: CGFloat = 999
    }

    enum Spacing {
        static let xs: CGFloat = 4
        static let sm: CGFloat = 8
        static let md: CGFloat = 12
        static let lg: CGFloat = 16
        static let xl: CGFloat = 24
        static let xxl: CGFloat = 32
    }

    // MARK: - Shadows

    enum Shadow {
        static func soft(color: Color = .black.opacity(0.08)) -> some ViewModifier {
            ShadowModifier(color: color, radius: 14, x: 0, y: 6)
        }
        static func lifted() -> some ViewModifier {
            ShadowModifier(color: .black.opacity(0.18), radius: 22, x: 0, y: 10)
        }
        static func inset() -> some ViewModifier {
            ShadowModifier(color: .black.opacity(0.04), radius: 4, x: 0, y: 1)
        }
    }
}

private struct ShadowModifier: ViewModifier {
    let color: Color
    let radius: CGFloat
    let x: CGFloat
    let y: CGFloat
    func body(content: Content) -> some View {
        content.shadow(color: color, radius: radius, x: x, y: y)
    }
}

extension View {
    func cardShadow() -> some View { modifier(Theme.Shadow.soft()) }
    func liftedShadow() -> some View { modifier(Theme.Shadow.lifted()) }
}
