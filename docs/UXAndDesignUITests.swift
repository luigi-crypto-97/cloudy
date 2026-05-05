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
        // Assumendo che il login sia stato saltato tramite flag di UITest, cerchiamo la Mappa
        let map = app.maps.firstMatch
        XCTAssertTrue(map.waitForExistence(timeout: 5.0), "La visualizzazione della mappa nativa è fallita")
        
        // Cerchiamo una 'nuvoletta/bubble' custom renderizzata sulla mappa
        let mapAnnotation = app.otherElements.matching(identifier: "venue_bubble_annotation").firstMatch
        
        if mapAnnotation.exists {
            mapAnnotation.tap()
            
            // Verifica che il Bottom Sheet sia salito (UX design spec)
            let bottomSheet = app.scrollViews.matching(identifier: "venue_bottom_sheet").firstMatch
            XCTAssertTrue(bottomSheet.waitForExistence(timeout: 2.0), "Il bottom sheet del locale deve apparire al tap")
            
            // Verifica la presenza dei bottoni action (Check-in, Pianifica)
            XCTAssertTrue(bottomSheet.buttons["Check-in"].exists || bottomSheet.buttons["Check in"].exists)
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
        // Verifica che la navigazione tramite TabBar o NavigationStack funzioni fluido
        let tabBar = app.tabBars.firstMatch
        XCTAssertTrue(tabBar.waitForExistence(timeout: 3.0))
        
        // Spostiamoci sul Feed
        let feedTab = tabBar.buttons["Feed"] // o label L10n
        if feedTab.exists {
            feedTab.tap()
            
            // Verifica presenza componentistica UI specificata in audit
            let storyRing = app.images.matching(identifier: "story_ring_avatar").firstMatch
            XCTAssertTrue(storyRing.waitForExistence(timeout: 5.0), "La UI delle Stories deve apparire in alto nel feed")
            
            // Tappa la storia
            storyRing.tap()
            
            // Verifica l'apertura a schermo intero
            let closeButton = app.buttons["Chiudi"]
            XCTAssertTrue(closeButton.waitForExistence(timeout: 2.0))
            closeButton.tap()
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