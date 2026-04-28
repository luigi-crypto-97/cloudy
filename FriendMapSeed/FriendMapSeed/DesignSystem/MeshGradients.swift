//
//  MeshGradients.swift
//  Cloudy — Static blue hero gradients.
//

import SwiftUI

enum CloudyMeshPreset {
    case loginHero
    case mapHero
    case profileHero
    case ghostMode
    case auroraNight

    var colors: [Color] {
        switch self {
        case .loginHero:
            return [
                Theme.Palette.blue900,
                Theme.Palette.blue600,
                Theme.Palette.blue400,
                Theme.Palette.blue700,
                Theme.Palette.blue500,
                Theme.Palette.mint400.opacity(0.72),
                Theme.Palette.blue900,
                Theme.Palette.blue600,
                Theme.Palette.blue100
            ]
        case .mapHero, .profileHero:
            return [
                Theme.Palette.blue50,
                Theme.Palette.surface,
                Theme.Palette.blue100,
                Theme.Palette.surfaceAlt,
                Theme.Palette.blue50,
                Theme.Palette.blue200,
                Theme.Palette.surface,
                Theme.Palette.blue100,
                Theme.Palette.surfaceAlt
            ]
        case .ghostMode, .auroraNight:
            return [
                Theme.Palette.appBackground,
                Theme.Palette.blue900,
                Theme.Palette.surfaceAlt,
                Theme.Palette.blue700,
                Theme.Palette.surface,
                Theme.Palette.blue600,
                Theme.Palette.appBackground,
                Theme.Palette.blue900,
                Theme.Palette.surface
            ]
        }
    }
}

struct MeshGradientBackground: View {
    var preset: CloudyMeshPreset
    var speed: Double = 0

    var body: some View {
        if #available(iOS 18.0, *) {
            MeshGradient(
                width: 3,
                height: 3,
                points: staticPoints,
                colors: preset.colors,
                background: Theme.Palette.appBackground,
                smoothsColors: true
            )
            .ignoresSafeArea()
        } else {
            LinearGradient(colors: preset.colors, startPoint: .topLeading, endPoint: .bottomTrailing)
                .ignoresSafeArea()
        }
    }

    private var staticPoints: [SIMD2<Float>] {
        [
            SIMD2(0.00, 0.00), SIMD2(0.50, 0.00), SIMD2(1.00, 0.00),
            SIMD2(0.00, 0.52), SIMD2(0.50, 0.46), SIMD2(1.00, 0.54),
            SIMD2(0.00, 1.00), SIMD2(0.50, 1.00), SIMD2(1.00, 1.00)
        ]
    }
}
