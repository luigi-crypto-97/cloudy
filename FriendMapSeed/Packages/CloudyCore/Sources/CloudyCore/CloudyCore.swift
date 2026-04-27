//
//  CloudyCore.swift
//  Logica di dominio pura, cross-platform, senza dipendenze UI / MapKit.
//
//  Contiene:
//   - Geometria 2D (LatLon) + distanza haversine
//   - Algoritmo fog-link cluster (sostituisce BuildFogLinks O(n²) di MAUI)
//   - Decoding helpers per backend C# (PascalCase + ISO8601 frazionato)
//   - VenueClusterInput: protocollo che il client iOS può adattare al suo VenueMarker
//

import Foundation

// MARK: - Geometry

public struct LatLon: Equatable, Hashable, Sendable {
    public let lat: Double
    public let lng: Double
    public init(lat: Double, lng: Double) {
        self.lat = lat
        self.lng = lng
    }
}

public enum Geo {
    /// Distanza haversine in metri tra due punti.
    public static func distance(_ a: LatLon, _ b: LatLon) -> Double {
        let R = 6_371_000.0 // raggio Terra (m)
        let φ1 = a.lat * .pi / 180
        let φ2 = b.lat * .pi / 180
        let Δφ = (b.lat - a.lat) * .pi / 180
        let Δλ = (b.lng - a.lng) * .pi / 180

        let h = sin(Δφ/2) * sin(Δφ/2)
              + cos(φ1) * cos(φ2) * sin(Δλ/2) * sin(Δλ/2)
        let c = 2 * atan2(sqrt(h), sqrt(1 - h))
        return R * c
    }
}

// MARK: - Cluster input protocol

/// Adattabile a `VenueMarker` (client iOS) o a qualsiasi altro tipo:
/// basta implementare questi tre membri.
public protocol VenueClusterInput {
    var clusterId: String { get }
    var location: LatLon { get }
    var bubbleIntensity: Int { get }
}

// MARK: - Fog link

public struct FogLinkResult: Equatable, Hashable, Sendable {
    public let id: String
    public let fromId: String
    public let toId: String
    public let from: LatLon
    public let to: LatLon
    public let distanceMeters: Double
    public let strength: Double // 0..1

    public init(
        id: String,
        fromId: String,
        toId: String,
        from: LatLon,
        to: LatLon,
        distanceMeters: Double,
        strength: Double
    ) {
        self.id = id
        self.fromId = fromId
        self.toId = toId
        self.from = from
        self.to = to
        self.distanceMeters = distanceMeters
        self.strength = strength
    }
}

public enum FogLinkBuilder {

    /// Costruisce le "fog links" tra cluster vicini.
    /// - parameter inputs: lista cluster.
    /// - parameter maxDistanceMeters: soglia oltre la quale due cluster non si linkano.
    /// - parameter minIntensity: ignora cluster con intensity inferiore.
    /// - parameter minStrength: ignora link con strength sotto soglia (riduce rumore visivo).
    /// - parameter maxClusters: limita il numero di cluster considerati per evitare O(n²) esplosivi.
    public static func build<T: VenueClusterInput>(
        from inputs: [T],
        maxDistanceMeters: Double = 600,
        minIntensity: Int = 1,
        minStrength: Double = 0.15,
        maxClusters: Int = 100
    ) -> [FogLinkResult] {
        guard inputs.count > 1 else { return [] }

        // 1. Filter + sort per intensity desc → privilegia cluster densi.
        let sorted = inputs
            .filter { $0.bubbleIntensity >= minIntensity }
            .sorted { $0.bubbleIntensity > $1.bubbleIntensity }

        let n = min(sorted.count, maxClusters)
        guard n > 1 else { return [] }

        var results: [FogLinkResult] = []
        results.reserveCapacity(n) // tipicamente ~ n a n*1.5

        for i in 0..<n {
            let a = sorted[i]
            for j in (i+1)..<n {
                let b = sorted[j]
                let d = Geo.distance(a.location, b.location)
                if d > maxDistanceMeters { continue }

                let normalized = max(0, 1 - d / maxDistanceMeters)
                let intensity = Double(min(a.bubbleIntensity, b.bubbleIntensity))
                let strength = min(1, normalized * (0.4 + intensity * 0.12))
                if strength < minStrength { continue }

                results.append(FogLinkResult(
                    id: "\(a.clusterId)|\(b.clusterId)",
                    fromId: a.clusterId,
                    toId: b.clusterId,
                    from: a.location,
                    to: b.location,
                    distanceMeters: d,
                    strength: strength
                ))
            }
        }
        return results
    }
}

// MARK: - JSON helpers (PascalCase + ISO8601 frazionato)

public enum CloudyJSON {

    public static func makeDecoder() -> JSONDecoder {
        let d = JSONDecoder()
        d.keyDecodingStrategy = .custom { keys in
            let key = keys.last!.stringValue
            guard let first = key.first else { return keys.last! }
            return AnyKey(stringValue: first.lowercased() + key.dropFirst())
        }
        d.dateDecodingStrategy = .custom { decoder in
            let c = try decoder.singleValueContainer()
            let s = try c.decode(String.self)

            let withFrac = ISO8601DateFormatter()
            withFrac.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
            if let d = withFrac.date(from: s) { return d }

            let plain = ISO8601DateFormatter()
            plain.formatOptions = [.withInternetDateTime]
            if let d = plain.date(from: s) { return d }

            throw DecodingError.dataCorruptedError(in: c, debugDescription: "Bad date \(s)")
        }
        return d
    }

    public static func makeEncoder() -> JSONEncoder {
        let e = JSONEncoder()
        e.keyEncodingStrategy = .custom { keys in
            let key = keys.last!.stringValue
            guard let first = key.first else { return keys.last! }
            return AnyKey(stringValue: first.uppercased() + key.dropFirst())
        }
        e.dateEncodingStrategy = .iso8601
        return e
    }
}

private struct AnyKey: CodingKey {
    var stringValue: String
    var intValue: Int? { nil }
    init(stringValue: String) { self.stringValue = stringValue }
    init?(intValue: Int) { return nil }
}
