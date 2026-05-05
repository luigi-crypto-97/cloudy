//
//  DataController.swift
//  Cloudy
//
//  CoreData stack centralizzato. La cache locale deve essere un supporto,
//  non un single point of failure: se lo store persistente fallisce, l'app
//  parte comunque e segnala l'errore.
//

import CoreData
import Foundation

@MainActor
public final class DataController {
    public static let shared = DataController()
    public static let preview = DataController(inMemory: true)

    public let container: NSPersistentContainer
    public private(set) var isPersistentStoreLoaded = false
    public private(set) var lastLoadError: Error?

    public var viewContext: NSManagedObjectContext {
        container.viewContext
    }

    public init(inMemory: Bool = false) {
        container = NSPersistentContainer(name: "CloudyModel")

        if inMemory {
            let description = NSPersistentStoreDescription()
            description.type = NSInMemoryStoreType
            container.persistentStoreDescriptions = [description]
        } else {
            container.persistentStoreDescriptions.forEach { description in
                description.shouldMigrateStoreAutomatically = true
                description.shouldInferMappingModelAutomatically = true
            }
        }

        container.loadPersistentStores { [weak self] _, error in
            guard let self else { return }
            if let error {
                self.lastLoadError = error
                self.isPersistentStoreLoaded = false
                print("[CoreData] Store load failed: \(error.localizedDescription)")
                CrashReportingService.shared.recordError(error, context: "CoreData store load")
            } else {
                self.isPersistentStoreLoaded = true
            }
        }

        container.viewContext.automaticallyMergesChangesFromParent = true
        container.viewContext.mergePolicy = NSMergeByPropertyObjectTrumpMergePolicy
    }

    public func saveIfNeeded() {
        guard viewContext.hasChanges else { return }
        do {
            try viewContext.save()
        } catch {
            lastLoadError = error
            print("[CoreData] Save failed: \(error.localizedDescription)")
            CrashReportingService.shared.recordError(error, context: "CoreData save")
            viewContext.rollback()
        }
    }

    /// Salva dati in background senza bloccare la UI. Utile per future code offline.
    public func saveInBackground(_ block: @escaping (NSManagedObjectContext) -> Void) async {
        let backgroundContext = container.newBackgroundContext()
        backgroundContext.mergePolicy = NSMergeByPropertyObjectTrumpMergePolicy

        await backgroundContext.perform {
            block(backgroundContext)

            guard backgroundContext.hasChanges else { return }
            do {
                try backgroundContext.save()
            } catch {
                print("[CoreData] Background save failed: \(error.localizedDescription)")
            }
        }
    }

    public func cleanupOldCache() {
        Task { @MainActor in
            DeviceCacheService.shared.cleanup()
        }
    }

    /// Helper usato dai test e come fallback offline.
    public func insertMockVenueCache(name: String) throws {
        let venue = VenueCache(context: viewContext)
        venue.venueId = UUID().uuidString
        venue.name = name
        venue.category = "demo"
        venue.addressLine = ""
        venue.city = ""
        venue.latitude = 45.4642
        venue.longitude = 9.19
        venue.isOpenNow = true
        venue.peopleEstimate = 0
        venue.densityLevel = "cached"
        venue.bubbleIntensity = 0
        venue.lastUpdated = Date()
        try viewContext.save()
    }
}
