//
//  APIClient.swift
//  Cloudy — HTTP client async/await con certificate pinning e token refresh
//
//  Wrapper su URLSession con:
//   - Base URL configurabile (dev/prod)
//   - JWT Bearer token con refresh automatico
//   - Certificate pinning per sicurezza
//   - Encoder/decoder PascalCase ↔ camelCase + ISO8601
//   - Errori localizzati
//   - Logging opzionale per debug
//

import Foundation
import CryptoKit

// MARK: - Errors

enum APIError: LocalizedError {
    case invalidURL
    case requestFailed(status: Int, body: String?)
    case decodingFailed(Error)
    case transport(Error)
    case unauthorized
    case noConnection
    case certificatePinningFailed
    case tokenRefreshFailed
    case serverError(status: Int)

    var errorDescription: String? {
        switch self {
        case .invalidURL:
            return L10n.Error.generic
        case .requestFailed(let status, let body):
            if status == 429 {
                return "Troppe richieste. Attendi un momento."
            }
            return "Errore server (\(status)). \(body ?? "")"
        case .decodingFailed(let error):
            return "Risposta non valida: \(error.localizedDescription)"
        case .transport(let error):
            return error.localizedDescription
        case .unauthorized:
            return L10n.Error.unauthorized
        case .noConnection:
            return L10n.Error.network
        case .certificatePinningFailed:
            return "Errore di sicurezza. Connessione non sicura."
        case .tokenRefreshFailed:
            return "Sessione scaduta. Effettua di nuovo il login."
        case .serverError(let status):
            switch status {
            case 500: return L10n.Error.server
            case 502, 503, 504: return "Servizio temporaneamente non disponibile."
            default: return "Errore server (\(status))."
            }
        }
    }
}

// MARK: - Certificate Pinning

final class CertificatePinningDelegate: NSObject, URLSessionDelegate {
    
    // Hash dei certificati pinati (SHA256 del public key in base64)
    // Per produzione: genera con `openssl x509 -pubkey -noout | openssl pkey -pubin -outform der | openssl dgst -sha256 -binary | openssl enc -base64`
    private static let pinnedPublicKeyHashes: Set<String> = [
        // Aggiungi qui gli hash dei certificati di produzione
        // Esempio: "Base64HashOfProductionCertificate="
    ]
    
    // Per development, accetta tutti i certificati se DEBUG è definito
    #if DEBUG
    private let allowSelfSignedInDebug = true
    #else
    private let allowSelfSignedInDebug = false
    #endif
    
    func urlSession(
        _ session: URLSession,
        didReceive challenge: URLAuthenticationChallenge,
        completionHandler: @escaping (URLSession.AuthChallengeDisposition, URLCredential?) -> Void
    ) {
        guard challenge.protectionSpace.authenticationMethod == NSURLAuthenticationMethodServerTrust,
              let serverTrust = challenge.protectionSpace.serverTrust else {
            completionHandler(.performDefaultHandling, nil)
            return
        }
        
        // In debug, permetti self-signed certificates per localhost
        #if DEBUG
        if allowSelfSignedInDebug {
            let host = challenge.protectionSpace.host.lowercased()
            if host == "localhost" || host == "127.0.0.1" || host.starts(with: "192.168.") {
                let credential = URLCredential(trust: serverTrust)
                completionHandler(.useCredential, credential)
                return
            }
        }
        #endif
        
        // Verifica certificate pinning per produzione
        guard verifyCertificatePinning(trust: serverTrust, host: challenge.protectionSpace.host) else {
            completionHandler(.cancelAuthenticationChallenge, nil)
            return
        }
        
        let credential = URLCredential(trust: serverTrust)
        completionHandler(.useCredential, credential)
    }
    
    private func verifyCertificatePinning(trust: SecTrust, host: String) -> Bool {
        // Se non ci sono pin configurati, usa la validazione standard
        guard !Self.pinnedPublicKeyHashes.isEmpty else {
            // Validazione standard della chain
            let policy = SecPolicyCreateSSL(true, host as CFString)
            SecTrustSetPolicies(trust, policy)
            
            var error: CFError?
            let isValid = SecTrustEvaluateWithError(trust, &error)
            return isValid
        }
        
        // Verifica gli hash dei public keys
        var publicKeyHashes: Set<String> = []

        // Usa API moderna (iOS 15+) o fallback per versioni precedenti
        if #available(iOS 15.0, *) {
            // API moderna: SecTrustCopyCertificateChain
            guard let certChain = SecTrustCopyCertificateChain(trust) as? [SecCertificate] else {
                return false
            }
            for cert in certChain {
                guard let publicKey = SecCertificateCopyKey(cert) else { continue }
                guard let publicKeyData = SecKeyCopyExternalRepresentation(publicKey, nil) as Data? else { continue }
                let hash = sha256(data: publicKeyData)
                let hashBase64 = hash.base64EncodedString()
                publicKeyHashes.insert(hashBase64)
            }
        } else {
            // Fallback per iOS < 15
            for i in 0..<SecTrustGetCertificateCount(trust) {
                guard let cert = SecTrustGetCertificateAtIndex(trust, i) else { continue }
                guard let publicKey = SecCertificateCopyKey(cert) else { continue }
                guard let publicKeyData = SecKeyCopyExternalRepresentation(publicKey, nil) as Data? else { continue }
                let hash = sha256(data: publicKeyData)
                let hashBase64 = hash.base64EncodedString()
                publicKeyHashes.insert(hashBase64)
            }
        }

        // Verifica se almeno un hash corrisponde
        let isPinned = !publicKeyHashes.isDisjoint(with: Self.pinnedPublicKeyHashes)

        if !isPinned {
            print("[CertificatePinning] FAILED for \(host). Found hashes: \(publicKeyHashes)")
        }

        return isPinned
    }

    private func sha256(data: Data) -> Data {
        let hash = SHA256.hash(data: data)
        return Data(hash)
    }
}

// MARK: - APIClient

@MainActor
final class APIClient {
    static let shared = APIClient()
    
    var baseURL: URL
    var bearerToken: String?
    var refreshToken: String?
    
    private let session: URLSession
    private let decoder: JSONDecoder
    private let encoder: JSONEncoder
    private var isRefreshingToken = false
    private var tokenRefreshHandlers: [(Result<(String, String?), Error>) -> Void] = []
    
    // Logging per debug
    private var shouldLogRequests: Bool {
        #if DEBUG
        return ProcessInfo.processInfo.environment["LOG_NETWORK_REQUESTS"] == "1"
        #else
        return false
        #endif
    }
    
    private init() {
        // Default produzione da xcconfig
        let defaultURL = ProcessInfo.processInfo.environment["API_BASE_URL"] ?? "https://api.iron-quote.it"
        self.baseURL = URL(string: defaultURL)!
        
        let cfg = URLSessionConfiguration.default
        cfg.timeoutIntervalForRequest = 30
        cfg.timeoutIntervalForResource = 180
        cfg.waitsForConnectivity = false
        cfg.httpAdditionalHeaders = ["User-Agent": "Cloudy-iOS/1.0"]
        
        let delegate = CertificatePinningDelegate()
        self.session = URLSession(configuration: cfg, delegate: delegate, delegateQueue: nil)
        
        let dec = JSONDecoder()
        dec.keyDecodingStrategy = .convertFromPascalCase
        dec.dateDecodingStrategy = .iso8601WithFractional
        self.decoder = dec
        
        let enc = JSONEncoder()
        enc.keyEncodingStrategy = .convertToPascalCase
        enc.dateEncodingStrategy = .iso8601
        self.encoder = enc
    }
    
    // MARK: - Public Configure
    
    func configure(baseURL: URL, token: String?, refreshToken: String? = nil) {
        guard Self.isTransportAllowed(for: baseURL) else {
            print("[Security] Rejected insecure API base URL: \(baseURL.absoluteString)")
            return
        }
        self.baseURL = baseURL
        self.bearerToken = token
        self.refreshToken = refreshToken
    }
    
    func setTokens(accessToken: String, refreshToken: String?) {
        self.bearerToken = accessToken
        self.refreshToken = refreshToken
    }
    
    func mediaURL(from rawValue: String?) -> URL? {
        guard let rawValue else { return nil }
        let trimmed = rawValue.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }
        
        if let absolute = URL(string: trimmed), absolute.scheme != nil {
            guard let host = absolute.host?.lowercased(), host == "localhost" || host == "127.0.0.1" else {
                return absolute
            }
            
            var components = URLComponents(url: baseURL, resolvingAgainstBaseURL: false)
            components?.path = absolute.path
            components?.query = absolute.query
            return components?.url
        }
        
        let relativePath = trimmed.hasPrefix("/") ? String(trimmed.dropFirst()) : trimmed
        return baseURL.appendingPathComponent(relativePath)
    }
    
    // MARK: - HTTP Verbs
    
    func get<R: Decodable>(_ path: String, query: [String: String?] = [:]) async throws -> R {
        try await send(method: "GET", path: path, query: query, body: Optional<EmptyBody>.none)
    }
    
    @discardableResult
    func post<B: Encodable, R: Decodable>(
        _ path: String,
        body: B,
        query: [String: String?] = [:]
    ) async throws -> R {
        try await send(method: "POST", path: path, query: query, body: body)
    }
    
    @discardableResult
    func post<R: Decodable>(_ path: String, query: [String: String?] = [:]) async throws -> R {
        try await send(method: "POST", path: path, query: query, body: Optional<EmptyBody>.none)
    }
    
    @discardableResult
    func put<B: Encodable, R: Decodable>(_ path: String, body: B) async throws -> R {
        try await send(method: "PUT", path: path, query: [:], body: body)
    }
    
    func delete(_ path: String) async throws {
        let _: EmptyResponse = try await send(method: "DELETE", path: path, query: [:], body: Optional<EmptyBody>.none)
    }
    
    func deleteWithResponse<R: Decodable>(_ path: String) async throws -> R {
        try await send(method: "DELETE", path: path, query: [:], body: Optional<EmptyBody>.none)
    }
    
    func upload<R: Decodable>(
        _ path: String,
        data: Data,
        fileName: String,
        mimeType: String = "image/jpeg",
        fieldName: String = "file"
    ) async throws -> R {
        guard let url = URLComponents(url: baseURL.appendingPathComponent(path), resolvingAgainstBaseURL: false)?.url else {
            throw APIError.invalidURL
        }
        guard Self.isTransportAllowed(for: url) else {
            throw APIError.certificatePinningFailed
        }
        
        let boundary = "Boundary-\(UUID().uuidString)"
        var req = URLRequest(url: url)
        req.httpMethod = "POST"
        req.timeoutInterval = data.count > 12 * 1024 * 1024 ? 180 : 45
        req.setValue("application/json", forHTTPHeaderField: "Accept")
        req.setValue("multipart/form-data; boundary=\(boundary)", forHTTPHeaderField: "Content-Type")
        if let token = bearerToken, !token.isEmpty {
            req.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        }
        
        var body = Data()
        body.append("--\(boundary)\r\n")
        body.append("Content-Disposition: form-data; name=\"\(fieldName)\"; filename=\"\(fileName)\"\r\n")
        body.append("Content-Type: \(mimeType)\r\n\r\n")
        body.append(data)
        body.append("\r\n--\(boundary)--\r\n")
        req.httpBody = body
        
        if shouldLogRequests {
            print("[HTTP] POST \(path) (upload: \(fileName), \(data.count) bytes)")
        }
        
        let (responseData, response): (Data, URLResponse)
        do {
            (responseData, response) = try await session.data(for: req)
        } catch {
            throw APIError.transport(error)
        }
        
        guard let http = response as? HTTPURLResponse else {
            throw APIError.requestFailed(status: -1, body: nil)
        }
        guard (200...299).contains(http.statusCode) else {
            throw APIError.requestFailed(status: http.statusCode, body: String(data: responseData, encoding: .utf8))
        }
        
        do {
            return try decodeResponse(R.self, from: responseData)
        } catch {
            throw APIError.decodingFailed(error)
        }
    }
    
    // MARK: - Core Send
    
    private func send<B: Encodable, R: Decodable>(
        method: String,
        path: String,
        query: [String: String?],
        body: B?
    ) async throws -> R {
        var components = URLComponents(url: baseURL.appendingPathComponent(path), resolvingAgainstBaseURL: false)
        let items = query.compactMap { (k, v) -> URLQueryItem? in
            guard let v else { return nil }
            return URLQueryItem(name: k, value: v)
        }
        if !items.isEmpty {
            components?.queryItems = items
        }
        guard let url = components?.url else { throw APIError.invalidURL }
        guard Self.isTransportAllowed(for: url) else {
            throw APIError.certificatePinningFailed
        }
        
        var req = URLRequest(url: url)
        req.httpMethod = method
        req.setValue("application/json", forHTTPHeaderField: "Accept")
        
        if let token = bearerToken, !token.isEmpty {
            req.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        }
        if let body, !(body is EmptyBody) {
            req.setValue("application/json", forHTTPHeaderField: "Content-Type")
            req.httpBody = try encoder.encode(body)
        }
        
        if shouldLogRequests {
            let bodyStr = body.flatMap { try? encoder.encode($0) }.flatMap { String(data: $0, encoding: .utf8) }
            print("[HTTP] \(method) \(path)")
            if let bodyStr { print("[Request] \(bodyStr)") }
        }
        
        let (data, response): (Data, URLResponse)
        do {
            (data, response) = try await session.data(for: req)
        } catch {
            let ns = error as NSError
            if ns.code == NSURLErrorNotConnectedToInternet ||
               ns.code == NSURLErrorCannotFindHost ||
               ns.code == NSURLErrorCannotConnectToHost ||
               ns.code == NSURLErrorTimedOut {
                throw APIError.noConnection
            }
            throw APIError.transport(error)
        }
        
        guard let http = response as? HTTPURLResponse else {
            throw APIError.requestFailed(status: -1, body: nil)
        }
        
        if shouldLogRequests {
            let responseStr = String(data: data, encoding: .utf8) ?? "(binary)"
            print("[HTTP Response] \(http.statusCode): \(responseStr.prefix(500))")
        }
        
        switch http.statusCode {
        case 200...299:
            do {
                return try decodeResponse(R.self, from: data)
            } catch {
                throw APIError.decodingFailed(error)
            }
        case 401:
            // Token scaduto - prova refresh
            if !isRefreshingToken, refreshToken != nil {
                do {
                    let (newToken, newRefreshToken) = try await refreshAuthToken()
                    APIClient.shared.setTokens(accessToken: newToken, refreshToken: newRefreshToken)
                    
                    // Retry della richiesta originale
                    return try await send(method: method, path: path, query: query, body: body)
                } catch {
                    throw APIError.tokenRefreshFailed
                }
            } else if isRefreshingToken {
                // Attendi che un altro refresh completi
                return try await waitForTokenRefreshAndRetry(method: method, path: path, query: query, body: body)
            }
            throw APIError.unauthorized
        case 400...499:
            let bodyStr = String(data: data, encoding: .utf8)
            throw APIError.requestFailed(status: http.statusCode, body: bodyStr)
        case 500...599:
            throw APIError.serverError(status: http.statusCode)
        default:
            let bodyStr = String(data: data, encoding: .utf8)
            throw APIError.requestFailed(status: http.statusCode, body: bodyStr)
        }
    }
    
    // MARK: - Token Refresh
    
    private func refreshAuthToken() async throws -> (accessToken: String, refreshToken: String?) {
        isRefreshingToken = true
        defer { isRefreshingToken = false }
        
        guard let refreshToken = refreshToken else {
            throw APIError.tokenRefreshFailed
        }
        
        do {
            // Chiama endpoint di refresh del backend
            // Nota: il backend deve implementare POST /api/auth/refresh
            let response: RefreshTokenResponse = try await post("/api/auth/refresh", body: RefreshTokenRequest(refreshToken: refreshToken))
            
            let (newAccess, newRefresh) = (response.accessToken, response.refreshToken)
            
            // Notifica tutti i handler in attesa
            tokenRefreshHandlers.forEach { $0(.success((newAccess, newRefresh))) }
            tokenRefreshHandlers.removeAll()
            
            return (newAccess, newRefresh)
        } catch {
            tokenRefreshHandlers.forEach { $0(.failure(error)) }
            tokenRefreshHandlers.removeAll()
            throw error
        }
    }
    
    private func waitForTokenRefreshAndRetry<B: Encodable, R: Decodable>(
        method: String,
        path: String,
        query: [String: String?],
        body: B?
    ) async throws -> R {
        return try await withCheckedThrowingContinuation { continuation in
            tokenRefreshHandlers.append { result in
                switch result {
                case .success((let newToken, _)):
                    self.bearerToken = newToken
                    Task {
                        do {
                            let result: R = try await self.send(method: method, path: path, query: query, body: body)
                            continuation.resume(returning: result)
                        } catch {
                            continuation.resume(throwing: error)
                        }
                    }
                case .failure(let error):
                    continuation.resume(throwing: error)
                }
            }
        }
    }
    
    // MARK: - Decode Helpers
    
    private func decodeResponse<R: Decodable>(_ type: R.Type, from data: Data) throws -> R {
        if R.self == EmptyResponse.self {
            return EmptyResponse() as! R
        }
        if R.self == IgnoredResponse.self {
            return IgnoredResponse() as! R
        }
        if data.isEmpty {
            throw APIError.decodingFailed(NSError(domain: "Cloudy", code: 0, userInfo: [NSLocalizedDescriptionKey: "Empty body"]))
        }
        return try decoder.decode(R.self, from: data)
    }

    private static func isTransportAllowed(for url: URL) -> Bool {
        guard let scheme = url.scheme?.lowercased() else { return false }
        if scheme == "https" { return true }

        #if DEBUG
        guard scheme == "http", let host = url.host?.lowercased() else { return false }
        return host == "localhost" ||
            host == "127.0.0.1" ||
            host.starts(with: "192.168.") ||
            host.starts(with: "10.") ||
            host.range(of: #"^172\.(1[6-9]|2[0-9]|3[0-1])\."#, options: .regularExpression) != nil
        #else
        return false
        #endif
    }
}

// MARK: - Refresh Token Models

private struct RefreshTokenRequest: Codable {
    let refreshToken: String
}

// RefreshTokenResponse è definito in Models+Auth.swift

// MARK: - Helpers

private struct EmptyBody: Encodable {}

// EmptyResponse è definito in Models+Feed.swift
// IgnoredResponse è definito in Models+Feed.swift

private extension Data {
    mutating func append(_ string: String) {
        append(Data(string.utf8))
    }
}

// MARK: - Codable Extensions

extension JSONDecoder.KeyDecodingStrategy {
    static var convertFromPascalCase: JSONDecoder.KeyDecodingStrategy {
        .custom { keys in
            let key = keys.last!.stringValue
            guard let first = key.first else { return keys.last! }
            return PascalCaseKey(stringValue: first.lowercased() + key.dropFirst())
        }
    }
}

extension JSONEncoder.KeyEncodingStrategy {
    static var convertToPascalCase: JSONEncoder.KeyEncodingStrategy {
        .custom { keys in
            let key = keys.last!.stringValue
            guard let first = key.first else { return keys.last! }
            return PascalCaseKey(stringValue: first.uppercased() + key.dropFirst())
        }
    }
}

private struct PascalCaseKey: CodingKey {
    var stringValue: String
    var intValue: Int? { nil }
    init(stringValue: String) { self.stringValue = stringValue }
    init?(intValue: Int) { return nil }
}

extension JSONDecoder.DateDecodingStrategy {
    static let iso8601WithFractional: JSONDecoder.DateDecodingStrategy = .custom { decoder in
        let container = try decoder.singleValueContainer()
        let str = try container.decode(String.self)
        
        let withFrac = ISO8601DateFormatter()
        withFrac.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        if let d = withFrac.date(from: str) { return d }
        
        let plain = ISO8601DateFormatter()
        plain.formatOptions = [.withInternetDateTime]
        if let d = plain.date(from: str) { return d }
        
        throw DecodingError.dataCorruptedError(in: container, debugDescription: "Bad date \(str)")
    }
}
