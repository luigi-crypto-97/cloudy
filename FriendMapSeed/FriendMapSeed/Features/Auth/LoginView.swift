//
//  LoginView.swift
//  Cloudy — ingresso beta.
//

import SwiftUI
import AuthenticationServices

struct LoginView: View {
    @Environment(AuthStore.self) private var auth

    @State private var nickname: String = ""
    @State private var displayName: String = ""
    @State private var backendString: String = ""
    @State private var isSubmitting: Bool = false
    @State private var isAppleSubmitting: Bool = false

    var body: some View {
        ZStack {
            MeshGradientBackground(preset: .loginHero)

            ScrollView(showsIndicators: false) {
                VStack(alignment: .leading, spacing: Theme.Spacing.xxl) {
                    hero
                        .padding(.top, 76)

                    formCard

                    Text("Beta privata. Usa il backend corretto e scegli come vuoi apparire agli amici.")
                        .font(Theme.Font.caption(12))
                        .foregroundStyle(.white.opacity(0.72))
                        .multilineTextAlignment(.center)
                        .frame(maxWidth: .infinity)
                        .padding(.bottom, 36)
                }
                .padding(.horizontal, Theme.Spacing.xl)
            }
        }
        .onAppear { backendString = auth.backendURL.absoluteString }
    }

    // MARK: - Hero moment
    // Un solo campo visivo forte: blu profondo, logo solido, promessa chiara.
    private var hero: some View {
        VStack(alignment: .leading, spacing: Theme.Spacing.xl) {
            ZStack {
                RoundedRectangle(cornerRadius: 28, style: .continuous)
                    .fill(.white.opacity(0.14))
                    .frame(width: 92, height: 92)
                    .overlay(
                        RoundedRectangle(cornerRadius: 28, style: .continuous)
                            .stroke(.white.opacity(0.22), lineWidth: 1)
                    )
                Image(systemName: "cloud.fill")
                    .font(.system(size: 46, weight: .heavy))
                    .foregroundStyle(.white)
            }

            VStack(alignment: .leading, spacing: 10) {
                Text("Conosci chi e vicino")
                    .font(Theme.Font.display(40))
                    .tracking(-0.5)
                    .foregroundStyle(.white)
                    .lineLimit(2)
                Text("Cloudy ti mostra luoghi, amici e piani intorno a te con calma e controllo.")
                    .font(Theme.Font.body(16))
                    .lineSpacing(6)
                    .foregroundStyle(.white.opacity(0.82))
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
    }

    private var formCard: some View {
        CloudyCard {
            Text("Entra in Cloudy")
                .font(Theme.Font.display(24))
                .tracking(-0.5)
                .foregroundStyle(Theme.Palette.ink)

            CloudyTextField(title: "Backend URL", placeholder: "https://api.iron-quote.it", text: $backendString)

            SignInWithAppleButton(.continue) { request in
                request.requestedScopes = [.fullName, .email]
            } onCompletion: { result in
                Task { await handleAppleSignIn(result) }
            }
            .signInWithAppleButtonStyle(.black)
            .frame(height: 52)
            .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
            .disabled(isSubmitting || isAppleSubmitting)

            HStack {
                Rectangle().fill(Theme.Palette.hairline).frame(height: 1)
                Text("oppure beta")
                    .font(Theme.Font.caption(12, weight: .medium))
                    .foregroundStyle(Theme.Palette.inkMuted)
                Rectangle().fill(Theme.Palette.hairline).frame(height: 1)
            }

            CloudyTextField(title: "Nickname", placeholder: "giulia", text: $nickname)
            CloudyTextField(title: "Nome visualizzato", placeholder: "Giulia", text: $displayName)

            if let err = auth.lastError {
                Text(err)
                    .font(Theme.Font.caption(12, weight: .medium))
                    .foregroundStyle(Theme.Palette.coral500)
                    .padding(.top, 2)
            }

            Button {
                Task { await submit() }
            } label: {
                HStack {
                    if isSubmitting { LoadingDots() }
                    Text(isSubmitting ? "Accesso in corso" : "Continua")
                        .frame(maxWidth: .infinity)
                }
            }
            .buttonStyle(.cloudyPrimary)
            .disabled(nickname.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || isSubmitting)
            .padding(.top, Theme.Spacing.sm)
        }
    }

    private func submit() async {
        guard let url = URL(string: backendString.trimmingCharacters(in: .whitespacesAndNewlines)) else {
            auth.lastError = "URL backend non valido"
            Haptics.error()
            return
        }
        auth.backendURL = url
        isSubmitting = true
        defer { isSubmitting = false }
        await auth.devLogin(
            nickname: nickname.trimmingCharacters(in: .whitespacesAndNewlines),
            displayName: displayName.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? nil : displayName
        )
        if auth.lastError == nil {
            Haptics.success()
        }
    }

    private func handleAppleSignIn(_ result: Result<ASAuthorization, Error>) async {
        guard let url = URL(string: backendString.trimmingCharacters(in: .whitespacesAndNewlines)) else {
            auth.lastError = "URL backend non valido"
            Haptics.error()
            return
        }

        switch result {
        case .success(let authorization):
            guard let credential = authorization.credential as? ASAuthorizationAppleIDCredential,
                  let identityData = credential.identityToken,
                  let identityToken = String(data: identityData, encoding: .utf8) else {
                auth.lastError = "Apple non ha restituito un token valido."
                Haptics.error()
                return
            }

            let authorizationCode = credential.authorizationCode.flatMap { String(data: $0, encoding: .utf8) }
            let fullName = PersonNameComponentsFormatter.localizedString(
                from: credential.fullName ?? PersonNameComponents(),
                style: .medium,
                options: []
            ).trimmingCharacters(in: .whitespacesAndNewlines)

            auth.backendURL = url
            isAppleSubmitting = true
            let ok = await auth.loginWithApple(
                identityToken: identityToken,
                authorizationCode: authorizationCode,
                fullName: fullName.isEmpty ? nil : fullName
            )
            isAppleSubmitting = false
            ok ? Haptics.success() : Haptics.error()

        case .failure(let error):
            auth.lastError = error.localizedDescription
            Haptics.error()
        }
    }
}
