# FriendMapSeed (Cloudy iOS — Native SwiftUI)

Riscrittura nativa **iOS / SwiftUI** dell'app Cloudy, in sostituzione del client .NET MAUI. Il backend ASP.NET Core (`src/FriendMap.Api`) **non cambia**: l'app iOS lo consuma come API REST.

## Perché questa riscrittura

Il client MAUI aveva problemi di performance e rendering documentati:

- `StartCloudPulse` con timer da 16ms sul main thread per ogni nuvola
- `RenderViewportOverlay` ricreava continuamente `Border` + `Shadow`
- `BuildFogLinks` O(n²) sul main thread
- Accelerometer a `SensorSpeed.Game`
- `MainMapPage.xaml.cs` da 94KB
- Glitch di rendering iOS con stack di `RadialGradientBrush`
- Coordinate non density-aware

La riscrittura nativa elimina queste classi di problemi alla radice.

## Architettura

```
FriendMapSeed/
├── App/                    → AppRouter (@Observable), RootView (auth gate + 5 tab)
├── DesignSystem/           → Theme (palette honey/Bumble + IG gradients), Components
├── Models/                 → Codable mirrors dei DTO backend (PascalCase)
├── Networking/             → APIClient async/await, Endpoints tipizzati, JWT bearer
├── Stores/                 → AuthStore, MapStore (@Observable, debounce 350ms)
├── Features/
│   ├── Auth/               → Dev login (nickname + URL backend configurabile)
│   ├── Map/                → CloudShape, CloudBubble, MapView, VenueDetailSheet
│   ├── Feed/               → Stories ring + feed cards stile Instagram
│   ├── Tables/             → Card stack swipe stile Bumble
│   ├── Profile/
│   └── Common/
├── Utilities/              → Haptics
└── Packages/CloudyCore/    → Swift Package locale, logica pura cross-platform
```

### Pattern chiave

- **MVVM + `@Observable`** (iOS 17+) — niente boilerplate Combine.
- **Una sola `TimelineView(.animation)`** a livello `MapView` produce un `phase 0..1` condiviso da tutte le `CloudBubble` → elimina N timer.
- **CloudShape** è una singola `Path` con stroke/fill nativi (non più stack di `Border` + `RadialGradient`).
- **Fog links** in `Task.detached`, cap a 100 cluster, algoritmo identico al MAUI ma in funzione pura testabile (`CloudyCore.FogLinkBuilder`).
- **MapKit SwiftUI**: `Map(position:)` + `Annotation` + `MapPolyline`.

### CloudyCore (Swift Package)

`Packages/CloudyCore/` contiene la logica di dominio pura, **cross-platform e testabile su Linux**:

- `LatLon`, `Geo.distance` (haversine)
- `VenueClusterInput` protocol → adattabile a `VenueMarker`
- `FogLinkBuilder.build(from:maxDistanceMeters:minIntensity:minStrength:maxClusters:)`
- `CloudyJSON.makeDecoder()` / `makeEncoder()` con keyDecodingStrategy PascalCase + ISO8601 con frazione opzionale

**13 unit test, tutti passano** su Swift 6.0.3 (Linux):

```bash
cd FriendMapSeed/Packages/CloudyCore
swift test
# Executed 13 tests, with 0 failures
```

## Compatibilità con il backend

| Aspetto | Backend (C#) | Client iOS |
|--------|------|--------|
| JSON keys | PascalCase | `keyDecodingStrategy = .custom` (lowercase first) |
| Date | ISO8601, frazione opzionale | due `ISO8601DateFormatter` in cascata |
| Auth | JWT bearer | salvato in Keychain, restore() al boot |
| URL backend | configurabile | `UserDefaults.standard.string(forKey: "backendURL")` |

## Setup locale

1. Aprire `FriendMapSeed.xcodeproj` in Xcode 16+.
2. Selezionare schema `FriendMapSeed`, simulator iPhone 15+.
3. Avviare `dotnet run` su `src/FriendMap.Api/` (default `http://localhost:8080`).
4. Lanciare app → schermata login → nickname + URL backend → "Entra".

### Nota: ATS per HTTP localhost

Il backend di sviluppo gira su `http://localhost:8080`. iOS blocca HTTP cleartext di default. **In dev**, aggiungere a `Info.plist` (oppure via Build Settings → "Custom iOS Target Properties"):

```xml
<key>NSAppTransportSecurity</key>
<dict>
  <key>NSAllowsLocalNetworking</key>
  <true/>
</dict>
```

In produzione il backend dev'essere HTTPS.

## Test

- **Unit (logica)**: 13 test su `CloudyCore` (`swift test`) — `FogLinkBuilder`, `Geo.distance`, JSON PascalCase round-trip, ISO8601 con/senza frazione, performance 60 cluster < 50ms.
- **Build**: parse syntactic Swift su tutti i file iOS (passato).
- **UI smoke test**: TODO — richiede target di test in Xcode (vedi follow-up).

## Trade-off

- ➕ Performance native (60 fps stabili, no jank), API SwiftUI moderne, tooling Xcode.
- ➕ Codice ~5x più piccolo del MAUI (no XAML 94KB, no event handlers stratificati).
- ➖ Si perde Android (consapevole — il backend resta REST, eventuale client Android in futuro).
