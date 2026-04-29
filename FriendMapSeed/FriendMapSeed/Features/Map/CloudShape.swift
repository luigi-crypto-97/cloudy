//
//  CloudShape.swift
//  Cloudy — Custom Shape per la nuvola
//
//  Disegna una nuvola come UNICA path (nessuno stack di Border + RadialGradient
//  sovrapposti come faceva la versione MAUI). Una sola texture, quindi
//  zero rendering glitch su iOS.
//

import SwiftUI

struct CloudShape: Shape {
    /// Numero di "puff" laterali. 5 → nuvola classica.
    var puffCount: Int = 5

    func path(in rect: CGRect) -> Path {
        var path = Path()
        let w = rect.width
        let h = rect.height

        // Body: ellisse centrale ampia
        let body = CGRect(x: w * 0.10, y: h * 0.30, width: w * 0.80, height: h * 0.55)
        path.addEllipse(in: body)

        // Puff superiori — leggermente sovrapposti
        let puffRadii: [CGFloat] = [0.18, 0.24, 0.27, 0.22, 0.16]
        let puffOffsets: [CGFloat] = [0.18, 0.36, 0.55, 0.74, 0.88]
        let puffYBias:  [CGFloat] = [0.50, 0.30, 0.10, 0.28, 0.55]

        for i in 0..<min(puffCount, puffRadii.count) {
            let r = puffRadii[i] * w
            let x = puffOffsets[i] * w - r / 2
            let y = puffYBias[i] * h * 0.6
            path.addEllipse(in: CGRect(x: x, y: y, width: r, height: r * 0.95))
        }

        return path
    }
}

// MARK: - Cloud bubble view

/// Una "nuvola pulsante" usata come marker mappa.
/// - Niente Timer / niente Animation infinito sul main thread.
/// - Una sola TimelineView a livello di mappa orchestra il pulse di tutte
///   le nuvole tramite una pure-function di fase.
struct CloudBubble: View {
    let intensity: Int        // 0..10 — bubble intensity dal backend
    let peopleCount: Int
    let densityLevel: String  // low / medium / high
    let isSelected: Bool
    /// Fase 0..1 condivisa proveniente dalla TimelineView della mappa.
    var phase: Double = 0

    private var size: CGFloat {
        // Da 70 → 130 in base all'intensità
        70 + min(60, CGFloat(intensity) * 6)
    }

    private var tint: Color {
        switch densityLevel.lowercased() {
        case "low":    return Theme.Palette.skyTint.opacity(0.55)
        case "medium": return Theme.Palette.honeySoft.opacity(0.75)
        case "high":   return Theme.Palette.densityHigh.opacity(0.45)
        default:       return Theme.Palette.cloudFog.opacity(0.85)
        }
    }

    private var ringColor: Color {
        switch densityLevel.lowercased() {
        case "low":    return Theme.Palette.skyDeep
        case "medium": return Theme.Palette.honeyDeep
        case "high":   return Theme.Palette.densityHigh
        default:       return Theme.Palette.inkMuted
        }
    }

    var body: some View {
        let pulse = 1.0 + 0.04 * sin(phase * .pi * 2)        // ±4% scale
        let glow  = 0.55 + 0.20 * sin(phase * .pi * 2 + 0.6) // glow radius

        ZStack {
            // Glow di sfondo (pulse)
            CloudShape()
                .fill(tint)
                .frame(width: size * 1.18, height: size * 0.86)
                .blur(radius: 14 * glow)
                .opacity(0.7)

            // Corpo nuvola — un solo gradient radial
            CloudShape()
                .fill(Theme.Gradients.cloudBody)
                .frame(width: size, height: size * 0.72)
                .overlay(
                    CloudShape()
                        .stroke(ringColor.opacity(isSelected ? 0.9 : 0.35), lineWidth: isSelected ? 2.5 : 1.3)
                        .frame(width: size, height: size * 0.72)
                )
                .scaleEffect(pulse)

            // Etichetta centrale (numero persone)
            VStack(spacing: 0) {
                Text("\(peopleCount)")
                    .font(Theme.Font.title(intensity > 6 ? 22 : 18, weight: .heavy))
                    .foregroundStyle(Theme.Palette.ink)
                Text(peopleCount == 1 ? "persona" : "persone")
                    .font(Theme.Font.caption(10, weight: .semibold))
                    .foregroundStyle(Theme.Palette.inkMuted)
            }
            .scaleEffect(pulse)
        }
        .frame(width: size * 1.2, height: size * 0.9)
        .shadow(color: ringColor.opacity(0.18), radius: 12, x: 0, y: 4)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(Text("\(peopleCount) persone, densità \(densityLevel)"))
        .accessibilityAddTraits(isSelected ? .isSelected : [])
    }
}

struct CompactVenueBubble: View {
    let peopleCount: Int
    let densityLevel: String
    let isSelected: Bool

    private var color: Color {
        switch densityLevel.lowercased() {
        case "low", "very_low": return Theme.Palette.skyDeep
        case "medium": return Theme.Palette.honeyDeep
        case "high", "very_high": return Theme.Palette.densityHigh
        default: return Theme.Palette.inkMuted
        }
    }

    var body: some View {
        ZStack {
            Circle()
                .fill(.white.opacity(0.94))
                .frame(width: 42, height: 42)
                .shadow(color: color.opacity(0.18), radius: 8, x: 0, y: 3)
            Circle()
                .stroke(color.opacity(isSelected ? 0.95 : 0.45), lineWidth: isSelected ? 3 : 1.5)
                .frame(width: 42, height: 42)
            Text("\(peopleCount)")
                .font(Theme.Font.body(13, weight: .heavy))
                .foregroundStyle(Theme.Palette.ink)
        }
        .accessibilityLabel(Text("\(peopleCount) persone"))
    }
}

struct AreaDensityBubble: View {
    let area: VenueMapArea

    private var color: Color {
        switch area.densityLevel.lowercased() {
        case "low", "very_low": return Theme.Palette.skyDeep
        case "medium": return Theme.Palette.honeyDeep
        case "high", "very_high": return Theme.Palette.densityHigh
        default: return Theme.Palette.inkMuted
        }
    }

    var body: some View {
        VStack(spacing: 2) {
            ZStack {
                Circle()
                    .fill(color.opacity(0.18))
                    .frame(width: 76, height: 76)
                Circle()
                    .fill(.white.opacity(0.92))
                    .frame(width: 56, height: 56)
                    .shadow(color: color.opacity(0.18), radius: 10, x: 0, y: 4)
                VStack(spacing: 0) {
                    Text("\(area.peopleCount)")
                        .font(Theme.Font.title(18, weight: .heavy))
                        .foregroundStyle(Theme.Palette.ink)
                    Text("\(area.venueCount) locali")
                        .font(Theme.Font.caption(9, weight: .semibold))
                        .foregroundStyle(Theme.Palette.inkMuted)
                }
            }
            Text(area.label)
                .font(Theme.Font.caption(10, weight: .bold))
                .foregroundStyle(Theme.Palette.ink)
                .lineLimit(1)
                .padding(.horizontal, 8)
                .padding(.vertical, 3)
                .background(.thinMaterial, in: Capsule())
        }
        .accessibilityLabel(Text("\(area.label), \(area.peopleCount) persone, \(area.venueCount) locali"))
    }
}

#Preview {
    VStack(spacing: 24) {
        CloudBubble(intensity: 2, peopleCount: 4, densityLevel: "low", isSelected: false, phase: 0.2)
        CloudBubble(intensity: 6, peopleCount: 28, densityLevel: "medium", isSelected: true, phase: 0.5)
        CloudBubble(intensity: 9, peopleCount: 84, densityLevel: "high", isSelected: false, phase: 0.8)
    }
    .padding()
    .background(Theme.Palette.surfaceAlt)
}
