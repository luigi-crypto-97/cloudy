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
@Observable
public final class SignalRService: SignalRServiceProtocol {
    private var hubConnection: HubConnection?
    private let baseURL: URL
    
    public var isConnected: Bool = false
    
    public init(baseURL: URL) {
        self.baseURL = baseURL
    }
    
    public func connect(threadId: UUID) async throws {
        // Appende il path esatto indicato dall'audit C# (ChatHub)
        let hubURL = baseURL.appendingPathComponent("chathub")
        
        hubConnection = HubConnectionBuilder(url: hubURL)
            .withAutoReconnect()
            .build()
        
        // Listener per i messaggi in ingresso inviati dal server
        hubConnection?.on(method: "ReceiveMessage") { (args: [Any]) in
            // Pubblica il messaggio a chiunque sia in ascolto (es. ChatRoomView)
            NotificationCenter.default.post(name: .init("NewChatMessage"), object: nil, userInfo: ["args": args])
        }
        
        hubConnection?.delegate = ConnectionDelegate(service: self)
        
        // Avvia la connessione in background
        hubConnection?.start()
        
        // Ci uniamo al gruppo del tavolo / thread
        hubConnection?.invoke(method: "JoinThread", threadId.uuidString) { error in
            if let error = error { print("Errore JoinThread: \(error)") }
        }
    }
    
    public func sendMessage(threadId: UUID, body: String) async throws {
        guard isConnected else {
            // Fallback gestito dal DataController: se siamo offline lanciamo errore e la View accoda il messaggio
            throw NSError(domain: "SignalRService", code: 1, userInfo: [NSLocalizedDescriptionKey: "Non connesso"])
        }
        
        hubConnection?.invoke(method: "SendMessage", threadId.uuidString, body) { error in
            if let error = error { print("Errore SendMessage: \(error)") }
        }
    }
    
    public func disconnect() {
        hubConnection?.stop()
        isConnected = false
    }
}

// MARK: - Delegate per lo stato della connessione
private class ConnectionDelegate: HubConnectionDelegate {
    weak var service: SignalRService?
    
    init(service: SignalRService) { self.service = service }
    
    func connectionDidOpen(hubConnection: HubConnection) {
        Task { @MainActor in self.service?.isConnected = true }
    }
    
    func connectionDidFailToOpen(error: Error) {
        Task { @MainActor in self.service?.isConnected = false }
    }
    
    func connectionDidClose(error: Error?) {
        Task { @MainActor in self.service?.isConnected = false }
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
