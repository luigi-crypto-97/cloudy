//
//  LiveLocationStore.swift
//  Cloudy — Posizione live privacy-friendly
//

import CoreLocation
import Foundation
import Observation

@MainActor
@Observable
final class LiveLocationStore: NSObject {
    enum State: Equatable {
        case off
        case requestingPermission
        case active
        case denied
        case failed(String)
    }

    var state: State = .off
    var currentLocation: CLLocation?
    var lastVenueName: String?
    var lastUpdateAt: Date?

    private let manager = CLLocationManager()
    private var userId: UUID?
    private var lastSentLocation: CLLocation?
    private var lastSentAt: Date?

    override init() {
        super.init()
        manager.delegate = self
        manager.desiredAccuracy = kCLLocationAccuracyBest
        manager.distanceFilter = 20
    }

    func configure(userId: UUID?) {
        self.userId = userId
        if userId == nil {
            stop()
        }
    }

    func toggle() {
        switch state {
        case .active, .requestingPermission:
            stop()
        default:
            start()
        }
    }

    func start() {
        guard userId != nil else {
            state = .failed("Effettua il login per attivare la posizione live.")
            return
        }

        switch manager.authorizationStatus {
        case .notDetermined:
            state = .requestingPermission
            manager.requestWhenInUseAuthorization()
        case .authorizedWhenInUse, .authorizedAlways:
            state = .active
            manager.startUpdatingLocation()
        case .denied, .restricted:
            state = .denied
        @unknown default:
            state = .failed("Permesso posizione non disponibile.")
        }
    }

    func stop() {
        manager.stopUpdatingLocation()
        state = .off
        lastVenueName = nil
        lastSentLocation = nil
        lastSentAt = nil
        Task {
            try? await API.stopLiveLocation()
        }
    }

    private func handle(location: CLLocation) {
        currentLocation = location
        guard state == .active, let userId else { return }
        guard shouldSend(location) else { return }

        lastSentLocation = location
        lastSentAt = Date()

        Task {
            do {
                let result = try await API.updateLiveLocation(
                    userId: userId,
                    latitude: location.coordinate.latitude,
                    longitude: location.coordinate.longitude,
                    accuracyMeters: location.horizontalAccuracy.isFinite ? location.horizontalAccuracy : nil
                )
                lastVenueName = result.venueName
                lastUpdateAt = Date()
            } catch {
                state = .failed((error as? LocalizedError)?.errorDescription ?? error.localizedDescription)
            }
        }
    }

    private func shouldSend(_ location: CLLocation) -> Bool {
        if let lastSentAt, Date().timeIntervalSince(lastSentAt) < 20 {
            return false
        }
        guard let lastSentLocation else { return true }
        return location.distance(from: lastSentLocation) >= 25
    }
}

extension LiveLocationStore: CLLocationManagerDelegate {
    nonisolated func locationManagerDidChangeAuthorization(_ manager: CLLocationManager) {
        Task { @MainActor in
            switch manager.authorizationStatus {
            case .authorizedAlways, .authorizedWhenInUse:
                state = .active
                manager.startUpdatingLocation()
            case .denied, .restricted:
                state = .denied
            case .notDetermined:
                state = .requestingPermission
            @unknown default:
                state = .failed("Permesso posizione non disponibile.")
            }
        }
    }

    nonisolated func locationManager(_ manager: CLLocationManager, didUpdateLocations locations: [CLLocation]) {
        guard let location = locations.last else { return }
        Task { @MainActor in
            handle(location: location)
        }
    }

    nonisolated func locationManager(_ manager: CLLocationManager, didFailWithError error: Error) {
        Task { @MainActor in
            state = .failed(error.localizedDescription)
        }
    }
}
