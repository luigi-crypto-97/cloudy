//
//  Endpoints+Chat.swift
//  Cloudy — Chat & Messaging API endpoints
//

import Foundation

extension API {
    
    // MARK: - Direct Messages (Chat 1:1)

    static func messageThreads() async throws -> [DirectMessageThreadSummary] {
        try await APIClient.shared.get("/api/messages/threads")
    }

    static func messageThread(otherUserId: UUID) async throws -> DirectMessageThread {
        try await APIClient.shared.get("/api/messages/threads/\(otherUserId.uuidString.lowercased())")
    }

    static func sendDirectMessage(otherUserId: UUID, body: String) async throws -> DirectMessage {
        let req = SendDirectMessageRequest(body: body)
        return try await APIClient.shared.post(
            "/api/messages/threads/\(otherUserId.uuidString.lowercased())",
            body: req
        )
    }

    static func deleteDirectMessageThread(otherUserId: UUID) async throws {
        try await APIClient.shared.delete("/api/messages/threads/\(otherUserId.uuidString.lowercased())")
    }

    static func uploadChatFile(data: Data, fileName: String, mimeType: String = "application/octet-stream") async throws -> String {
        let result: UploadMediaResult = try await APIClient.shared.upload(
            "/api/messages/files",
            data: data,
            fileName: fileName,
            mimeType: mimeType
        )
        return result.url
    }

    // MARK: - Group / Venue Chats

    static func groupChats() async throws -> [GroupChatSummary] {
        try await APIClient.shared.get("/api/messages/groups")
    }

    static func createGroupChat(title: String, memberUserIds: [UUID]) async throws -> GroupChatSummary {
        try await APIClient.shared.post(
            "/api/messages/groups",
            body: CreateGroupChatRequest(title: title, memberUserIds: memberUserIds)
        )
    }

    static func groupChatThread(chatId: UUID) async throws -> GroupChatThread {
        try await APIClient.shared.get("/api/messages/groups/\(chatId.uuidString.lowercased())")
    }

    static func sendGroupChatMessage(chatId: UUID, body: String) async throws -> GroupChatMessage {
        try await APIClient.shared.post(
            "/api/messages/groups/\(chatId.uuidString.lowercased())/messages",
            body: SendGroupChatMessageRequest(body: body)
        )
    }

    static func deleteGroupChat(chatId: UUID) async throws {
        try await APIClient.shared.delete("/api/messages/groups/\(chatId.uuidString.lowercased())")
    }

    static func venueChatThread(venueId: UUID) async throws -> GroupChatThread {
        try await APIClient.shared.get("/api/messages/venues/\(venueId.uuidString.lowercased())/chat")
    }

    static func sendVenueChatMessage(venueId: UUID, body: String) async throws -> GroupChatMessage {
        try await APIClient.shared.post(
            "/api/messages/venues/\(venueId.uuidString.lowercased())/chat/messages",
            body: SendGroupChatMessageRequest(body: body)
        )
    }
}
