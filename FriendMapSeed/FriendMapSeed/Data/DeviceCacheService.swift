//
//  DeviceCacheService.swift
//  Cloudy
//
//  Cache locale privacy-safe per ridurre schermate vuote e consentire fallback
//  offline. Per ora collega la cache venue alla mappa; le altre entity CoreData
//  restano pronte per chat/stories/profili senza bloccare la release.
//

import CoreData
import Foundation

struct DeviceCacheStatus: Hashable {
    let isStoreLoaded: Bool
    let cachedVenues: Int
    let cachedStories: Int
    let cachedMessages: Int
    let queuedMessages: Int
    let cachedProfiles: Int
    let lastErrorDescription: String?
}

@MainActor
final class DeviceCacheService {
    static let shared = DeviceCacheService()

    private let dataController: DataController
    private var context: NSManagedObjectContext { dataController.viewContext }

    private convenience init() {
        self.init(dataController: DataController.shared)
    }

    init(dataController: DataController) {
        self.dataController = dataController
    }

    func cachedVenues(
        minLat: Double,
        minLng: Double,
        maxLat: Double,
        maxLng: Double,
        maxAge: TimeInterval = 24 * 60 * 60
    ) -> [VenueMarker] {
        let request = VenueCache.fetchRequest()
        request.fetchLimit = 160
        request.sortDescriptors = [
            NSSortDescriptor(keyPath: \VenueCache.peopleEstimate, ascending: false),
            NSSortDescriptor(keyPath: \VenueCache.lastUpdated, ascending: false)
        ]
        request.predicate = NSPredicate(
            format: "latitude >= %lf AND latitude <= %lf AND longitude >= %lf AND longitude <= %lf AND lastUpdated >= %@",
            minLat,
            maxLat,
            minLng,
            maxLng,
            Date(timeIntervalSinceNow: -maxAge) as NSDate
        )

        do {
            return try context.fetch(request).compactMap(makeVenueMarker)
        } catch {
            print("[DeviceCache] Venue fetch failed: \(error.localizedDescription)")
            CrashReportingService.shared.recordError(error, context: "DeviceCache venue fetch")
            return []
        }
    }

    func cacheVenues(_ venues: [VenueMarker]) {
        guard !venues.isEmpty else { return }
        let now = Date()
        for venue in venues {
            let row = fetchOrCreateVenue(id: venue.venueId.uuidString)
            row.venueId = venue.venueId.uuidString
            row.name = venue.name
            row.category = venue.category
            row.addressLine = venue.addressLine
            row.city = venue.city
            row.phoneNumber = venue.phoneNumber
            row.websiteUrl = venue.websiteUrl
            row.hoursSummary = venue.hoursSummary
            row.coverImageUrl = venue.coverImageUrl
            row.descriptionText = venue.description
            row.latitude = venue.latitude
            row.longitude = venue.longitude
            row.isOpenNow = venue.isOpenNow
            row.peopleEstimate = Int32(max(0, venue.peopleEstimate))
            row.densityLevel = venue.densityLevel
            row.bubbleIntensity = Int32(max(0, venue.bubbleIntensity))
            row.averageRating = venue.averageRating
            row.ratingCount = Int32(max(0, venue.ratingCount))
            row.lastUpdated = now
        }
        dataController.saveIfNeeded()
    }

    func cachedStories(includeExpired: Bool = false, maxAge: TimeInterval = 14 * 24 * 60 * 60) -> [UserStory] {
        let request = StoryCache.fetchRequest()
        request.fetchLimit = 240
        request.sortDescriptors = [NSSortDescriptor(keyPath: \StoryCache.createdAt, ascending: false)]
        var predicates: [NSPredicate] = [
            NSPredicate(format: "lastUpdated >= %@", Date(timeIntervalSinceNow: -maxAge) as NSDate)
        ]
        if !includeExpired {
            predicates.append(NSPredicate(format: "expiresAt >= %@", Date() as NSDate))
        }
        request.predicate = NSCompoundPredicate(andPredicateWithSubpredicates: predicates)

        do {
            return try context.fetch(request).compactMap(makeUserStory)
        } catch {
            print("[DeviceCache] Story fetch failed: \(error.localizedDescription)")
            CrashReportingService.shared.recordError(error, context: "DeviceCache story fetch")
            return []
        }
    }

    func cacheStories(_ stories: [UserStory]) {
        guard !stories.isEmpty else { return }
        let now = Date()
        for story in stories {
            let row = fetchOrCreateStory(id: story.id.uuidString)
            row.storyId = story.id.uuidString
            row.userId = story.userId.uuidString
            row.nickname = story.nickname
            row.displayName = story.displayName
            row.avatarUrl = story.avatarUrl
            row.mediaUrl = story.mediaUrl
            row.caption = story.caption
            row.venueId = story.venueId?.uuidString
            row.venueName = story.venueName
            row.likeCount = Int32(max(0, story.likeCount))
            row.commentCount = Int32(max(0, story.commentCount))
            row.hasLiked = story.hasLiked
            row.createdAt = story.createdAtUtc
            row.expiresAt = story.expiresAtUtc
            row.lastUpdated = now
        }
        dataController.saveIfNeeded()
    }

    func cachedEditableProfile(maxAge: TimeInterval = 7 * 24 * 60 * 60) -> EditableUserProfile? {
        cachedEditableProfile(userId: API.currentUserId, maxAge: maxAge)
    }

    func cachedEditableProfile(userId: UUID?, maxAge: TimeInterval = 7 * 24 * 60 * 60) -> EditableUserProfile? {
        guard let userId else { return nil }
        let request = UserProfileCache.fetchRequest()
        request.fetchLimit = 1
        request.predicate = NSPredicate(
            format: "userId == %@ AND lastUpdated >= %@",
            userId.uuidString,
            Date(timeIntervalSinceNow: -maxAge) as NSDate
        )
        do {
            return try context.fetch(request).first.flatMap(makeEditableProfile)
        } catch {
            print("[DeviceCache] Profile fetch failed: \(error.localizedDescription)")
            CrashReportingService.shared.recordError(error, context: "DeviceCache profile fetch")
            return nil
        }
    }

    func cacheProfile(_ profile: EditableUserProfile) {
        let row = fetchOrCreateProfile(id: profile.userId.uuidString)
        row.userId = profile.userId.uuidString
        row.nickname = profile.nickname
        row.displayName = profile.displayName
        row.avatarUrl = profile.avatarUrl
        row.discoverablePhone = profile.discoverablePhone
        row.discoverableEmail = profile.discoverableEmail
        row.bio = profile.bio
        row.birthYear = Int32(profile.birthYear ?? 0)
        row.gender = profile.gender
        row.interestsCsv = profile.interests.joined(separator: "\n")
        row.lastUpdated = Date()
        dataController.saveIfNeeded()
    }

    func cachedDirectMessages(otherUserId: UUID, maxAge: TimeInterval = 14 * 24 * 60 * 60) -> [DirectMessage] {
        cachedMessages(threadKey: threadKey(.direct, otherUserId.uuidString), maxAge: maxAge).compactMap(makeDirectMessage)
    }

    func cacheDirectThread(_ thread: DirectMessageThread, otherUserId: UUID) {
        cacheMessages(threadKey: threadKey(.direct, otherUserId.uuidString), messages: thread.messages.map(CachedMessagePayload.init))
    }

    func cachedGroupMessages(chatId: UUID, maxAge: TimeInterval = 14 * 24 * 60 * 60) -> [GroupChatMessage] {
        cachedMessages(threadKey: threadKey(.group, chatId.uuidString), maxAge: maxAge).compactMap(makeGroupMessage)
    }

    func cacheGroupThread(_ thread: GroupChatThread) {
        cacheMessages(threadKey: threadKey(.group, thread.chat.chatId.uuidString), messages: thread.messages.map(CachedMessagePayload.init))
    }

    func cachedVenueChatMessages(venueId: UUID, maxAge: TimeInterval = 14 * 24 * 60 * 60) -> [GroupChatMessage] {
        cachedMessages(threadKey: threadKey(.venue, venueId.uuidString), maxAge: maxAge).compactMap(makeGroupMessage)
    }

    func cacheVenueChatThread(_ thread: GroupChatThread, venueId: UUID) {
        cacheMessages(threadKey: threadKey(.venue, venueId.uuidString), messages: thread.messages.map(CachedMessagePayload.init))
    }

    func cachedTableMessages(tableId: UUID, maxAge: TimeInterval = 14 * 24 * 60 * 60) -> [SocialTableMessage] {
        cachedMessages(threadKey: threadKey(.table, tableId.uuidString), maxAge: maxAge).compactMap(makeTableMessage)
    }

    func cacheTableThread(_ thread: SocialTableThread, tableId: UUID) {
        cacheMessages(threadKey: threadKey(.table, tableId.uuidString), messages: thread.messages.map(CachedMessagePayload.init))
    }

    func queueDirectMessage(otherUserId: UUID, body: String) -> DirectMessage {
        makeDirectMessage(queueMessage(threadKey: threadKey(.direct, otherUserId.uuidString), body: body))
    }

    func queuedDirectMessages(otherUserId: UUID) -> [DirectMessage] {
        queuedMessages(threadKey: threadKey(.direct, otherUserId.uuidString)).map(makeDirectMessage)
    }

    func queueGroupMessage(chatId: UUID, body: String) -> GroupChatMessage {
        makeGroupMessage(queueMessage(threadKey: threadKey(.group, chatId.uuidString), body: body))
    }

    func queueGroupAttachment(chatId: UUID, data: Data, fileName: String, mimeType: String) async throws -> GroupChatMessage {
        let queued = try await queueAttachment(
            threadKey: threadKey(.group, chatId.uuidString),
            data: data,
            fileName: fileName,
            mimeType: mimeType
        )
        return makeGroupMessage(queued)
    }

    func queuedGroupMessages(chatId: UUID) -> [GroupChatMessage] {
        queuedMessages(threadKey: threadKey(.group, chatId.uuidString)).map(makeGroupMessage)
    }

    func queueVenueChatMessage(venueId: UUID, body: String) -> GroupChatMessage {
        makeGroupMessage(queueMessage(threadKey: threadKey(.venue, venueId.uuidString), body: body))
    }

    func queueVenueChatAttachment(venueId: UUID, data: Data, fileName: String, mimeType: String) async throws -> GroupChatMessage {
        let queued = try await queueAttachment(
            threadKey: threadKey(.venue, venueId.uuidString),
            data: data,
            fileName: fileName,
            mimeType: mimeType
        )
        return makeGroupMessage(queued)
    }

    func queuedVenueChatMessages(venueId: UUID) -> [GroupChatMessage] {
        queuedMessages(threadKey: threadKey(.venue, venueId.uuidString)).map(makeGroupMessage)
    }

    func queueTableMessage(tableId: UUID, body: String) -> SocialTableMessage {
        makeTableMessage(queueMessage(threadKey: threadKey(.table, tableId.uuidString), body: body))
    }

    func queuedTableMessages(tableId: UUID) -> [SocialTableMessage] {
        queuedMessages(threadKey: threadKey(.table, tableId.uuidString)).map(makeTableMessage)
    }

    private func queuedMessages(threadKey: String) -> [QueuedMessage] {
        let request = QueuedMessage.fetchRequest()
        request.sortDescriptors = [NSSortDescriptor(keyPath: \QueuedMessage.createdAt, ascending: true)]
        request.predicate = NSPredicate(format: "threadId == %@", threadKey)
        do {
            return try context.fetch(request)
        } catch {
            return []
        }
    }

    func retryQueuedDirectMessages(otherUserId: UUID) async {
        await retryQueuedMessages(threadKey: threadKey(.direct, otherUserId.uuidString)) { body in
            _ = try await API.sendDirectMessage(otherUserId: otherUserId, body: body)
        }
    }

    func retryQueuedGroupMessages(chatId: UUID) async {
        await retryQueuedMessages(threadKey: threadKey(.group, chatId.uuidString)) { body in
            _ = try await API.sendGroupChatMessage(chatId: chatId, body: body)
        }
    }

    func retryQueuedVenueChatMessages(venueId: UUID) async {
        await retryQueuedMessages(threadKey: threadKey(.venue, venueId.uuidString)) { body in
            _ = try await API.sendVenueChatMessage(venueId: venueId, body: body)
        }
    }

    func retryQueuedTableMessages(tableId: UUID) async {
        await retryQueuedMessages(threadKey: threadKey(.table, tableId.uuidString)) { body in
            _ = try await API.sendTableMessage(tableId: tableId, body: body)
        }
    }

    private func queueMessage(threadKey: String, body: String) -> QueuedMessage {
        let row = QueuedMessage(context: context)
        row.messageId = UUID().uuidString
        row.threadId = threadKey
        row.userId = API.currentUserId?.uuidString ?? "unknown"
        row.body = body
        row.createdAt = Date()
        row.attempts = 0
        dataController.saveIfNeeded()
        return row
    }

    private func queueAttachment(threadKey: String, data: Data, fileName: String, mimeType: String) async throws -> QueuedMessage {
        let localURL = try await MediaFileCache.shared.storePendingUpload(data: data, fileName: fileName)
        let row = queueMessage(
            threadKey: threadKey,
            body: pendingAttachmentBody(fileName: fileName, mimeType: mimeType, localURL: localURL)
        )
        row.fileName = fileName
        row.mimeType = mimeType
        row.localFilePath = localURL.path
        dataController.saveIfNeeded()
        return row
    }

    private func retryQueuedMessages(threadKey: String, sender: (String) async throws -> Void) async {
        let rows = queuedMessages(threadKey: threadKey)
        for row in rows {
            do {
                let body = try await sendableBody(for: row)
                try await sender(body)
                await MediaFileCache.shared.removePendingUpload(path: row.localFilePath)
                context.delete(row)
            } catch {
                row.attempts += 1
                break
            }
        }
        dataController.saveIfNeeded()
    }

    private func sendableBody(for row: QueuedMessage) async throws -> String {
        guard let localFilePath = row.localFilePath,
              let fileName = row.fileName,
              let mimeType = row.mimeType
        else {
            return row.body ?? ""
        }
        let data = try await MediaFileCache.shared.dataForPendingUpload(path: localFilePath)
        let remoteUrl = try await API.uploadChatFile(data: data, fileName: fileName, mimeType: mimeType)
        let marker = mimeType.hasPrefix("image/") ? "[image]" : "[file]"
        return "\(marker) \(fileName)\n\(remoteUrl)"
    }

    private func pendingAttachmentBody(fileName: String, mimeType: String, localURL: URL) -> String {
        let marker = mimeType.hasPrefix("image/") ? "[image]" : "[file]"
        return "\(marker) \(fileName)\n\(localURL.absoluteString)"
    }

    func cleanup(maxAge: TimeInterval = 7 * 24 * 60 * 60) {
        let cutoff = Date(timeIntervalSinceNow: -maxAge)
        let storyCutoff = Date(timeIntervalSinceNow: -120 * 24 * 60 * 60)
        delete(entityName: "VenueCache", olderThan: cutoff)
        delete(entityName: "StoryCache", olderThan: storyCutoff)
        delete(entityName: "MessageCache", olderThan: cutoff)
        delete(entityName: "UserProfileCache", olderThan: cutoff)
        dataController.saveIfNeeded()
    }

    func status() -> DeviceCacheStatus {
        DeviceCacheStatus(
            isStoreLoaded: dataController.isPersistentStoreLoaded,
            cachedVenues: count("VenueCache"),
            cachedStories: count("StoryCache"),
            cachedMessages: count("MessageCache"),
            queuedMessages: count("QueuedMessage"),
            cachedProfiles: count("UserProfileCache"),
            lastErrorDescription: dataController.lastLoadError?.localizedDescription
        )
    }

    private func fetchOrCreateVenue(id: String) -> VenueCache {
        let request = VenueCache.fetchRequest()
        request.fetchLimit = 1
        request.predicate = NSPredicate(format: "venueId == %@", id)
        if let existing = try? context.fetch(request).first {
            return existing
        }
        return VenueCache(context: context)
    }

    private func fetchOrCreateStory(id: String) -> StoryCache {
        let request = StoryCache.fetchRequest()
        request.fetchLimit = 1
        request.predicate = NSPredicate(format: "storyId == %@", id)
        if let existing = try? context.fetch(request).first {
            return existing
        }
        return StoryCache(context: context)
    }

    private func fetchOrCreateProfile(id: String) -> UserProfileCache {
        let request = UserProfileCache.fetchRequest()
        request.fetchLimit = 1
        request.predicate = NSPredicate(format: "userId == %@", id)
        if let existing = try? context.fetch(request).first {
            return existing
        }
        return UserProfileCache(context: context)
    }

    private func fetchOrCreateMessage(threadKey: String, messageId: String) -> MessageCache {
        let request = MessageCache.fetchRequest()
        request.fetchLimit = 1
        request.predicate = NSPredicate(format: "userId == %@ AND messageId == %@", threadKey, messageId)
        if let existing = try? context.fetch(request).first {
            return existing
        }
        return MessageCache(context: context)
    }

    private func makeUserStory(_ row: StoryCache) -> UserStory? {
        guard
            let rawStoryId = row.storyId,
            let storyId = UUID(uuidString: rawStoryId),
            let rawUserId = row.userId,
            let userId = UUID(uuidString: rawUserId),
            let nickname = row.nickname,
            let createdAt = row.createdAt,
            let expiresAt = row.expiresAt
        else { return nil }
        return UserStory(
            id: storyId,
            userId: userId,
            nickname: nickname,
            displayName: row.displayName,
            avatarUrl: row.avatarUrl,
            mediaUrl: row.mediaUrl,
            caption: row.caption,
            venueId: row.venueId.flatMap(UUID.init(uuidString:)),
            venueName: row.venueName,
            likeCount: Int(row.likeCount),
            commentCount: Int(row.commentCount),
            hasLiked: row.hasLiked,
            createdAtUtc: createdAt,
            expiresAtUtc: expiresAt
        )
    }

    private func makeEditableProfile(_ row: UserProfileCache) -> EditableUserProfile? {
        guard
            let rawUserId = row.userId,
            let userId = UUID(uuidString: rawUserId),
            let nickname = row.nickname
        else { return nil }
        return EditableUserProfile(
            userId: userId,
            nickname: nickname,
            displayName: row.displayName,
            avatarUrl: row.avatarUrl,
            discoverablePhone: row.discoverablePhone,
            discoverableEmail: row.discoverableEmail,
            bio: row.bio,
            birthYear: row.birthYear > 0 ? Int(row.birthYear) : nil,
            gender: row.gender ?? "unspecified",
            interests: row.interestsCsv?
                .split(separator: "\n")
                .map(String.init) ?? []
        )
    }

    private func makeVenueMarker(_ row: VenueCache) -> VenueMarker? {
        guard
            let rawId = row.venueId,
            let venueId = UUID(uuidString: rawId),
            let name = row.name
        else { return nil }

        let people = Int(row.peopleEstimate)
        let bubble = Int(row.bubbleIntensity)
        return VenueMarker(
            venueId: venueId,
            name: name,
            category: row.category ?? "locale",
            addressLine: row.addressLine ?? "",
            city: row.city ?? "",
            phoneNumber: row.phoneNumber,
            websiteUrl: row.websiteUrl,
            hoursSummary: row.hoursSummary,
            coverImageUrl: row.coverImageUrl,
            description: row.descriptionText,
            tags: [],
            latitude: row.latitude,
            longitude: row.longitude,
            isOpenNow: row.isOpenNow,
            peopleEstimate: people,
            densityLevel: row.densityLevel ?? "unknown",
            bubbleIntensity: bubble,
            demographicDataAvailable: false,
            activeCheckIns: 0,
            activeIntentions: 0,
            openTables: 0,
            partyPulse: PartyPulse(
                energyScore: min(100, max(0, bubble)),
                mood: people > 0 ? "cached" : "quiet",
                arrivalsLast15: 0,
                checkInsNow: people,
                intentionsSoon: 0,
                sparkline: []
            ),
            intentRadar: IntentRadar(
                goingOut: 0,
                almostThere: 0,
                hereNow: people,
                coolingDown: 0,
                updatedAtUtc: row.lastUpdated ?? Date(),
                privacyLevel: "cached"
            ),
            presencePreview: [],
            averageRating: row.averageRating,
            ratingCount: Int(row.ratingCount),
            myRating: nil
        )
    }

    private func delete(entityName: String, olderThan cutoff: Date) {
        let request = NSFetchRequest<NSFetchRequestResult>(entityName: entityName)
        request.predicate = NSPredicate(format: "lastUpdated < %@", cutoff as NSDate)

        if dataController.container.persistentStoreDescriptions.contains(where: { $0.type == NSInMemoryStoreType }) {
            let objectRequest = NSFetchRequest<NSManagedObject>(entityName: entityName)
            objectRequest.predicate = request.predicate
            do {
                for object in try context.fetch(objectRequest) {
                    context.delete(object)
                }
            } catch {
                print("[DeviceCache] Cleanup failed for \(entityName): \(error.localizedDescription)")
            }
            return
        }

        let deleteRequest = NSBatchDeleteRequest(fetchRequest: request)
        do {
            try context.execute(deleteRequest)
        } catch {
            print("[DeviceCache] Cleanup failed for \(entityName): \(error.localizedDescription)")
        }
    }

    private func count(_ entityName: String) -> Int {
        let request = NSFetchRequest<NSFetchRequestResult>(entityName: entityName)
        do {
            return try context.count(for: request)
        } catch {
            return 0
        }
    }

    private enum CachedThreadKind: String {
        case direct
        case group
        case venue
        case table
    }

    private func threadKey(_ kind: CachedThreadKind, _ id: String) -> String {
        "\(kind.rawValue):\(id.lowercased())"
    }

    private func cachedMessages(threadKey: String, maxAge: TimeInterval) -> [MessageCache] {
        let request = MessageCache.fetchRequest()
        request.fetchLimit = 260
        request.sortDescriptors = [NSSortDescriptor(keyPath: \MessageCache.sentAt, ascending: true)]
        request.predicate = NSPredicate(
            format: "userId == %@ AND lastUpdated >= %@",
            threadKey,
            Date(timeIntervalSinceNow: -maxAge) as NSDate
        )
        do {
            return try context.fetch(request)
        } catch {
            print("[DeviceCache] Message fetch failed: \(error.localizedDescription)")
            return []
        }
    }

    private func cacheMessages(threadKey: String, messages: [CachedMessagePayload]) {
        guard !messages.isEmpty else { return }
        let now = Date()
        for message in messages {
            let row = fetchOrCreateMessage(threadKey: threadKey, messageId: message.messageId.uuidString)
            row.userId = threadKey
            row.messageId = message.messageId.uuidString
            row.senderUserId = message.senderUserId.uuidString
            row.nickname = message.nickname
            row.displayName = message.displayName
            row.avatarUrl = message.avatarUrl
            row.body = message.body
            row.sentAt = message.sentAtUtc
            row.isMine = message.isMine
            row.lastUpdated = now
        }
        dataController.saveIfNeeded()
    }

    private func makeDirectMessage(_ row: MessageCache) -> DirectMessage? {
        guard
            let rawId = row.messageId,
            let id = UUID(uuidString: rawId),
            let rawSenderId = row.senderUserId,
            let senderId = UUID(uuidString: rawSenderId),
            let nickname = row.nickname,
            let body = row.body,
            let sentAt = row.sentAt
        else { return nil }
        return DirectMessage(
            messageId: id,
            senderUserId: senderId,
            nickname: nickname,
            displayName: row.displayName,
            avatarUrl: row.avatarUrl,
            body: body,
            sentAtUtc: sentAt,
            isMine: row.isMine
        )
    }

    private func makeDirectMessage(_ row: QueuedMessage) -> DirectMessage {
        let id = row.messageId.flatMap(UUID.init(uuidString:)) ?? UUID()
        return DirectMessage(
            messageId: id,
            senderUserId: API.currentUserId ?? id,
            nickname: "Tu",
            displayName: "Tu",
            avatarUrl: nil,
            body: row.body ?? "",
            sentAtUtc: row.createdAt ?? Date(),
            isMine: true
        )
    }

    private func makeGroupMessage(_ row: MessageCache) -> GroupChatMessage? {
        guard
            let rawId = row.messageId,
            let id = UUID(uuidString: rawId),
            let rawSenderId = row.senderUserId,
            let senderId = UUID(uuidString: rawSenderId),
            let nickname = row.nickname,
            let body = row.body,
            let sentAt = row.sentAt
        else { return nil }
        return GroupChatMessage(
            messageId: id,
            userId: senderId,
            nickname: nickname,
            displayName: row.displayName,
            avatarUrl: row.avatarUrl,
            body: body,
            sentAtUtc: sentAt,
            isMine: row.isMine
        )
    }

    private func makeGroupMessage(_ row: QueuedMessage) -> GroupChatMessage {
        let id = row.messageId.flatMap(UUID.init(uuidString:)) ?? UUID()
        return GroupChatMessage(
            messageId: id,
            userId: API.currentUserId ?? id,
            nickname: "Tu",
            displayName: "Tu",
            avatarUrl: nil,
            body: row.body ?? "",
            sentAtUtc: row.createdAt ?? Date(),
            isMine: true
        )
    }

    private func makeTableMessage(_ row: MessageCache) -> SocialTableMessage? {
        guard
            let rawId = row.messageId,
            let id = UUID(uuidString: rawId),
            let rawSenderId = row.senderUserId,
            let senderId = UUID(uuidString: rawSenderId),
            let nickname = row.nickname,
            let body = row.body,
            let sentAt = row.sentAt
        else { return nil }
        return SocialTableMessage(
            messageId: id,
            userId: senderId,
            nickname: nickname,
            displayName: row.displayName,
            avatarUrl: row.avatarUrl,
            body: body,
            sentAtUtc: sentAt,
            isMine: row.isMine
        )
    }

    private func makeTableMessage(_ row: QueuedMessage) -> SocialTableMessage {
        let id = row.messageId.flatMap(UUID.init(uuidString:)) ?? UUID()
        return SocialTableMessage(
            messageId: id,
            userId: API.currentUserId ?? id,
            nickname: "Tu",
            displayName: "Tu",
            avatarUrl: nil,
            body: row.body ?? "",
            sentAtUtc: row.createdAt ?? Date(),
            isMine: true
        )
    }
}

private struct CachedMessagePayload {
    let messageId: UUID
    let senderUserId: UUID
    let nickname: String
    let displayName: String?
    let avatarUrl: String?
    let body: String
    let sentAtUtc: Date
    let isMine: Bool

    init(_ message: DirectMessage) {
        messageId = message.messageId
        senderUserId = message.senderUserId
        nickname = message.nickname
        displayName = message.displayName
        avatarUrl = message.avatarUrl
        body = message.body
        sentAtUtc = message.sentAtUtc
        isMine = message.isMine
    }

    init(_ message: GroupChatMessage) {
        messageId = message.messageId
        senderUserId = message.userId
        nickname = message.nickname
        displayName = message.displayName
        avatarUrl = message.avatarUrl
        body = message.body
        sentAtUtc = message.sentAtUtc
        isMine = message.isMine
    }

    init(_ message: SocialTableMessage) {
        messageId = message.messageId
        senderUserId = message.userId
        nickname = message.nickname
        displayName = message.displayName
        avatarUrl = message.avatarUrl
        body = message.body
        sentAtUtc = message.sentAtUtc
        isMine = message.isMine
    }
}
