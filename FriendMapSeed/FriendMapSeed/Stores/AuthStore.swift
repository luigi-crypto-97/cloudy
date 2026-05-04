//
//  AuthStore.swift
//  Cloudy — Authentication state con refresh token
//
//  Gestisce:
//   - Token JWT in Keychain (sicuro)
//   - Refresh token per rinnovo automatico
//   - Backend URL in UserDefaults
//   - Restore sessione all'avvio
//

import Foundation
import Observation
import Security
import LocalAuthentication

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
            APIClient.shared.configure(baseURL: backendURL, token: token, refreshToken: refreshToken)
        }
    }
    var lastError: String?

    private(set) var token: String? {
        didSet {
            if let token { Keychain.save(token, for: Keys.token) }
            else { Keychain.delete(Keys.token) }
            APIClient.shared.configure(baseURL: backendURL, token: token, refreshToken: refreshToken)
        }
    }
    
    private(set) var refreshToken: String? {
        didSet {
            if let token = refreshToken { Keychain.save(token, for: Keys.refreshToken) }
            else { Keychain.delete(Keys.refreshToken) }
            APIClient.shared.configure(baseURL: backendURL, token: token, refreshToken: refreshToken)
        }
    }

    private enum Keys {
        static let backendURL = "cloudy.backendURL"
        static let token = "cloudy.jwt"
        static let refreshToken = "cloudy.refresh"
    }

    init() {
        let urlString = UserDefaults.standard.string(forKey: Keys.backendURL) ?? "https://api.iron-quote.it"
        self.backendURL = URL(string: urlString) ?? URL(string: "https://api.iron-quote.it")!
        self.token = Keychain.load(Keys.token)
        self.refreshToken = Keychain.load(Keys.refreshToken)
        APIClient.shared.configure(baseURL: backendURL, token: token, refreshToken: refreshToken)
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
        } catch APIError.unauthorized, APIError.tokenRefreshFailed {
            // Token invalido/scaduto e refresh fallito
            token = nil
            refreshToken = nil
            state = .loggedOut
        } catch {
            // Altro errore (network, server) - mantieni token e riprova dopo
            state = .loggedOut
        }
    }

    func devLogin(nickname: String, displayName: String?) async {
        lastError = nil
        do {
            let resp = try await API.devLogin(nickname: nickname, displayName: displayName)
            token = resp.accessToken
            // Il dev-login non restituisce refresh token, ma lo impostiamo per coerenza
            refreshToken = nil
            state = .loggedIn(resp.user)
        } catch {
            lastError = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    func login(nickname: String, displayName: String?) async -> Bool {
        lastError = nil
        do {
            let resp = try await API.devLogin(nickname: nickname, displayName: displayName)
            token = resp.accessToken
            // In produzione, il backend dovrebbe restituire anche refreshToken
            refreshToken = nil
            state = .loggedIn(resp.user)
            return true
        } catch {
            lastError = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
            return false
        }
    }

    func logout() {
        token = nil
        refreshToken = nil
        state = .loggedOut
    }
    
    /// Forza il logout con clear completo della sessione
    func forceLogout() {
        logout()
        // Clear UserDefaults
        UserDefaults.standard.removeObject(forKey: Keys.backendURL)
    }
}

// MARK: - Keychain Helper

private enum Keychain {
    static func save(_ value: String, for key: String) {
        guard let data = value.data(using: .utf8) else { return }
        delete(key)
        
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrAccount as String: key,
            kSecValueData as String: data,
            kSecAttrAccessible as String: kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly,
            kSecAttrSynchronizable as String: kCFBooleanTrue!
        ]
        
        let status = SecItemAdd(query as CFDictionary, nil)
        if status != errSecSuccess {
            print("[Keychain] Save error: \(status)")
        }
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
        
        guard status == errSecSuccess, let data = result as? Data else {
            return nil
        }
        
        return String(data: data, encoding: .utf8)
    }

    static func delete(_ key: String) {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrAccount as String: key
        ]
        SecItemDelete(query as CFDictionary)
    }
    
    static func contains(_ key: String) -> Bool {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrAccount as String: key
        ]
        return SecItemCopyMatching(query as CFDictionary, nil) == errSecSuccess
    }
}

// MARK: - Biometric Authentication Helper

extension AuthStore {
    /// Verifica se l'autenticazione biometrica è disponibile
    var isBiometricAuthAvailable: Bool {
        #if targetEnvironment(simulator)
        return false
        #else
        var error: NSError?
        let context = LAContext()
        return context.canEvaluatePolicy(.deviceOwnerAuthenticationWithBiometrics, error: &error)
        #endif
    }
    
    /// Autenticazione con biometria per riaprire l'app
    func authenticateWithBiometrics(reason: String = "Accedi a Cloudy") async throws -> Bool {
        let context = LAContext()
        context.localizedReason = reason
        
        return try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Bool, Error>) in
            context.evaluatePolicy(.deviceOwnerAuthenticationWithBiometrics, localizedReason: reason) { success, error in
                if let error {
                    continuation.resume(throwing: error)
                } else {
                    continuation.resume(returning: success)
                }
            }
        }
    }
}
