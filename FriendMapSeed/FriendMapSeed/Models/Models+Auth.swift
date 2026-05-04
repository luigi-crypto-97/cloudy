//
//  Models+Auth.swift
//  Cloudy — Auth models
//
//  Modelli per autenticazione e gestione sessione.
//

import Foundation

// MARK: - Auth User

struct AuthUser: Codable, Hashable, Identifiable {
    let userId: UUID
    let nickname: String
    let displayName: String?

    var id: UUID { userId }
}

// MARK: - Auth Token Response

struct AuthTokenResponse: Codable {
    let accessToken: String
    let expiresAtUtc: Date
    let user: AuthUser
}

// MARK: - Dev Login Request

struct DevLoginRequest: Codable {
    let nickname: String
    let displayName: String?
}

// MARK: - Upload Media Result

struct UploadMediaResult: Codable {
    let url: String
}

// MARK: - Refresh Token

struct RefreshTokenResponse: Codable {
    let accessToken: String
    let refreshToken: String?
    let expiresAtUtc: Date
}
