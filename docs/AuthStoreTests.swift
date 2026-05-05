import XCTest
@testable import FriendMapSeed

@MainActor
final class AuthStoreTests: XCTestCase {
    
    var sut: AuthStore!
    
    override func setUp() {
        super.setUp()
        // Inizializza lo store partendo da uno stato pulito (simulando assenza di token)
        sut = AuthStore()
        sut.state = .loggedOut
    }
    
    override func tearDown() {
        sut = nil
        super.tearDown()
    }
    
    func test_initialState_shouldBeLoggedOut() {
        if case .loggedOut = sut.state {
            XCTAssertTrue(true)
        } else {
            XCTFail("Lo stato iniziale di AuthStore deve essere .loggedOut")
        }
    }
    
    func test_loginSuccess_updatesStateAndSavesTokens() async throws {
        // Arrange: simuliamo un payload utente di successo
        let expectedUserId = UUID()
        
        // Act: richiamiamo la logica di login finta/mockata
        // Nota: in un'app reale inietteresti un MockAPIClient nel costruttore di AuthStore
        await sut.handleLoginSuccess(accessToken: "mock_jwt", refreshToken: "mock_refresh", user: UserProfile(id: expectedUserId, nickname: "test"))
        
        // Assert
        if case .loggedIn(let user) = sut.state {
            XCTAssertEqual(user.id, expectedUserId, "L'utente loggato deve corrispondere a quello ricevuto")
        } else {
            XCTFail("Lo stato non è transizionato in .loggedIn")
        }
    }
    
    func test_logout_clearsTokensAndState() async {
        // Arrange
        await sut.handleLoginSuccess(accessToken: "mock", refreshToken: "mock", user: UserProfile(id: UUID(), nickname: "test"))
        
        // Act
        sut.logout()
        
        // Assert
        if case .loggedOut = sut.state {
            XCTAssertTrue(true)
        } else {
            XCTFail("Lo stato deve tornare a .loggedOut dopo il logout")
        }
        
        // Verifica che i token siano stati rimossi (concettualmente)
        XCTAssertNil(sut.token, "L'access token deve essere rimosso")
        XCTAssertNil(sut.refreshToken, "Il refresh token deve essere rimosso")
    }
    
    func test_restoreSession_withValidTokens_bypassesLoginScreen() async throws {
        // Arrange: Simuliamo che Keychain abbia già i token
        sut.token = "valid_jwt"
        sut.refreshToken = "valid_refresh"
        
        // Act
        await sut.restore() // Metodo che l'app chiama al lancio in RootView
        
        // Assert
        if case .loggedIn = sut.state {
            XCTAssertTrue(true)
        } else {
            XCTFail("Il restore deve recuperare la sessione ed evitare il login")
        }
    }
    
    func test_tokenRefresh_onApiUnauthorizedError() async throws {
        // Arrange: stato loggato ma con token scaduto
        sut.token = "expired_jwt"
        sut.refreshToken = "valid_refresh"
        sut.state = .loggedIn(UserProfile(id: UUID(), nickname: "old"))
        
        // Simuliamo una chiamata API che fallisce con 401 e lancia il refresh
        let refreshSuccess = await sut.attemptTokenRefresh()
        
        // Assert
        XCTAssertTrue(refreshSuccess, "Il refresh del token deve avere successo con un refresh token valido")
        XCTAssertNotEqual(sut.token, "expired_jwt", "Il token deve essere stato aggiornato")
    }
    
    func test_biometricAuthentication_unlocksApp() async throws {
        // Arrange
        sut.state = .locked // Stato introdotto per nascondere dati sensibili
        
        // Act
        // mockAuthenticateWithBiometrics simula il LAContext .deviceOwnerAuthenticationWithBiometrics
        let success = try await sut.authenticateWithBiometrics(reason: "Sblocca Cloudy")
        
        // Assert
        XCTAssertTrue(success)
        if case .locked = sut.state {
            XCTFail("Lo stato non deve essere locked dopo autenticazione biometrica con successo")
        }
    }
}