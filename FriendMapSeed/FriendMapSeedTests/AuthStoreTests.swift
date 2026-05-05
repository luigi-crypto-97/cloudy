import XCTest
@testable import FriendMapSeed

@MainActor
final class AuthStoreTests: XCTestCase {

    var sut: AuthStore!

    override func setUp() {
        super.setUp()
        UserDefaults.standard.removeObject(forKey: "cloudy.backendURL")
        sut = AuthStore()
        sut.state = .loggedOut
    }

    override func tearDown() {
        sut.logout()
        UserDefaults.standard.removeObject(forKey: "cloudy.backendURL")
        sut = nil
        super.tearDown()
    }

    func test_defaultBackendURL_usesProductionApi() {
        XCTAssertEqual(sut.backendURL.absoluteString, "https://api.iron-quote.it")
    }

    func test_logout_clearsStateAndReadableTokens() {
        sut.state = .loggedIn(AuthUser(userId: UUID(), nickname: "test", displayName: nil))

        sut.logout()

        if case .loggedOut = sut.state {
            XCTAssertTrue(true)
        } else {
            XCTFail("Lo stato deve tornare a .loggedOut dopo il logout")
        }
        XCTAssertNil(sut.token)
        XCTAssertNil(sut.refreshToken)
    }

    func test_backendURL_persistsToUserDefaults() {
        let customURL = URL(string: "https://example.test")!

        sut.backendURL = customURL

        XCTAssertEqual(UserDefaults.standard.string(forKey: "cloudy.backendURL"), customURL.absoluteString)
    }

    func test_forceLogout_clearsPersistedBackendURL() {
        sut.backendURL = URL(string: "https://example.test")!

        sut.forceLogout()

        XCTAssertNil(UserDefaults.standard.string(forKey: "cloudy.backendURL"))
        if case .loggedOut = sut.state {
            XCTAssertTrue(true)
        } else {
            XCTFail("Il force logout deve lasciare lo store sloggato")
        }
    }

    func test_biometricAvailability_isReadableWithoutPrompting() {
        _ = sut.isBiometricAuthAvailable
    }
}
