//
//  PrivacyView.swift
//  Cloudy — Privacy & Ghost mode
//

import SwiftUI

@MainActor
@Observable
final class PrivacyStore {
    var isGhost: Bool = false
    var sharePresence: Bool = true
    var shareIntentions: Bool = true
    var isLoading: Bool = false
    var error: String?

    func load() async {
        isLoading = true
        defer { isLoading = false }
        do {
            let s = try await API.mySocialState()
            self.isGhost = s.isGhostModeEnabled
            self.sharePresence = s.sharePresenceWithFriends
            self.shareIntentions = s.shareIntentionsWithFriends
        } catch {
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    func update(ghost: Bool? = nil, presence: Bool? = nil, intentions: Bool? = nil) async {
        let req = UpdatePrivacySettingsRequest(
            isGhostModeEnabled: ghost,
            sharePresenceWithFriends: presence,
            shareIntentionsWithFriends: intentions
        )
        do {
            let s = try await API.updatePrivacy(req)
            self.isGhost = s.isGhostModeEnabled
            self.sharePresence = s.sharePresenceWithFriends
            self.shareIntentions = s.shareIntentionsWithFriends
            Haptics.tap()
        } catch {
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
            Haptics.error()
        }
    }
}

struct PrivacyView: View {
    @State private var store = PrivacyStore()

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Theme.Spacing.lg) {
                header

                SectionCard {
                    VStack(alignment: .leading, spacing: Theme.Spacing.md) {
                        Text("Modalità fantasma")
                            .font(Theme.Font.title(16, weight: .bold))
                        Text("Quando attiva, sei invisibile sulla mappa. Continui a vedere gli altri.")
                            .font(Theme.Font.caption(12))
                            .foregroundStyle(Theme.Palette.inkSoft)
                        Toggle(isOn: Binding(
                            get: { store.isGhost },
                            set: { newValue in
                                store.isGhost = newValue
                                Task { await store.update(ghost: newValue) }
                            }
                        )) {
                            Label("Attiva modalità fantasma", systemImage: "eye.slash.fill")
                                .font(Theme.Font.body(14, weight: .semibold))
                        }
                        .tint(Theme.Palette.honeyDeep)
                    }
                }

                SectionCard {
                    VStack(alignment: .leading, spacing: Theme.Spacing.md) {
                        Text("Condivisione con gli amici")
                            .font(Theme.Font.title(16, weight: .bold))

                        Toggle(isOn: Binding(
                            get: { store.sharePresence },
                            set: { newValue in
                                store.sharePresence = newValue
                                Task { await store.update(presence: newValue) }
                            }
                        )) {
                            VStack(alignment: .leading, spacing: 2) {
                                Text("Mostra dove sono")
                                    .font(Theme.Font.body(14, weight: .semibold))
                                Text("Gli amici vedono il tuo check-in attivo.")
                                    .font(Theme.Font.caption(11))
                                    .foregroundStyle(Theme.Palette.inkSoft)
                            }
                        }
                        .tint(Theme.Palette.honeyDeep)

                        Divider()

                        Toggle(isOn: Binding(
                            get: { store.shareIntentions },
                            set: { newValue in
                                store.shareIntentions = newValue
                                Task { await store.update(intentions: newValue) }
                            }
                        )) {
                            VStack(alignment: .leading, spacing: 2) {
                                Text("Mostra dove sto andando")
                                    .font(Theme.Font.body(14, weight: .semibold))
                                Text("Gli amici vedono le tue intenzioni di check-in.")
                                    .font(Theme.Font.caption(11))
                                    .foregroundStyle(Theme.Palette.inkSoft)
                            }
                        }
                        .tint(Theme.Palette.honeyDeep)
                    }
                }

                if let err = store.error {
                    Text(err)
                        .font(Theme.Font.caption(12))
                        .foregroundStyle(Theme.Palette.densityHigh)
                }
            }
            .padding(Theme.Spacing.lg)
        }
        .background(Theme.Palette.surfaceAlt.ignoresSafeArea())
        .navigationTitle("Privacy")
        .navigationBarTitleDisplayMode(.inline)
        .task { await store.load() }
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("Tu controlli cosa vedono gli altri")
                .font(Theme.Font.display(22))
            Text("Decidi quando essere visibile e cosa condividere.")
                .font(Theme.Font.body(13))
                .foregroundStyle(Theme.Palette.inkSoft)
        }
    }
}
