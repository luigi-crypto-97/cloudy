//
//  FlareLaunchView.swift
//  Cloudy — Lancia un flare nella zona corrente della mappa
//

import SwiftUI
import CoreLocation

struct FlareLaunchView: View {
    let coordinate: CLLocationCoordinate2D
    var onSent: () -> Void = {}

    @Environment(\.dismiss) private var dismiss
    @State private var message: String = ""
    @State private var isSending: Bool = false
    @State private var error: String?

    private let maxLen = 200

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
                        HStack(spacing: 12) {
                            Image(systemName: "mappin.and.ellipse")
                                .font(.system(size: 22))
                                .foregroundStyle(Theme.Palette.honeyDeep)
                            VStack(alignment: .leading, spacing: 2) {
                                Text("Posizione del flare")
                                    .font(Theme.Font.body(13, weight: .semibold))
                                Text(String(format: "%.4f, %.4f", coordinate.latitude, coordinate.longitude))
                                    .font(Theme.Font.caption(12))
                                    .foregroundStyle(Theme.Palette.inkSoft)
                            }
                            Spacer()
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
                latitude: coordinate.latitude,
                longitude: coordinate.longitude,
                message: trimmed
            )
            Haptics.tap()
            onSent()
            dismiss()
        } catch {
            Haptics.error()
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }
}
