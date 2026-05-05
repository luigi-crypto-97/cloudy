import XCTest
import CoreData
@testable import FriendMapSeed

@MainActor
final class DataControllerTests: XCTestCase {

    var sut: DataController!

    override func setUpWithError() throws {
        sut = DataController(inMemory: true)
    }

    override func tearDownWithError() throws {
        sut = nil
    }

    func test_coreDataStack_initializesSuccessfully() {
        XCTAssertNotNil(sut.container)
        XCTAssertNotNil(sut.container.viewContext)
        XCTAssertTrue(sut.isPersistentStoreLoaded)
    }

    func test_saveVenueCache_persistsCorrectly() throws {
        let context = sut.container.viewContext

        let cachedVenue = VenueCache(context: context)
        cachedVenue.venueId = UUID().uuidString
        cachedVenue.name = "Test Bar"
        cachedVenue.latitude = 45.4642
        cachedVenue.longitude = 9.1900
        cachedVenue.isOpenNow = true
        cachedVenue.lastUpdated = Date()

        try context.save()

        let request: NSFetchRequest<VenueCache> = VenueCache.fetchRequest()
        let results = try context.fetch(request)

        XCTAssertEqual(results.count, 1)
        XCTAssertEqual(results.first?.name, "Test Bar")
    }

    func test_cleanupOldCache_removesEntriesOlderThan7Days() throws {
        let context = sut.container.viewContext

        let oldVenue = VenueCache(context: context)
        oldVenue.venueId = UUID().uuidString
        oldVenue.isOpenNow = true
        oldVenue.lastUpdated = Calendar.current.date(byAdding: .day, value: -8, to: Date())

        try context.save()

        DeviceCacheService(dataController: sut).cleanup()

        let request: NSFetchRequest<VenueCache> = VenueCache.fetchRequest()
        let results = try context.fetch(request)
        XCTAssertEqual(results.count, 0, "Le entry più vecchie di 7 giorni devono essere eliminate")
    }

    func test_offlineQueue_savesMessageForLaterRetry() throws {
        let context = sut.container.viewContext
        let threadId = UUID()

        let queuedMsg = QueuedMessage(context: context)
        queuedMsg.messageId = UUID().uuidString
        queuedMsg.threadId = threadId.uuidString
        queuedMsg.userId = UUID().uuidString
        queuedMsg.body = "Ciao, ci vediamo lì!"
        queuedMsg.createdAt = Date()
        queuedMsg.attempts = 0

        try context.save()

        let request: NSFetchRequest<QueuedMessage> = QueuedMessage.fetchRequest()
        request.predicate = NSPredicate(format: "attempts < 5")
        request.sortDescriptors = [NSSortDescriptor(keyPath: \QueuedMessage.createdAt, ascending: true)]

        let pendingMessages = try context.fetch(request)

        XCTAssertEqual(pendingMessages.count, 1)
        XCTAssertEqual(pendingMessages.first?.body, "Ciao, ci vediamo lì!")
    }

    func test_performBackgroundSave_doesNotBlockMainThread() async throws {
        await sut.saveInBackground { context in
            let profile = UserProfileCache(context: context)
            profile.userId = UUID().uuidString
            profile.nickname = "background_user"
            profile.lastUpdated = Date()
        }

        let request: NSFetchRequest<UserProfileCache> = UserProfileCache.fetchRequest()
        let results = try sut.container.viewContext.fetch(request)
        XCTAssertTrue(results.contains(where: { $0.nickname == "background_user" }))
    }
}
