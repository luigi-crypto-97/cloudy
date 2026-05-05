import Foundation

#if canImport(SignalRClient)
import SignalRClient
#endif

public protocol SignalRServiceProtocol {
    func connect(threadId: UUID) async throws
    func sendMessage(threadId: UUID, body: String) async throws
    func disconnect()
}

#if canImport(SignalRClient)
private struct SignalRIncomingMessage: Decodable {
    let threadId: String
    let senderId: String?
    let body: String
    let sentAt: String?

    private enum CodingKeys: String, CodingKey {
        case threadId
        case senderId
        case body
        case sentAt
        case pascalThreadId = "ThreadId"
        case pascalSenderId = "SenderId"
        case pascalBody = "Body"
        case pascalSentAt = "SentAt"
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        threadId = try container.decodeIfPresent(String.self, forKey: .threadId)
            ?? container.decodeIfPresent(String.self, forKey: .pascalThreadId)
            ?? ""
        senderId = try container.decodeIfPresent(String.self, forKey: .senderId)
            ?? container.decodeIfPresent(String.self, forKey: .pascalSenderId)
        body = try container.decodeIfPresent(String.self, forKey: .body)
            ?? container.decodeIfPresent(String.self, forKey: .pascalBody)
            ?? ""
        sentAt = try container.decodeIfPresent(String.self, forKey: .sentAt)
            ?? container.decodeIfPresent(String.self, forKey: .pascalSentAt)
    }
}

@Observable
public final class SignalRService: SignalRServiceProtocol {
    private var hubConnection: HubConnection?
    private let baseURL: URL
    
    public var isConnected: Bool = false
    
    public init(baseURL: URL) {
        self.baseURL = baseURL
    }
    
    public func connect(threadId: UUID) async throws {
        let hubURL = baseURL.appendingPathComponent("chathub")
        var options = HttpConnectionOptions()
        options.accessTokenFactory = {
            await MainActor.run { APIClient.shared.bearerToken }
        }

        let connection = HubConnectionBuilder()
            .withUrl(url: hubURL.absoluteString, options: options)
            .withAutomaticReconnect()
            .build()

        await connection.on("ReceiveMessage") { (message: SignalRIncomingMessage) in
            NotificationCenter.default.post(
                name: .init("NewChatMessage"),
                object: nil,
                userInfo: [
                    "threadId": message.threadId,
                    "senderId": message.senderId ?? "",
                    "body": message.body,
                    "sentAt": message.sentAt ?? ""
                ]
            )
        }

        hubConnection = connection
        try await connection.start()
        isConnected = true
        try await connection.invoke(method: "JoinThread", arguments: threadId.uuidString)
    }
    
    public func sendMessage(threadId: UUID, body: String) async throws {
        guard isConnected else {
            // Fallback gestito dal DataController: se siamo offline lanciamo errore e la View accoda il messaggio
            throw NSError(domain: "SignalRService", code: 1, userInfo: [NSLocalizedDescriptionKey: "Non connesso"])
        }
        
        try await hubConnection?.invoke(method: "SendMessage", arguments: threadId.uuidString, body)
    }
    
    public func disconnect() {
        let connection = hubConnection
        Task {
            await connection?.stop()
        }
        isConnected = false
    }
}
#else
@Observable
public final class SignalRService: SignalRServiceProtocol {
    public var isConnected: Bool = false

    public init(baseURL _: URL) {}

    public func connect(threadId _: UUID) async throws {
        isConnected = false
    }

    public func sendMessage(threadId _: UUID, body _: String) async throws {
        throw NSError(
            domain: "SignalRService",
            code: 1,
            userInfo: [NSLocalizedDescriptionKey: "SignalRClient non installato"]
        )
    }

    public func disconnect() {
        isConnected = false
    }
}
#endif
