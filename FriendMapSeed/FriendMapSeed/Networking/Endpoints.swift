//
//  Endpoints.swift
//  Cloudy — API namespace
//
//  Enumeratore namespace per tutti gli endpoint API.
//  Le implementazioni sono suddivise nei file:
//  - Endpoints+Auth.swift
//  - Endpoints+Venue.swift
//  - Endpoints+Social.swift
//  - Endpoints+Feed.swift
//  - Endpoints+Chat.swift
//

import Foundation

/// Namespace per tutte le API del backend.
/// Usage: try await API.me()
enum API {
    
    // MARK: - Helper
    
    /// Ottieni l'ID utente corrente dal JWT token.
    static var currentUserId: UUID? {
        guard let token = APIClient.shared.bearerToken else { return nil }
        let parts = token.split(separator: ".")
        guard parts.count >= 2 else { return nil }
        var payload = String(parts[1])
            .replacingOccurrences(of: "-", with: "+")
            .replacingOccurrences(of: "_", with: "/")
        while payload.count % 4 != 0 { payload.append("=") }
        guard
            let data = Data(base64Encoded: payload),
            let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any]
        else { return nil }

        let id = json["sub"] as? String
            ?? json["nameid"] as? String
            ?? json["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"] as? String
        return id.flatMap(UUID.init(uuidString:))
    }
}
