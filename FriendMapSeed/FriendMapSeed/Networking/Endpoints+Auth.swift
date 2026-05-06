//
//  Endpoints+Auth.swift
//  Cloudy — Auth API endpoints
//

import Foundation

extension API {
    
    // MARK: - Auth
    
    static func devLogin(nickname: String, displayName: String?) async throws -> AuthTokenResponse {
        let req = DevLoginRequest(nickname: nickname, displayName: displayName)
        return try await APIClient.shared.post("/api/auth/dev-login", body: req)
    }

    static func appleLogin(identityToken: String, authorizationCode: String?, fullName: String?) async throws -> AuthTokenResponse {
        let req = AppleLoginRequest(
            identityToken: identityToken,
            authorizationCode: authorizationCode,
            fullName: fullName
        )
        return try await APIClient.shared.post("/api/auth/apple", body: req)
    }

    static func me() async throws -> AuthUser {
        try await APIClient.shared.get("/api/auth/me")
    }
    
    static func refreshToken(_ refreshToken: String) async throws -> RefreshTokenResponse {
        let req = RefreshTokenRequest(refreshToken: refreshToken)
        return try await APIClient.shared.post("/api/auth/refresh", body: req)
    }
}

// MARK: - Refresh Token Request

private struct RefreshTokenRequest: Codable {
    let refreshToken: String
}
