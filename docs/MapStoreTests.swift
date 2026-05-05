import XCTest
import MapKit
@testable import FriendMapSeed

@MainActor
final class MapStoreTests: XCTestCase {
    
    var sut: MapStore!
    
    override func setUp() {
        super.setUp()
        sut = MapStore(dataController: DataController(inMemory: true)) // Inietta mock
    }
    
    override func tearDown() {
        sut = nil
        super.tearDown()
    }
    
    func test_regionChange_debouncesNetworkRequests() async throws {
        let initialRegion = MKCoordinateRegion(center: CLLocationCoordinate2D(latitude: 45.0, longitude: 9.0), span: MKCoordinateSpan(latitudeDelta: 0.1, longitudeDelta: 0.1))
        
        // Act: simuliamo che l'utente muova la mappa 5 volte di fila molto velocemente
        for i in 1...5 {
            sut.updateRegion(MKCoordinateRegion(center: CLLocationCoordinate2D(latitude: 45.0 + Double(i)*0.01, longitude: 9.0), span: initialRegion.span))
        }
        
        // Aspettiamo meno del tempo di debounce (es. 100ms su 350ms previsti)
        try await Task.sleep(nanoseconds: 100_000_000)
        XCTAssertFalse(sut.isFetching, "Non dovrebbe fetchare immediatamente a causa del debounce")
        
        // Aspettiamo che il debounce scatti (350ms)
        try await Task.sleep(nanoseconds: 400_000_000)
        
        // Assumendo che isLoading scatti al momento della richiesta
        XCTAssertTrue(sut.hasPerformedFetchForLatestRegion, "Il fetch deve essere scattato per l'ULTIMA regione impostata")
    }
    
    func test_fetchFailure_loadsFromDeviceCache() async throws {
        // Arrange: impostiamo la rete fallita o offline
        sut.simulateOfflineMode = true 
        
        // Inseriamo a mano un dato finto nella cache tramite mock
        try sut.dataController.insertMockVenueCache(name: "Offline Bar")
        
        // Act
        await sut.fetchMarkers(in: MKCoordinateRegion())
        
        // Assert
        XCTAssertTrue(sut.markers.contains(where: { $0.name == "Offline Bar" }), "Deve fallbackare sui locali in CoreData se API fallisce")
    }
}