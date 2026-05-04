# Audit Progetto iOS Native SwiftUI - Cloudy (FriendMapSeed)

**Data audit:** 4 Maggio 2026  
**Branch:** `feat/ios-native-swiftui`  
**Stato:** Produzione-ready con miglioramenti raccomandati

---

## Executive Summary

Il client iOS nativo SwiftUI rappresenta un **miglioramento significativo** rispetto alla precedente implementazione .NET MAUI. L'architettura è moderna, performante e ben strutturata. Tuttavia, mancano diverse componenti essenziali per un'app di produzione.

### Valutazione Complessiva

| Categoria | Voto | Note |
|-----------|------|------|
| Architettura | ⭐⭐⭐⭐☆ | Observable + @MainActor, ma no DI framework |
| Performance | ⭐⭐⭐⭐⭐ | TimelineView singolo, Task.detached, debounce |
| Sicurezza | ⭐⭐☆☆☆ | Manca certificate pinning, token refresh |
| Testing | ⭐⭐☆☆☆ | Solo 13 test su CloudyCore, no UI test |
| UX/UI | ⭐⭐⭐⭐☆ | Design system coerente, animazioni curate |
| Offline | ⭐☆☆☆☆ | Nessun caching, no database locale |
| Accessibilità | ⭐⭐☆☆☆ | Label base, no Dynamic Type audit |
| Internazionalizzazione | ⭐☆☆☆☆ | Tutto hardcoded in italiano |

---

## 1. Architettura e Struttura del Codice

### ✅ Punti di Forza

**1.1 Stack Tecnologico Moderno**
- iOS 17+ con `@Observable` macro (no boilerplate Combine)
- `@MainActor` isolation per thread safety
- Async/await per tutto il networking
- SwiftUI nativo con MapKit integration

**1.2 Separation of Concerns**
```
FriendMapSeed/
├── App/                    → AppRouter, RootView (auth gate)
├── DesignSystem/           → Theme, Components, Motion
├── Features/               → Feature-based (Auth, Chat, Feed, Map, etc.)
├── Models/                 → Codable DTOs
├── Networking/             → APIClient + Endpoints
├── Stores/                 → State management (@Observable)
├── Utilities/              → Haptics, helpers
└── Packages/CloudyCore/    → Cross-platform domain logic
```

**1.3 CloudyCore Package**
- Logica di dominio pura e testabile
- Cross-platform (Swift 6.0.3 su Linux)
- 13 unit test passing
- FogLinkBuilder O(n²) ottimizzato con maxClusters cap

### ❌ Criticità

**1.1 God Files**
- `Models.swift`: ~900 linee (dovrebbe essere splittato per dominio)
- `Endpoints.swift`: ~500 linee (350+ metodi API)
- `FeedView.swift`: ~1024 linee (singola view troppo complessa)

**1.2 No Protocol Abstraction per Networking**
```swift
// Problema: APIClient è una classe concreta, non un protocollo
final class APIClient {
    static let shared = APIClient.shared
    // ...
}

// Rende il mocking difficile per i test
```

**Raccomandazione:**
```swift
protocol APIClientProtocol {
    var baseURL: URL { get set }
    func get<R: Decodable>(_ path: String, query: [String: String?]) async throws -> R
    // ...
}

final class APIClient: APIClientProtocol { ... }

// Per i test:
final class MockAPIClient: APIClientProtocol { ... }
```

**1.3 Hardcoded URLs**
```swift
// AuthStore.swift
let urlString = UserDefaults.standard.string(forKey: Keys.backendURL) ?? "https://api.iron-quote.it"

// APIClient.swift
self.baseURL = URL(string: "https://api.iron-quote.it")!
```

**Raccomandazione:** Usare `.xcconfig` per environment separation:
```xcconfig
// Debug.xcconfig
API_BASE_URL = http://localhost:8080

// Release.xcconfig
API_BASE_URL = https://api.iron-quote.it
```

**1.4 No Dependency Injection Framework**
- Tutti gli store sono iniettati via `.environment()`
- Funziona per app piccole, difficile da testare per app grandi

**Raccomandazione:** Considerare Swinject o Factory per progetti più complessi.

---

## 2. Networking e API Integration

### ✅ Punti di Forza

**2.1 APIClient Ben Strutturato**
- Singleton con configurazione dinamica
- JWT Bearer token injection automatica
- Custom JSON encoding/decoding per compatibilità C# backend
- Multipart upload per media files
- Error handling tipizzato (`APIError` enum)

**2.2 Backend Compatibility**
```swift
// PascalCase (C#) → camelCase (Swift)
d.keyDecodingStrategy = .custom { keys in
    let key = keys.last!.stringValue
    guard let first = key.first else { return keys.last! }
    return PascalCaseKey(stringValue: first.lowercased() + key.dropFirst())
}

// ISO8601 con fractional seconds opzionali
d.dateDecodingStrategy = .iso8601WithFractional
```

**2.3 Endpoint Coverage Completo**
- Auth, Venues, Feed, Stories, Chat, Tables, Flares, Gamification, Privacy
- 50+ endpoint methods in `Endpoints.swift`

### ❌ Criticità

**2.1 No Request/Response Logging**
```swift
// Non c'è modo di debuggare le chiamate HTTP in development
```

**Raccomandazione:**
```swift
// Aggiungere in APIClient.send()
if ProcessInfo.processInfo.environment["LOG_NETWORK"] == "1" {
    print("[HTTP] \(method) \(path)")
    print("[Request] \(String(data: bodyData, encoding: .utf8) ?? "")")
}
```

**2.2 No Retry Logic**
- Solo URLSession defaults
- No exponential backoff per transient errors

**2.3 No Rate Limiting Client-Side**
- Il backend ha rate limiting, ma il client potrebbe beneficiare di code locali

**2.4 Chat Polling Invece di SignalR**
```swift
// ChatRoomView.swift - polling ogni 5 secondi
Task {
    while !Task.isCancelled {
        try? await Task.sleep(nanoseconds: 5_000_000_000)
        await pollThread()
    }
}
```

**Il backend ha SignalR (`ChatHub.cs`) ma il client iOS non lo usa!**

**Raccomandazione PRIORITARIA:**
```swift
import SignalRClient

final class ChatConnection {
    private var connection: HubConnection?
    
    func connect() async throws {
        connection = HubConnectionBuilder(url: URL(string: "\(baseURL)/chathub")!)
            .withAutoReconnect()
            .build()
        
        connection?.on(method: "ReceiveMessage") { [weak self] args in
            // Handle message
        }
        
        try await connection?.start()
    }
}
```

---

## 3. Sicurezza

### ✅ Punti di Forza

**3.1 JWT in Keychain**
```swift
private enum Keychain {
    static func save(_ value: String, for key: String) {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrAccount as String: key,
            kSecValueData as String: data,
            kSecAttrAccessible as String: kSecAttrAccessibleAfterFirstUnlock
        ]
        SecItemAdd(query as CFDictionary, nil)
    }
}
```

### ❌ Criticità CRITICHE

**3.1 No Token Refresh Mechanism**
- JWT scade dopo 7 giorni (10080 minuti da `appsettings.json`)
- Quando scade, l'utente deve fare login di nuovo

**Raccomandazione:**
- Implementare refresh token rotation
- Intercept 401 errors e tentare refresh automatico

**3.2 No Certificate Pinning**
```swift
// URLSessionConfiguration.default senza pinning
let cfg = URLSessionConfiguration.default
```

**Raccomandazione:**
```swift
class PinnedURLSessionDelegate: NSObject, URLSessionDelegate {
    func urlSession(_ session: URLSession, didReceive challenge: URLAuthenticationChallenge) async -> URLSession.AuthChallengeDisposition {
        guard let serverTrust = challenge.protectionSpace.serverTrust else {
            return .cancelAuthenticationChallenge
        }
        
        // Verifica certificato pinato
        let pinned = verifyCertificate(serverTrust)
        return pinned ? .useCredential : .cancelAuthenticationChallenge
    }
}
```

**3.3 No Jailbreak Detection**
- L'app può essere eseguita su device jailbroken
- Rischio per security e cheating

**3.4 No Biometric Authentication**
- No FaceID/TouchID per riaprire l'app

**Raccomandazione:**
```swift
import LocalAuthentication

func authenticateWithBiometrics() async throws -> Bool {
    let context = LAContext()
    var error: NSError?
    
    guard context.canEvaluatePolicy(.deviceOwnerAuthenticationWithBiometrics, error: &error) else {
        return false
    }
    
    return try await context.evaluatePolicy(.deviceOwnerAuthenticationWithBiometrics, localizedReason: "Accedi a Cloudy")
}
```

**3.5 No Screenshot Prevention per Schermate Sensibili**
- Profile, Privacy settings potrebbero essere screenshotati

---

## 4. Testing e Qualità del Codice

### ✅ Punti di Forza

**4.1 CloudyCore Testato**
- 13 unit test passing
- Test su FogLinkBuilder, Geo.distance, JSON decoding
- Performance test (< 50ms per 60 cluster)

### ❌ Criticità

**4.1 No Linting Configuration**
- ❌ No `.swiftlint.yml`
- ❌ No `.editorconfig`

**Raccomandazione:**
```yaml
# .swiftlint.yml
disabled_rules:
  - line_length
  - function_body_length

opt_in_rules:
  - explicit_acl
  - explicit_top_level_acl
  - missing_docs

line_length: 160
function_body_length: 60
file_length: 400
```

**4.2 No Test per Stores**
- AuthStore, MapStore, FeedStore non testati
- No integration test per API layer

**4.3 No UI Tests**
- No XCUITest targets
- No snapshot testing

**4.4 No CI/CD Configuration**
- ❌ No GitHub Actions
- ❌ No Xcode Cloud workflow
- ❌ No automated build verification

**Raccomandazione MINIMA:**
```yaml
# .github/workflows/ios-build.yml
name: iOS Build
on: [push, pull_request]
jobs:
  build:
    runs-on: macos-14
    steps:
      - uses: actions/checkout@v3
      - name: Build iOS
        run: |
          cd FriendMapSeed
          xcodebuild -project FriendMapSeed.xcodeproj \
            -scheme FriendMapSeed \
            -destination 'platform=iOS Simulator,name=iPhone 15' \
            build
```

---

## 5. Offline Support e Caching

### ❌ Criticità GRAVE

**5.1 No Local Database**
- CoreData / Realm / SQLite assenti
- Nessuna cache persistente
- Se offline, l'app è inutile

**5.2 No Request Queueing**
- I messaggi inviati offline vanno persi
- No retry queue per failed requests

**5.3 No Cached Content Display**
- Feed, Map, Profile non mostrano dati cached

**Raccomandazione PRIORITARIA:**
```swift
import CoreData

// 1. Aggiungere Core Data stack
final class DataController {
    static let shared = DataController()
    let container: NSPersistentContainer
    
    init() {
        container = NSPersistentContainer(name: "Cloudy")
        container.loadPersistentStores { _, error in
            if let error { fatalError("Core Data load failed: \(error)") }
        }
    }
}

// 2. Cache venues in MapStore
func fetchMarkers(in region: MKCoordinateRegion) async {
    // 1. Show cached data immediately
    markers = try? fetchCachedVenues(in: region)
    
    // 2. Fetch fresh data
    do {
        let fresh = try await API.venueMapLayer(...)
        markers = fresh.markers
        cacheVenues(fresh.markers)
    } catch {
        // Show cached with error banner
    }
}

// 3. Queue messages for offline send
func sendMessage(_ message: String) {
    if isOffline {
        queueMessageForLater(message)
        return
    }
    Task { try await API.sendMessage(...) }
}
```

---

## 6. Accessibilità

### ✅ Punti di Forza

**6.1 Basic Accessibility Labels**
```swift
Button {
    centerOnUser()
} label: {
    Image(systemName: "location.viewfinder")
}
.accessibilityLabel(Text("Centra posizione"))
```

### ❌ Criticità

**6.1 No Dynamic Type Support Audit**
- Font size hardcoded in molti punti
- `.font(Theme.Font.body(15))` non scala con Accessibility

**Raccomandazione:**
```swift
// Invece di font size fisso:
Text(title)
    .font(Theme.Font.body(15))

// Usare Dynamic Type:
Text(title)
    .font(.body)
    .dynamicTypeSize(...100)
```

**6.2 No VoiceOver Testing Evidence**
- Nessun test documentato con VoiceOver
- Alcuni custom view potrebbero non essere accessibili

**6.3 No Color Contrast Audit**
- Theme.Palette ha colori custom, nessun audit WCAG

---

## 7. Internazionalizzazione (i18n)

### ❌ Criticità GRAVE

**7.1 Tutto Hardcoded in Italiano**
```swift
Text("Aggiorno la mappa…")
Text("Caricamento")
Text("Effettua il login")
```

**Raccomandazione PRIORITARIA:**
```swift
// 1. Creare Localizable.strings
// Localizable.strings (Italian)
"map.loading" = "Aggiorno la mappa…";
"loading" = "Caricamento";
"auth.login" = "Effettua il login";

// Localizable.strings (English)
"map.loading" = "Updating map…";
"loading" = "Loading";
"auth.login" = "Sign in";

// 2. Usare NSLocalizedString
Text(NSLocalizedString("map.loading", comment: "Status message when refreshing map"))

// 3. O meglio, creare un helper type-safe
enum L10n {
    enum Map {
        static let loading = NSLocalizedString("map.loading", comment: "")
    }
}

Text(L10n.Map.loading)
```

**7.2 No Locale-Aware Formatting**
- Date, numeri, valute non formattati per locale

---

## 8. Performance

### ✅ Punti di Forza

**8.1 TimelineView Singolo per Animazioni**
```swift
// Una sola TimelineView a livello MapView, non N timer per nuvola
TimelineView(.animation) { timeline in
    let phase = timeline.phase
    // Tutte le CloudBubble usano questo phase
}
```

**8.2 Task.detached per Fog Links**
```swift
fogTask = Task.detached(priority: .utility) {
    let coreLinks = FogLinkBuilder.build(from: inputs)
    await MainActor.run {
        self.fogLinks = mapped
    }
}
```

**8.3 Debounce per Fetch**
```swift
fetchTask = Task { [weak self] in
    try? await Task.sleep(nanoseconds: 350_000_000) // 350ms debounce
    await self?.fetchMarkers(in: region)
}
```

### ❌ Criticità

**8.1 No Image Caching**
- `AsyncImage` usa cache default di SwiftUI
- No controllo su cache size o eviction

**Raccomandazione:**
```swift
import Nuke

// Usare Nuke per image caching avanzato
let pipeline = ImagePipeline {
    $0.imageCache = ImageCache()
    $0.dataCache = DataCache()
}

AsyncImage(url: url, pipeline: pipeline)
```

**8.2 No Pagination per Feed**
- FeedView carica tutti gli items in una volta
- Potrebbe diventare lento con molti items

**8.3 No Prefetching**
- No `onAppear` prefetch per liste scrollabili

---

## 9. Notifiche Push

### ✅ Punti di Forza

**9.1 APNs Registration Implementata**
```swift
final class NotificationBridge {
    func activate(for userId: UUID) {
        requestAuthorizationIfNeeded()
        Task { await registerIfPossible() }
    }
}
```

**9.2 Backend Integration**
- `POST /api/notifications/device-tokens` implementato
- APNs client HTTP/2 nel backend

### ❌ Criticità

**9.1 No Notification Content Extension**
- Le notifiche mostrano solo titolo e body
- No custom UI per chat messages, flares, etc.

**9.2 No Notification Action Handlers**
- No azioni rapide (respond, accept, join)

**9.3 Deep Linking Base**
```swift
// AppRouter.swift - implementazione base
func open(deepLink: String?) {
    guard let url = URL(string: deepLink) else { return }
    // Parse e routing
}
```

**Miglioramento:** Aggiungere stato "unread" e mark-as-read da notifica.

---

## 10. Privacy e Compliance

### ✅ Punti di Forza

**10.1 Privacy Settings UI**
- Ghost mode, share presence, share intentions
- Live location control

### ❌ Criticità

**10.1 No App Privacy Manifest**
- Richiesto da Apple App Store da 2024

**10.2 No Data Export/Delete**
- GDPR richiede data portability e right to be forgotten

**10.3 No Age Verification**
- App potrebbe essere usata da minori

---

## 11. Nuove Integrazioni Mancanti

### 11.1 Analytics

**Stato:** `FeedAnalytics.swift` esiste ma è stubbed

**Raccomandazione:** Integrare Firebase Analytics o Mixpanel
```swift
import FirebaseAnalytics

enum AnalyticsEvent {
    static func login(nickname: String) {
        Analytics.logEvent("user_login", parameters: ["nickname": nickname])
    }
    
    static func viewVenue(venueId: UUID, category: String) {
        Analytics.logEvent("view_venue", parameters: [
            "venue_id": venueId.uuidString,
            "category": category
        ])
    }
}
```

### 11.2 Crash Reporting

**Stato:** Assente

**Raccomandazione:** Sentry o Firebase Crashlytics
```swift
import Sentry

SentrySDK.start { options in
    options.dsn = "https://..."
    options.tracesSampleRate = 0.1
}
```

### 11.3 Performance Monitoring

**Stato:** Assente

**Raccomandazione:** Firebase Performance o Sentry Performance
```swift
import FirebasePerformance

let trace = Performance.startTrace(name: "fetch_venues")
trace?.start()

// ... fetch logic ...

trace?.stop()
```

### 11.4 A/B Testing

**Stato:** Assente

**Raccomandazione:** Firebase Remote Config per feature flags

### 11.5 Background App Refresh

**Stato:** Assente

**Raccomandazione:**
```swift
func application(_ application: UIApplication, performFetchWithCompletionHandler completionHandler: @escaping (UIBackgroundFetchResult) -> Void) {
    Task {
        await fetchNewMessages()
        completionHandler(.newData)
    }
}
```

### 11.6 WidgetKit

**Stato:** Assente

**Raccomandazione:** Widget per home screen con:
- Prossimi eventi/tavoli
- Amici online nelle vicinanze
- Flares attivi

### 11.7 App Clips

**Stato:** Assente

**Raccomandazione:** App Clip per:
- Check-in rapido a venue
- Join tavolo sociale
- Lancio flare temporaneo

---

## 12. Backend Integration Issues

### 12.1 SignalR Hubs Non Usati

**Backend:** `ChatHub.cs` implementato
**Client iOS:** No SignalR client, solo polling

**Impatto:**
- Latenza messaggi 5 secondi
- Battery drain da polling continuo
- Network waste

### 12.2 Redis Non Usato

**Backend:** Redis disponibile via Docker Compose
**Utilizzo:** Nessuno nel codice

**Raccomandazione:**
- Cache venue data
- Rate limiting
- Session storage

### 12.3 Outbox Notifications

**Backend:** `NotificationOutboxService` implementato
**Stato:** Dispatch disabled di default

```json
// appsettings.json
"Notifications": {
    "DispatchEnabled": false,
    "DispatchIntervalSeconds": 30
}
```

---

## 13. File Configuration Mancanti

### 13.1 Info.plist Customizations

**Stato:** File non trovato (potrebbe essere embedded in Xcode project)

**Da verificare:**
- ATS exceptions per localhost development
- Background modes (location, fetch, remote notifications)
- Privacy descriptions (NSLocationWhenInUseUsageDescription, etc.)

### 13.2 Entitlements File

**Stato:** ❌ Non trovato

**Da aggiungere:**
- Associated Domains per Universal Links
- Push Notifications capability
- Sign in with Apple (se implementato)

### 13.3 .xcconfig Files

**Stato:** ❌ Assenti

**Raccomandazione:**
```
Debug.xcconfig:
    API_BASE_URL = http://localhost:8080
    LOG_NETWORK = 1
    ENABLE_DEV_FEATURES = 1

Release.xcconfig:
    API_BASE_URL = https://api.iron-quote.it
    LOG_NETWORK = 0
    ENABLE_DEV_FEATURES = 0
```

---

## 14. Raccomandazioni Prioritarie

### HIGH Priority (Sprint 1-2)

1. **Aggiungere SignalR Client per Chat**
   - Sostituire polling con WebSocket
   - Ridurre latenza da 5s a realtime
   - Migliorare battery life

2. **Implementare Certificate Pinning**
   - Security critica per produzione
   - Prevenire MITM attacks

3. **Aggiungere Core Data per Caching**
   - Offline support base
   - Migliorare perceived performance

4. **Internazionalizzazione (i18n)**
   - Estrarre tutte le stringhe in Localizable.strings
   - Supporto almeno IT/EN

5. **Aggiungere Linting (SwiftLint)**
   - Qualità codice consistente
   - Prevenire code smells

### MEDIUM Priority (Sprint 3-4)

6. **Token Refresh Mechanism**
   - Evitare re-login ogni 7 giorni
   - Refresh token rotation

7. **Analytics Integration**
   - Firebase Analytics o Mixpanel
   - Tracking eventi chiave

8. **Crash Reporting**
   - Sentry o Crashlytics
   - Monitoraggio produzione

9. **Split God Files**
   - Models.swift → per dominio
   - Endpoints.swift → per feature
   - FeedView.swift → componenti più piccoli

10. **CI/CD Pipeline**
    - GitHub Actions per build verification
    - Automated testing

### LOW Priority (Sprint 5+)

11. **Biometric Authentication**
12. **WidgetKit**
13. **App Clips**
14. **Background App Refresh**
15. **A/B Testing Framework**

---

## 15. Codice da Correggere Subito

### 15.1 Chat Polling → SignalR

**File:** `ChatRoomView.swift`

```swift
// ATTUALE (polling)
Task {
    while !Task.isCancelled {
        try? await Task.sleep(nanoseconds: 5_000_000_000)
        await pollThread()
    }
}

// DOVREBBE ESSERE (SignalR)
final class ChatConnection: ObservableObject {
    private var connection: HubConnection?
    
    func connect(threadId: UUID) async throws {
        connection = HubConnectionBuilder(url: baseURL)
            .withAutoReconnect()
            .build()
        
        connection?.on(method: "ReceiveMessage") { [weak self] args in
            Task { @MainActor in
                self?.handleIncomingMessage(args)
            }
        }
        
        try await connection?.start()
        try await connection?.invoke(method: "JoinThread", argument: threadId.uuidString)
    }
}
```

### 15.2 Certificate Pinning

**File:** `APIClient.swift`

```swift
// AGGIUNGERE delegate con pinning
private let session: URLSession

private init() {
    let cfg = URLSessionConfiguration.default
    let delegate = PinnedURLSessionDelegate()
    self.session = URLSession(configuration: cfg, delegate: delegate, delegateQueue: nil)
}
```

### 15.3 Token Refresh

**File:** `AuthStore.swift`

```swift
// AGGIUNGERE refresh token
var refreshToken: String? {
    didSet {
        if let token = refreshToken { Keychain.save(token, for: Keys.refreshToken) }
        else { Keychain.delete(Keys.refreshToken) }
    }
}

func restore() async {
    guard token != nil || refreshToken != nil else {
        state = .loggedOut
        return
    }
    
    do {
        let user = try await API.me()
        state = .loggedIn(user)
    } catch APIError.unauthorized {
        // Try refresh
        if let refreshToken {
            do {
                let newTokens = try await API.refreshAuth(refreshToken: refreshToken)
                token = newTokens.accessToken
                self.refreshToken = newTokens.refreshToken
                state = .loggedIn(try await API.me())
                return
            } catch {}
        }
        // Both failed
        token = nil
        self.refreshToken = nil
        state = .loggedOut
    }
}
```

---

## 16. Conclusioni

Il client iOS **FriendMapSeed** è un'ottima base di partenza con:
- ✅ Architettura moderna e performante
- ✅ Design system coerente e curato
- ✅ Integrazione backend completa
- ✅ CloudyCore package ben testato

Ma per la **produzione** mancano componenti critiche:
- ❌ Sicurezza (certificate pinning, token refresh)
- ❌ Offline support (caching, database locale)
- ❌ Testing (UI test, integration test)
- ❌ i18n (tutto in italiano)
- ❌ Observability (analytics, crash reporting)

**Stima sforzo:** 4-6 sprint (2 settimane ciascuno) per rendere l'app production-ready con tutte le HIGH priority.

---

## Appendice A: File Audit

| File | Linee | Stato | Note |
|------|-------|-------|------|
| `Models.swift` | ~900 | ⚠️ Split | Troppo grande, dividere per dominio |
| `Endpoints.swift` | ~500 | ⚠️ Split | 350+ metodi, dividere per feature |
| `FeedView.swift` | 1024 | ⚠️ Split | View troppo complessa |
| `APIClient.swift` | ~280 | ✅ OK | Ben strutturato |
| `AuthStore.swift` | ~120 | ✅ OK | Manca refresh token |
| `MapStore.swift` | ~180 | ✅ OK | Debounce + Task.detached |
| `ChatRoomView.swift` | ~200 | ❌ Fix | Polling → SignalR |
| `Theme.swift` | ~200 | ✅ OK | Design system coerente |
| `Components.swift` | ~450 | ✅ OK | Componenti riutilizzabili |
| `CloudyCore.swift` | ~180 | ✅ OK | 13 test passing |

---

## Appendice B: Comandi Utili per Sviluppo

```bash
# Build iOS
cd FriendMapSeed
xcodebuild -project FriendMapSeed.xcodeproj \
  -scheme FriendMapSeed \
  -destination 'platform=iOS Simulator,name=iPhone 15' \
  build

# Test CloudyCore
cd FriendMapSeed/Packages/CloudyCore
swift test

# Run su device fisico
./scripts/run-ios-device.sh

# Backend locale
./scripts/run-api-lan.sh

# URL per device fisico
./scripts/dev-api-url.sh
```

---

**Documento preparato da:** Qwen Code  
**Data:** 4 Maggio 2026  
**Branch auditato:** `feat/ios-native-swiftui`
