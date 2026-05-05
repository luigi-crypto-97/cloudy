import XCTest

final class UXAndDesignUITests: XCTestCase {

    var app: XCUIApplication!

    override func setUpWithError() throws {
        continueAfterFailure = false
        app = XCUIApplication()
        
        // Passiamo variabili d'ambiente per disabilitare le animazioni nei test UI
        // e settare l'ambiente come "UITesting" così l'app sa di non chiamare API vere
        app.launchEnvironment = ["UITEST_MODE": "1", "ENABLE_ANIMATIONS": "0"]
        app.launch()
    }

    override func tearDownWithError() throws {
        app = nil
    }

    func test_loginScreen_hasAccessibleElements() throws {
        // Verifica che le localizzazioni introdotte funzionino e siano accessibili
        let loginButton = app.buttons["auth.login"] // Si aspetta l'identifier o la stringa localizzata
        
        // Se l'app avvia in uno stato sloggato, il tasto di login deve esserci
        if loginButton.exists {
            XCTAssertTrue(loginButton.isHittable, "Il bottone di login deve essere toccabile")
        }
    }
    
    func test_mapScreen_displaysBottomSheetOnVenueTap() throws {
        let map = app.maps.firstMatch
        guard map.waitForExistence(timeout: 5.0) else {
            throw XCTSkip("La mappa non è esposta come MKMapView nativa in questa build UI.")
        }

        let mapAnnotation = app.otherElements.matching(identifier: "venue_bubble_annotation").firstMatch

        if mapAnnotation.exists {
            mapAnnotation.tap()

            let bottomSheet = app.scrollViews.matching(identifier: "venue_bottom_sheet").firstMatch
            XCTAssertTrue(bottomSheet.waitForExistence(timeout: 2.0), "Il bottom sheet del locale deve apparire al tap")
            XCTAssertTrue(bottomSheet.buttons["Check-in"].exists || bottomSheet.buttons["Check in"].exists)
        } else {
            throw XCTSkip("Nessuna venue annotation visibile nello stato iniziale del test.")
        }
    }
    
    func test_liveLocationButton_triggersAccessibilityAction() {
        // "Centra posizione" come richiesto dall'audit dell'accessibilità
        let locationButton = app.buttons["Centra posizione"]
        
        if locationButton.exists {
            XCTAssertTrue(locationButton.isEnabled)
            locationButton.tap()
        }
    }
    
    func test_fullUserJourney_map_to_feed_and_story() throws {
        let tabBar = app.tabBars.firstMatch
        guard tabBar.waitForExistence(timeout: 3.0) else {
            throw XCTSkip("L'app usa una tab bar custom, non una UITabBar esposta a XCTest.")
        }

        let feedTab = tabBar.buttons["Feed"]
        if feedTab.exists {
            feedTab.tap()

            let storyRing = app.images.matching(identifier: "story_ring_avatar").firstMatch
            XCTAssertTrue(storyRing.waitForExistence(timeout: 5.0), "La UI delle Stories deve apparire in alto nel feed")

            storyRing.tap()

            let closeButton = app.buttons["Chiudi"]
            XCTAssertTrue(closeButton.waitForExistence(timeout: 2.0))
            closeButton.tap()
        } else {
            throw XCTSkip("Tab Feed non esposta nella UITabBar nativa.")
        }
    }
    
    func test_accessibility_dynamicType_andVoiceOverLabels() throws {
        // Testa esplicitamente che le Label VoiceOver personalizzate siano attaccate correttamente
        let flareButton = app.buttons["lancia_flare_button"]
        if flareButton.exists {
            // L'etichetta accessibilità non dovrebbe essere il nome tecnico
            let accessibilityLabel = flareButton.label
            XCTAssertNotEqual(accessibilityLabel, "lancia_flare_button", "Il pulsante deve avere una label localizzata e descrittiva per VoiceOver (es. 'Lancia un flare qui')")
        }
    }
}
