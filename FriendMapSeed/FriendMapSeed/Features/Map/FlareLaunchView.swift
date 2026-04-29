//
//  FlareLaunchView.swift
//  Cloudy — Lancia un flare nella zona corrente della mappa
//

import SwiftUI
import CoreLocation
import MapKit

struct FlareLaunchView: View {
    let coordinate: CLLocationCoordinate2D
    var onSent: (String, CLLocationCoordinate2D) -> Void = { _, _ in }

    @Environment(\.dismiss) private var dismiss
    @State private var selectedCoordinate: CLLocationCoordinate2D
    @State private var camera: MapCameraPosition
    @State private var message: String = ""
    @State private var durationHours: Int = 1
    @State private var isSending: Bool = false
    @State private var error: String?

    private let maxLen = 200

    init(coordinate: CLLocationCoordinate2D, onSent: @escaping (String, CLLocationCoordinate2D) -> Void = { _, _ in }) {
        self.coordinate = coordinate
        self.onSent = onSent
        self._selectedCoordinate = State(initialValue: coordinate)
        self._camera = State(initialValue: .region(MKCoordinateRegion(
            center: coordinate,
            span: MKCoordinateSpan(latitudeDelta: 0.012, longitudeDelta: 0.012)
        )))
    }

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: Theme.Spacing.lg) {
                    header

                    SectionCard {
                        VStack(alignment: .leading, spacing: Theme.Spacing.sm) {
                            Text("Cosa stai cercando?")
                                .font(Theme.Font.title(15, weight: .bold))
                            TextEditor(text: $message)
                                .frame(minHeight: 110)
                                .font(Theme.Font.body(15))
                                .scrollContentBackground(.hidden)
                                .padding(8)
                                .background(
                                    RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous)
                                        .fill(Theme.Palette.surfaceAlt)
                                )
                                .overlay(alignment: .topLeading) {
                                    if message.isEmpty {
                                        Text("Es. \"Aperitivo veloce in zona Navigli, chi c'è?\"")
                                            .font(Theme.Font.body(15))
                                            .foregroundStyle(Theme.Palette.inkMuted)
                                            .padding(16)
                                            .allowsHitTesting(false)
                                    }
                                }
                            HStack {
                                Spacer()
                                Text("\(message.count)/\(maxLen)")
                                    .font(Theme.Font.caption(11))
                                    .foregroundStyle(Theme.Palette.inkMuted)
                            }
                        }
                    }

                    SectionCard {
                        Stepper("Durata: \(durationHours) \(durationHours == 1 ? "ora" : "ore")", value: $durationHours, in: 1...4)
                            .font(Theme.Font.body(15, weight: .semibold))
                    }

                    SectionCard {
                        VStack(alignment: .leading, spacing: 10) {
                            HStack(spacing: 12) {
                                Image(systemName: "mappin.and.ellipse")
                                    .font(.system(size: 22))
                                    .foregroundStyle(Theme.Palette.honeyDeep)
                                VStack(alignment: .leading, spacing: 2) {
                                    Text("Posizione del flare")
                                        .font(Theme.Font.body(13, weight: .semibold))
                                    Text(String(format: "%.4f, %.4f", selectedCoordinate.latitude, selectedCoordinate.longitude))
                                        .font(Theme.Font.caption(12))
                                        .foregroundStyle(Theme.Palette.inkSoft)
                                }
                                Spacer()
                            }
                            MapReader { proxy in
                                Map(position: $camera) {
                                    Annotation("Flare", coordinate: selectedCoordinate, anchor: .center) {
                                        Image(systemName: "flame.fill")
                                            .font(.system(size: 18, weight: .bold))
                                            .foregroundStyle(.white)
                                            .frame(width: 40, height: 40)
                                            .background(Circle().fill(Theme.Gradients.honeyCTA))
                                            .shadow(color: Theme.Palette.honeyDeep.opacity(0.35), radius: 10, x: 0, y: 4)
                                    }
                                }
                                .mapStyle(.standard(pointsOfInterest: .excludingAll))
                                .frame(height: 190)
                                .clipShape(RoundedRectangle(cornerRadius: Theme.Radius.md, style: .continuous))
                                .overlay(alignment: .bottom) {
                                    Text("Tocca la mappa per spostare il flare")
                                        .font(Theme.Font.caption(11, weight: .bold))
                                        .foregroundStyle(Theme.Palette.ink)
                                        .padding(.horizontal, 10)
                                        .padding(.vertical, 6)
                                        .background(.thinMaterial, in: Capsule())
                                        .padding(8)
                                }
                                .onTapGesture(coordinateSpace: .local) { point in
                                    if let coordinate = proxy.convert(point, from: .local) {
                                        selectedCoordinate = coordinate
                                        Haptics.tap()
                                    }
                                }
                            }
                        }
                    }

                    if let err = error {
                        Text(err)
                            .font(Theme.Font.caption(12))
                            .foregroundStyle(Theme.Palette.densityHigh)
                    }

                    Button {
                        Task { await send() }
                    } label: {
                        HStack {
                            if isSending {
                                ProgressView().tint(.white)
                            } else {
                                Image(systemName: "paperplane.fill")
                            }
                            Text(isSending ? "Invio…" : "Lancia flare")
                        }
                        .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.honey)
                    .disabled(isSending || trimmed.isEmpty || trimmed.count > maxLen)
                }
                .padding(Theme.Spacing.lg)
            }
            .background(Theme.Palette.surfaceAlt.ignoresSafeArea())
            .navigationTitle("Lancia un flare")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Annulla") { dismiss() }
                }
            }
        }
    }

    private var trimmed: String { message.trimmingCharacters(in: .whitespacesAndNewlines) }

    private var header: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("Lancia un flare")
                .font(Theme.Font.display(24))
            Text("Scrivi un messaggio. Lo vedono le persone vicine e gli amici online.")
                .font(Theme.Font.body(13))
                .foregroundStyle(Theme.Palette.inkSoft)
        }
    }

    private func send() async {
        guard !trimmed.isEmpty else { return }
        isSending = true
        error = nil
        defer { isSending = false }
        do {
            _ = try await API.launchFlare(
                latitude: selectedCoordinate.latitude,
                longitude: selectedCoordinate.longitude,
                message: trimmed,
                durationHours: durationHours
            )
            Haptics.tap()
            onSent(trimmed, selectedCoordinate)
            dismiss()
        } catch {
            Haptics.error()
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }
}
