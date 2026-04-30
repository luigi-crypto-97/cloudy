//
//  APIClient.swift
//  Cloudy — HTTP client async/await
//
//  Wrapper minimale su URLSession. Gestisce:
//   - base URL configurabile (per dev simulatore vs LAN device)
//   - JWT bearer token salvato in Keychain (via AuthStore)
//   - encoder/decoder camelCase + ISO8601 con frazioni
//   - errori tipizzati
//

import Foundation

// MARK: - Errors

enum APIError: LocalizedError {
    case invalidURL
    case requestFailed(status: Int, body: String?)
    case decodingFailed(Error)
    case transport(Error)
    case unauthorized
    case noConnection

    var errorDescription: String? {
        switch self {
        case .invalidURL:                       return "URL backend non valido."
        case .requestFailed(let s, let body):   return "Errore server (\(s)). \(body ?? "")"
        case .decodingFailed(let e):            return "Risposta non valida: \(e.localizedDescription)"
        case .transport(let e):                 return e.localizedDescription
        case .unauthorized:                     return "Sessione scaduta. Effettua di nuovo il login."
        case .noConnection:                     return "Nessuna connessione al backend."
        }
    }
}

// MARK: - APIClient

@MainActor
final class APIClient {
    static let shared = APIClient()

    var baseURL: URL
    var bearerToken: String?

    private let session: URLSession
    private let decoder: JSONDecoder
    private let encoder: JSONEncoder

    private init() {
        // Default produzione. Sovrascrivibile da login screen (es. backend dev locale).
        self.baseURL = URL(string: "https://api.iron-quote.it")!

        let cfg = URLSessionConfiguration.default
        cfg.timeoutIntervalForRequest = 30
        cfg.timeoutIntervalForResource = 180
        cfg.waitsForConnectivity = false
        self.session = URLSession(configuration: cfg)

        let dec = JSONDecoder()
        dec.keyDecodingStrategy = .convertFromPascalCase
        dec.dateDecodingStrategy = .iso8601WithFractional
        self.decoder = dec

        let enc = JSONEncoder()
        enc.keyEncodingStrategy = .convertToPascalCase
        enc.dateEncodingStrategy = .iso8601
        self.encoder = enc
    }

    // MARK: - Public configure

    func configure(baseURL: URL, token: String?) {
        self.baseURL = baseURL
        self.bearerToken = token
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

    // MARK: - Verbs

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
    func put<B: Encodable, R: Decodable>(
        _ path: String,
        body: B
    ) async throws -> R {
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

        let responseData: Data
        let response: URLResponse
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

    // MARK: - Core

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

        let data: Data
        let response: URLResponse
        do {
            (data, response) = try await session.data(for: req)
        } catch {
            // network errors (offline, dns)
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

        switch http.statusCode {
        case 200...299:
            do {
                return try decodeResponse(R.self, from: data)
            } catch {
                throw APIError.decodingFailed(error)
            }
        case 401:
            throw APIError.unauthorized
        default:
            let bodyStr = String(data: data, encoding: .utf8)
            throw APIError.requestFailed(status: http.statusCode, body: bodyStr)
        }
    }

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
}

private struct EmptyBody: Encodable {}

/// Tipo restituito quando il caller non si aspetta un body (es. DELETE, 204).
struct EmptyResponse: Decodable {}

private extension Data {
    mutating func append(_ string: String) {
        append(Data(string.utf8))
    }
}

// MARK: - Codable helpers

extension JSONDecoder.KeyDecodingStrategy {
    /// Backend C# emette PascalCase (System.Text.Json default). Convertiamo a camelCase
    /// affinché coincida con i nomi delle property Swift.
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

// MARK: - Date strategy: ISO8601 with optional fractional seconds

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
