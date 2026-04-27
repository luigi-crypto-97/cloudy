//
//  FogLinkTests.swift
//  Test della logica di clustering (sostituisce BuildFogLinks O(n²) MAUI).
//

import XCTest
@testable import CloudyCore

private struct StubVenue: VenueClusterInput {
    let clusterId: String
    let location: LatLon
    let bubbleIntensity: Int
}

final class FogLinkTests: XCTestCase {

    // MARK: - Geo distance

    func test_distance_milanoZero() {
        let p = LatLon(lat: 45.4642, lng: 9.1900)
        XCTAssertEqual(Geo.distance(p, p), 0, accuracy: 0.001)
    }

    func test_distance_milanoCentroVsDuomoNoto() {
        // Distanza nota: Duomo Milano → Castello Sforzesco ≈ 1100 m
        let duomo    = LatLon(lat: 45.4641, lng: 9.1916)
        let castello = LatLon(lat: 45.4706, lng: 9.1791)
        let d = Geo.distance(duomo, castello)
        XCTAssertEqual(d, 1170, accuracy: 200) // tolleranza ampia per haversine
    }

    // MARK: - Fog links

    func test_emptyInput_returnsNoLinks() {
        XCTAssertEqual(FogLinkBuilder.build(from: [StubVenue]()).count, 0)
    }

    func test_singleInput_returnsNoLinks() {
        let only = [StubVenue(clusterId: "a", location: LatLon(lat: 45, lng: 9), bubbleIntensity: 5)]
        XCTAssertEqual(FogLinkBuilder.build(from: only).count, 0)
    }

    func test_twoCloseClusters_areLinked() {
        // Due cluster a 100m: sotto la soglia di 600m → linkati.
        let a = StubVenue(clusterId: "a", location: LatLon(lat: 45.4642, lng: 9.1900), bubbleIntensity: 5)
        let b = StubVenue(clusterId: "b", location: LatLon(lat: 45.4651, lng: 9.1900), bubbleIntensity: 5) // ~100m N
        let links = FogLinkBuilder.build(from: [a, b])
        XCTAssertEqual(links.count, 1)
        XCTAssertGreaterThan(links[0].strength, 0.4)
        XCTAssertLessThan(links[0].distanceMeters, 200)
    }

    func test_farClusters_areNotLinked() {
        // Due cluster a >2km → non linkati.
        let a = StubVenue(clusterId: "a", location: LatLon(lat: 45.4642, lng: 9.1900), bubbleIntensity: 9)
        let b = StubVenue(clusterId: "b", location: LatLon(lat: 45.5000, lng: 9.2400), bubbleIntensity: 9)
        XCTAssertEqual(FogLinkBuilder.build(from: [a, b]).count, 0)
    }

    func test_lowIntensity_areFiltered() {
        let a = StubVenue(clusterId: "a", location: LatLon(lat: 45.4642, lng: 9.1900), bubbleIntensity: 0)
        let b = StubVenue(clusterId: "b", location: LatLon(lat: 45.4651, lng: 9.1900), bubbleIntensity: 0)
        XCTAssertEqual(FogLinkBuilder.build(from: [a, b], minIntensity: 1).count, 0)
    }

    func test_strengthOrderedByDistanceAndIntensity() {
        let center = LatLon(lat: 45.4642, lng: 9.1900)
        // a-b vicini e intensi → strength alto
        let a = StubVenue(clusterId: "a", location: center, bubbleIntensity: 9)
        let b = StubVenue(clusterId: "b", location: LatLon(lat: 45.4647, lng: 9.1900), bubbleIntensity: 9) // ~55m
        // a-c ancora vicini ma poco intensi → strength minore
        let c = StubVenue(clusterId: "c", location: LatLon(lat: 45.4649, lng: 9.1900), bubbleIntensity: 1) // ~75m
        let links = FogLinkBuilder.build(from: [a, b, c])
        let ab = links.first { $0.id.contains("a") && $0.id.contains("b") }
        let ac = links.first { $0.id.contains("a") && $0.id.contains("c") }
        XCTAssertNotNil(ab)
        XCTAssertNotNil(ac)
        XCTAssertGreaterThan(ab!.strength, ac!.strength)
    }

    func test_maxClustersLimit_capsBigInputs() {
        // 200 cluster densi → limitato a 100 considerati.
        let inputs = (0..<200).map { i in
            StubVenue(
                clusterId: "v\(i)",
                location: LatLon(lat: 45.4642 + Double(i) * 0.0001, lng: 9.1900),
                bubbleIntensity: 5
            )
        }
        let links = FogLinkBuilder.build(from: inputs, maxClusters: 100)
        // Numero massimo di link possibili tra 100 nodi: 100*99/2 = 4950
        XCTAssertLessThanOrEqual(links.count, 4950)
        // Verifico che non vengano usati cluster oltre l'indice 99.
        // Estraggo i clusterId effettivamente toccati e confronto con il set permesso.
        let allowed = Set((0..<100).map { "v\($0)" })
        for link in links {
            XCTAssertTrue(allowed.contains(link.fromId), "fromId fuori range: \(link.fromId)")
            XCTAssertTrue(allowed.contains(link.toId),   "toId fuori range: \(link.toId)")
        }
    }

    func test_performance_60clusters_under50ms() {
        // 60 cluster su 6km — caso realistico per un viewport. Deve girare
        // ben sotto a 50ms per non pesare sul main thread (ed è eseguito su
        // background nel client iOS).
        let inputs = (0..<60).map { i -> StubVenue in
            let row = i / 8
            let col = i % 8
            return StubVenue(
                clusterId: "v\(i)",
                location: LatLon(
                    lat: 45.4642 + Double(row) * 0.001,
                    lng: 9.1900 + Double(col) * 0.001
                ),
                bubbleIntensity: 1 + (i % 9)
            )
        }
        let start = Date()
        let links = FogLinkBuilder.build(from: inputs)
        let elapsed = Date().timeIntervalSince(start)
        XCTAssertLessThan(elapsed, 0.05, "fog link build deve essere < 50ms (era \(elapsed * 1000)ms)")
        XCTAssertGreaterThan(links.count, 0)
    }
}

// MARK: - JSON decoder tests (compat backend C# PascalCase)

final class CloudyJSONTests: XCTestCase {

    private struct Sample: Codable, Equatable {
        let userId: String
        let displayName: String
        let createdAtUtc: Date
    }

    func test_decoder_acceptsPascalCaseFromCSharpBackend() throws {
        let json = """
        {
          "UserId": "abc-123",
          "DisplayName": "Giulia",
          "CreatedAtUtc": "2026-04-15T17:18:57.123Z"
        }
        """.data(using: .utf8)!
        let s = try CloudyJSON.makeDecoder().decode(Sample.self, from: json)
        XCTAssertEqual(s.userId, "abc-123")
        XCTAssertEqual(s.displayName, "Giulia")
    }

    func test_decoder_acceptsIsoWithoutFractional() throws {
        let json = """
        {"UserId":"x","DisplayName":"y","CreatedAtUtc":"2026-04-15T17:18:57Z"}
        """.data(using: .utf8)!
        let s = try CloudyJSON.makeDecoder().decode(Sample.self, from: json)
        XCTAssertEqual(s.userId, "x")
    }

    func test_encoderRoundTrip_emitsPascalCase() throws {
        let sample = Sample(userId: "u1", displayName: "Anna", createdAtUtc: Date(timeIntervalSince1970: 0))
        let data = try CloudyJSON.makeEncoder().encode(sample)
        let str = String(data: data, encoding: .utf8) ?? ""
        XCTAssertTrue(str.contains("\"UserId\""), "atteso PascalCase: \(str)")
        XCTAssertTrue(str.contains("\"DisplayName\""))
    }
}
