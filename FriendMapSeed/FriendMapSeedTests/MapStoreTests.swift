import XCTest
import MapKit
@testable import FriendMapSeed

@MainActor
final class MapStoreTests: XCTestCase {

    var sut: MapStore!

    override func setUp() {
        super.setUp()
        sut = MapStore()
    }

    override func tearDown() {
        sut = nil
        super.tearDown()
    }

    func test_viewportChange_keepsLatestRegion() async throws {
        let initialSpan = MKCoordinateSpan(latitudeDelta: 0.1, longitudeDelta: 0.1)
        let finalRegion = MKCoordinateRegion(
            center: CLLocationCoordinate2D(latitude: 45.05, longitude: 9.0),
            span: initialSpan
        )

        for i in 1...5 {
            sut.onViewportChanged(
                MKCoordinateRegion(
                    center: CLLocationCoordinate2D(latitude: 45.0 + Double(i) * 0.01, longitude: 9.0),
                    span: initialSpan
                )
            )
        }

        let viewport = try XCTUnwrap(sut.lastViewport)
        XCTAssertEqual(viewport.center.latitude, finalRegion.center.latitude, accuracy: 0.000_001)
        XCTAssertEqual(viewport.center.longitude, finalRegion.center.longitude, accuracy: 0.000_001)
    }

    func test_applyFilters_updatesLocalFilterState() {
        sut.applyFilters(query: "bar", category: "pub", openNow: true)

        XCTAssertEqual(sut.query, "bar")
        XCTAssertEqual(sut.category, "pub")
        XCTAssertTrue(sut.openNowOnly)
    }
}
