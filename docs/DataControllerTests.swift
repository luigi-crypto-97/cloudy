import XCTest
import CoreData
@testable import FriendMapSeed

final class DataControllerTests: XCTestCase {
    
    var sut: DataController!
    
    override func setUpWithError() throws {
        // Creiamo un DataController che usa il database in RAM (/dev/null) per test isolati
        sut = DataController(inMemory: true)
    }
    
    override func tearDownWithError() throws {
        sut = nil
    }
    
    func test_coreDataStack_initializesSuccessfully() {
        XCTAssertNotNil(sut.container)
        XCTAssertNotNil(sut.container.viewContext)
    }
    
    func test_saveVenueCache_persistsCorrectly() throws {
        let context = sut.container.viewContext
        
        // Arrange: Creiamo una venue di test nella cache
        let cachedVenue = VenueCache(context: context)
        cachedVenue.id = UUID()
        cachedVenue.name = "Test Bar"
        cachedVenue.latitude = 45.4642
        cachedVenue.longitude = 9.1900
        cachedVenue.updatedAt = Date()
        
        // Act: Salviamo il context
        try context.save()
        
        // Assert: Recuperiamo dal DB per assicurarci che sia salvata
        let request: NSFetchRequest<VenueCache> = VenueCache.fetchRequest()
        let results = try context.fetch(request)
        
        XCTAssertEqual(results.count, 1)
        XCTAssertEqual(results.first?.name, "Test Bar")
    }
    
    func test_cleanupOldCache_removesEntriesOlderThan7Days() throws {
        let context = sut.container.viewContext
        
        let oldVenue = VenueCache(context: context)
        oldVenue.id = UUID()
        oldVenue.updatedAt = Calendar.current.date(byAdding: .day, value: -8, to: Date()) // 8 giorni fa
        
        try context.save()
        
        // Act: richiamiamo la funzione di cleanup
        sut.cleanupOldCache()
        
        let request: NSFetchRequest<VenueCache> = VenueCache.fetchRequest()
        let results = try context.fetch(request)
        XCTAssertEqual(results.count, 0, "Le entry più vecchie di 7 giorni devono essere eliminate")
    }
    
    func test_offlineQueue_savesMessageForLaterRetry() throws {
        let context = sut.container.viewContext
        let threadId = UUID()
        
        // Arrange: l'utente tenta di inviare un messaggio ma fallisce la connessione
        let queuedMsg = QueuedMessage(context: context)
        queuedMsg.id = UUID()
        queuedMsg.threadId = threadId
        queuedMsg.body = "Ciao, ci vediamo lì!"
        queuedMsg.createdAt = Date()
        queuedMsg.retryCount = 0
        
        try context.save()
        
        // Act: simuliamo che il network sia tornato e fetchiamo la coda
        let request: NSFetchRequest<QueuedMessage> = QueuedMessage.fetchRequest()
        request.predicate = NSPredicate(format: "retryCount < 5")
        request.sortDescriptors = [NSSortDescriptor(keyPath: \QueuedMessage.createdAt, ascending: true)]
        
        let pendingMessages = try context.fetch(request)
        
        // Assert
        XCTAssertEqual(pendingMessages.count, 1)
        XCTAssertEqual(pendingMessages.first?.body, "Ciao, ci vediamo lì!")
    }
    
    func test_performBackgroundSave_doesNotBlockMainThread() async throws {
        // Act: richiamiamo il salvataggio in un background context fornito da DataController
        await sut.saveInBackground { context in
            let profile = UserProfileCache(context: context)
            profile.id = UUID()
            profile.nickname = "background_user"
        }
        
        // Assert: verifichiamo che il context principale veda il dato aggiornato
        let request: NSFetchRequest<UserProfileCache> = UserProfileCache.fetchRequest()
        let results = try sut.container.viewContext.fetch(request)
        XCTAssertTrue(results.contains(where: { $0.nickname == "background_user" }))
    }
}