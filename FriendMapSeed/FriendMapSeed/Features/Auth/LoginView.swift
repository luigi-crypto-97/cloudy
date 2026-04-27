//
//  LoginView.swift
//  Cloudy — Schermata di login dev (nickname + backend URL)
//

import SwiftUI

struct LoginView: View {
    @Environment(AuthStore.self) private var auth

    @State private var nickname: String = ""
    @State private var displayName: String = ""
    @State private var backendString: String = ""
    @State private var isSubmitting: Bool = false

    var body: some View {
        ScrollView {
            VStack(spacing: Theme.Spacing.xl) {
                // Hero
                VStack(spacing: 14) {
                    ZStack {
                        Circle()
                            .fill(Theme.Gradients.honeyCTA)
                            .frame(width: 140, height: 140)
                            .liftedShadow()
                        Image(systemName: "cloud.fill")
                            .font(.system(size: 72, weight: .black))
                            .foregroundStyle(.white)
                    }

                    Text("Cloudy")
                        .font(Theme.Font.display(40))
                        .foregroundStyle(Theme.Palette.ink)
                    Text("Vivi i posti, non gli sconosciuti.")
                        .font(Theme.Font.body(15))
                        .foregroundStyle(Theme.Palette.inkSoft)
                        .multilineTextAlignment(.center)
                }
                .padding(.top, Theme.Spacing.xxl)

                // Form
                VStack(alignment: .leading, spacing: Theme.Spacing.md) {
                    Text("Backend URL")
                        .font(Theme.Font.caption(11, weight: .bold))
                        .foregroundStyle(Theme.Palette.inkMuted)
                    TextField("https://api.iron-quote.it", text: $backendString)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled(true)
                        .keyboardType(.URL)
                        .padding(12)
                        .background(Theme.Palette.surfaceAlt, in: RoundedRectangle(cornerRadius: 12))

                    Text("Nickname")
                        .font(Theme.Font.caption(11, weight: .bold))
                        .foregroundStyle(Theme.Palette.inkMuted)
                        .padding(.top, 4)
                    TextField("giulia", text: $nickname)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled(true)
                        .padding(12)
                        .background(Theme.Palette.surfaceAlt, in: RoundedRectangle(cornerRadius: 12))

                    Text("Nome visualizzato (opzionale)")
                        .font(Theme.Font.caption(11, weight: .bold))
                        .foregroundStyle(Theme.Palette.inkMuted)
                        .padding(.top, 4)
                    TextField("Giulia Dev", text: $displayName)
                        .padding(12)
                        .background(Theme.Palette.surfaceAlt, in: RoundedRectangle(cornerRadius: 12))

                    if let err = auth.lastError {
                        Text(err)
                            .font(Theme.Font.body(13))
                            .foregroundStyle(Theme.Palette.densityHigh)
                            .padding(.top, 4)
                    }

                    Button {
                        Task { await submit() }
                    } label: {
                        HStack {
                            if isSubmitting { ProgressView().tint(Theme.Palette.ink) }
                            Text(isSubmitting ? "Accesso…" : "Entra")
                                .frame(maxWidth: .infinity)
                        }
                    }
                    .buttonStyle(.honey)
                    .disabled(nickname.trimmingCharacters(in: .whitespaces).isEmpty || isSubmitting)
                    .padding(.top, Theme.Spacing.md)
                }
                .padding(Theme.Spacing.lg)
                .background(
                    RoundedRectangle(cornerRadius: Theme.Radius.lg)
                        .fill(Theme.Palette.surface)
                )
                .cardShadow()

                Text("Login dev — usa qualsiasi nickname per testare con il backend locale.")
                    .font(Theme.Font.caption(11))
                    .foregroundStyle(Theme.Palette.inkMuted)
                    .multilineTextAlignment(.center)
            }
            .padding(.horizontal, Theme.Spacing.lg)
        }
        .background(Theme.Palette.surfaceAlt.ignoresSafeArea())
        .onAppear { backendString = auth.backendURL.absoluteString }
    }

    private func submit() async {
        guard let url = URL(string: backendString.trimmingCharacters(in: .whitespaces)) else {
            auth.lastError = "URL backend non valido"
            return
        }
        auth.backendURL = url
        isSubmitting = true
        defer { isSubmitting = false }
        await auth.devLogin(
            nickname: nickname.trimmingCharacters(in: .whitespaces),
            displayName: displayName.isEmpty ? nil : displayName
        )
    }
}
