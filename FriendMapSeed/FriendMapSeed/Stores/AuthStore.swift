//
//  AuthStore.swift
//  Cloudy — Authentication state
//
//  Conserva sessione e backend URL. Persistenza:
//   - token JWT in Keychain (sicuro)
//   - backend URL in UserDefaults (non sensibile)
//

import Foundation
import Observation
import Security

@MainActor
@Observable
final class AuthStore {

    enum State: Equatable {
        case loading
        case loggedOut
        case loggedIn(AuthUser)
    }

    var state: State = .loading
    var backendURL: URL {
        didSet {
            UserDefaults.standard.set(backendURL.absoluteString, forKey: Keys.backendURL)
            APIClient.shared.configure(baseURL: backendURL, token: token)
        }
    }
    var lastError: String?

    private(set) var token: String? {
        didSet {
            if let token { Keychain.save(token, for: Keys.token) }
            else { Keychain.delete(Keys.token) }
            APIClient.shared.configure(baseURL: backendURL, token: token)
        }
    }

    private enum Keys {
        static let backendURL = "cloudy.backendURL"
        static let token = "cloudy.jwt"
    }

    init() {
        let urlString = UserDefaults.standard.string(forKey: Keys.backendURL) ?? "https://api.iron-quote.it"
        self.backendURL = URL(string: urlString) ?? URL(string: "https://api.iron-quote.it")!
        self.token = Keychain.load(Keys.token)
        APIClient.shared.configure(baseURL: backendURL, token: token)
    }

    /// Ricostruisce lo stato all'avvio: se ho un token, verifico con `/auth/me`.
    func restore() async {
        guard token != nil else {
            state = .loggedOut
            return
        }
        do {
            let user = try await API.me()
            state = .loggedIn(user)
        } catch {
            // token invalido / scaduto / backend irraggiungibile
            token = nil
            state = .loggedOut
        }
    }

    func devLogin(nickname: String, displayName: String?) async {
        lastError = nil
        do {
            let resp = try await API.devLogin(nickname: nickname, displayName: displayName)
            token = resp.accessToken
            state = .loggedIn(resp.user)
        } catch {
            lastError = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    func logout() {
        token = nil
        state = .loggedOut
    }
}

// MARK: - Keychain helper minimal

private enum Keychain {
    static func save(_ value: String, for key: String) {
        guard let data = value.data(using: .utf8) else { return }
        delete(key)
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrAccount as String: key,
            kSecValueData as String: data,
            kSecAttrAccessible as String: kSecAttrAccessibleAfterFirstUnlock
        ]
        SecItemAdd(query as CFDictionary, nil)
    }

    static func load(_ key: String) -> String? {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrAccount as String: key,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne
        ]
        var result: AnyObject?
        let status = SecItemCopyMatching(query as CFDictionary, &result)
        guard status == errSecSuccess, let data = result as? Data else { return nil }
        return String(data: data, encoding: .utf8)
    }

    static func delete(_ key: String) {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrAccount as String: key
        ]
        SecItemDelete(query as CFDictionary)
    }
}
