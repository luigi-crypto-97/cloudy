//
//  DensityCanvasView.swift
//  Cloudy — One-pass density renderer for the map.
//
//  Performance note: replaces per-venue CloudShape rendering with a single
//  Canvas pass. The preview below creates 200 fake points to keep the hot path
//  visible during profiling; Instruments should be used on-device for final fps.
//

import SwiftUI
import MapKit

struct DensityCanvasCluster: Identifiable {
    let id: String
    let coordinate: CLLocationCoordinate2D
    let weight: Double

    init(id: String, coordinate: CLLocationCoordinate2D, weight: Double) {
        self.id = id
        self.coordinate = coordinate
        self.weight = max(0, weight)
    }
}

struct DensityCanvasView: View {
    let clusters: [DensityCanvasCluster]
    let region: MKCoordinateRegion?

    var body: some View {
        GeometryReader { proxy in
            Canvas(opaque: false) { ctx, size in
                guard let region else { return }
                let visible = clusters.compactMap { cluster -> (CGPoint, CGFloat, Color)? in
                    guard let point = project(cluster.coordinate, in: region, size: size) else {
                        return nil
                    }
                    let radius = CGFloat(18 + min(cluster.weight, 12) * 4)
                    return (point, radius, color(for: cluster.weight))
                }

                ctx.addFilter(.blur(radius: 32))
                ctx.addFilter(.alphaThreshold(min: 0.42, color: Theme.Palette.blue500.opacity(0.72)))
                for item in visible {
                    let rect = CGRect(
                        x: item.0.x - item.1,
                        y: item.0.y - item.1,
                        width: item.1 * 2,
                        height: item.1 * 2
                    )
                    ctx.fill(Path(ellipseIn: rect), with: .color(.white))
                }

                ctx.addFilter(.blur(radius: 10))
                for item in visible where item.1 > 28 {
                    let core = item.1 * 0.46
                    let rect = CGRect(
                        x: item.0.x - core,
                        y: item.0.y - core,
                        width: core * 2,
                        height: core * 2
                    )
                    ctx.fill(Path(ellipseIn: rect), with: .color(item.2.opacity(0.34)))
                }
            }
            .frame(width: proxy.size.width, height: proxy.size.height)
        }
        .allowsHitTesting(false)
        .accessibilityHidden(true)
    }

    private func color(for weight: Double) -> Color {
        switch weight {
        case 0..<3:
            return Theme.Palette.densityLow
        case 3..<7:
            return Theme.Palette.densityMedium
        case 7..<11:
            return Theme.Palette.densityHigh
        default:
            return Theme.Palette.densityPeak
        }
    }

    private func project(_ coordinate: CLLocationCoordinate2D, in region: MKCoordinateRegion, size: CGSize) -> CGPoint? {
        let latDelta = max(region.span.latitudeDelta, 0.000_001)
        let lngDelta = max(region.span.longitudeDelta, 0.000_001)
        let minLat = region.center.latitude - latDelta / 2
        let maxLat = region.center.latitude + latDelta / 2
        let minLng = region.center.longitude - lngDelta / 2
        let maxLng = region.center.longitude + lngDelta / 2

        guard coordinate.latitude >= minLat,
              coordinate.latitude <= maxLat,
              coordinate.longitude >= minLng,
              coordinate.longitude <= maxLng else {
            return nil
        }

        let x = (coordinate.longitude - minLng) / lngDelta * size.width
        let y = (maxLat - coordinate.latitude) / latDelta * size.height
        return CGPoint(x: x, y: y)
    }
}

#if DEBUG
private struct DensityCanvasStressPreview: View {
    private let region = MKCoordinateRegion(
        center: CLLocationCoordinate2D(latitude: 45.4642, longitude: 9.1900),
        span: MKCoordinateSpan(latitudeDelta: 0.06, longitudeDelta: 0.06)
    )

    private var points: [DensityCanvasCluster] {
        (0..<200).map { index in
            DensityCanvasCluster(
                id: "p-\(index)",
                coordinate: CLLocationCoordinate2D(
                    latitude: 45.4642 + Double.random(in: -0.025...0.025),
                    longitude: 9.1900 + Double.random(in: -0.025...0.025)
                ),
                weight: Double.random(in: 1...12)
            )
        }
    }

    var body: some View {
        DensityCanvasView(clusters: points, region: region)
            .background(Theme.Palette.appBackground)
    }
}

#Preview("Density Canvas 200 punti") {
    DensityCanvasStressPreview()
}
#endif
