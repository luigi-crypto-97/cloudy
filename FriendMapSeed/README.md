# Cloudy iOS — FriendMapSeed

Client iOS nativo SwiftUI per Cloudy/FriendMap.

## Requisiti

- **macOS:** 14.0+
- **Xcode:** 16.2+
- **iOS:** 17.0+
- **Swift:** 6.0

## Setup Rapido

### 1. Clona e Risolvi Dipendenze

```bash
cd FriendMapSeed
xcodebuild -resolvePackageDependencies
```

### 2. Configura Ambiente

Copia e modifica le xcconfig se necessario:

```bash
# Debug.xcconfig - per sviluppo locale
API_BASE_URL = http://localhost:8080
ENABLE_LOGGING = 1
ENABLE_ANALYTICS = 0

# Release.xcconfig - per produzione
API_BASE_URL = https://api.iron-quote.it
ENABLE_LOGGING = 0
ENABLE_ANALYTICS = 1
```

### 3. Avvia da Xcode

1. Apri `FriendMapSeed.xcodeproj`
2. Seleziona schema `FriendMapSeed`
3. Scegli device (simulatore o iPhone fisico)
4. Premi ▶️ Run

### 4. Script da Terminale

```bash
# Build simulatore
./scripts/run-ios-device.sh

# Build device fisico
./scripts/run-ios-device.sh <device-identifier>
```

## Architettura

```
FriendMapSeed/
├── App/                        # App entry point, routing
│   ├── FriendMapSeedApp.swift
│   ├── AppRouter.swift
│   └── RootView.swift
├── Data/                       # Core Data stack e caching
│   ├── DataController.swift
│   └── CloudyModel+*.swift
├── DesignSystem/               # Theme, componenti UI
│   ├── Theme.swift
│   ├── Components.swift
│   └── Motion.swift
├── Features/                   # Feature-based organization
│   ├── Auth/
│   ├── Chat/
│   ├── Feed/
│   ├── Friends/
│   ├── Map/
│   ├── Profile/
│   └── Tables/
├── Models/                     # Domain models (split per dominio)
│   ├── Models+Auth.swift
│   ├── Models+Venue.swift
│   ├── Models+Social.swift
│   └── Models+Feed.swift
├── Networking/                 # API client, SignalR, Endpoints
│   ├── APIClient.swift         # HTTP con certificate pinning
│   ├── SignalRService.swift    # WebSocket per chat real-time
│   ├── Endpoints+Auth.swift
│   ├── Endpoints+Venue.swift
│   ├── Endpoints+Social.swift
│   ├── Endpoints+Feed.swift
│   └── Endpoints+Chat.swift
├── Stores/                     # State management (@Observable)
│   ├── AuthStore.swift
│   ├── MapStore.swift
│   └── LiveLocationStore.swift
├── Utilities/                  # Helpers
│   ├── L10n.swift              # Localizzazioni type-safe
│   ├── Analytics.swift         # Firebase Analytics wrapper
│   ├── ImageCache.swift        # Nuke image caching
│   └── Haptics.swift
└── Resources/                  # Localizzazioni, asset
    ├── Localizable.strings (it)
    └── Localizable+en.strings (en)
```

## Feature Implementate

### Sicurezza
- ✅ **Certificate Pinning** per prevenire MITM attacks
- ✅ **Token Refresh** automatico con retry delle richieste
- ✅ **Biometric Authentication** (FaceID/TouchID)
- ✅ **Keychain** per token sensibili

### Offline Support
- ✅ **Core Data** caching per venues, messaggi, storie
- ✅ **Offline Queue** per messaggi inviati senza connessione
- ✅ **Cache cleanup** automatico (7 giorni)

### Networking
- ✅ **APIClient** con logging opzionale, errori localizzati
- ✅ **SignalR** per chat real-time (no polling)
- ✅ **Endpoints** suddivisi per feature

### Internazionalizzazione
- ✅ **Italiano** e **Inglese** (~200 chiavi)
- ✅ **L10n helper** type-safe

### Performance
- ✅ **Nuke** per image caching avanzato
- ✅ **Prefetching** per immagini in liste
- ✅ **Debounced fetch** per viewport mappa (350ms)
- ✅ **Task.detached** per fog links calculation

### Analytics & Monitoring
- ✅ **Firebase Analytics** integration
- ✅ **Crashlytics** per crash reporting
- ✅ **Event tracking** per azioni utente

### CI/CD
- ✅ **GitHub Actions** per build e test automatici
- ✅ **SwiftLint** per code quality
- ✅ **Security workflow** per dependency audit

## Configurazione Firebase

1. Crea progetto su [Firebase Console](https://console.firebase.google.com)
2. Scarica `GoogleService-Info.plist`
3. Aggiungi a `FriendMapSeed/` in Xcode
4. Abilita Analytics e Crashlytics
5. Imposta in `Release.xcconfig`:
   ```
   ENABLE_FIREBASE = 1
   ```

## Configurazione Certificate Pinning

Per produzione, genera l'hash del certificato:

```bash
openssl s_client -servername api.iron-quote.it -connect api.iron-quote.it:443 < /dev/null 2>/dev/null | \
  openssl x509 -pubkey -noout | \
  openssl pkey -pubin -outform der | \
  openssl dgst -sha256 -binary | \
  openssl enc -base64
```

Copia l'hash in `APIClient.swift`:

```swift
private static let pinnedPublicKeyHashes: Set<String> = [
    "Base64HashQui="
]
```

## Testing

### Unit Test (CloudyCore)

```bash
cd FriendMapSeed/Packages/CloudyCore
swift test
```

### Build Verification

```bash
xcodebuild -project FriendMapSeed.xcodeproj \
  -scheme FriendMapSeed \
  -destination 'platform=iOS Simulator,name=iPhone 15' \
  build
```

### SwiftLint

```bash
cd FriendMapSeed
swiftlint lint
```

## Dipendenze

Il progetto usa Swift Package Manager:

- **SignalR-Client-Swift** (≥ 0.12.0) - Chat real-time
- **Nuke** (≥ 12.8.0) - Image caching
- **firebase-ios-sdk** (≥ 11.0.0) - Analytics, Crashlytics
- **sentry-cocoa** (≥ 8.30.0) - Error monitoring (opzionale)
- **SwiftLint** (≥ 0.57.0) - Linting

Per risolvere dipendenze:

```bash
xcodebuild -resolvePackageDependencies
```

## Ambiente di Sviluppo

### Variabili d'Ambiente (Debug)

| Variabile | Descrizione | Default |
|-----------|-------------|---------|
| `API_BASE_URL` | Backend URL | `http://localhost:8080` |
| `ENABLE_FIREBASE` | Abilita Firebase | `0` |
| `ENABLE_ANALYTICS` | Abilita analytics | `0` |
| `LOG_NETWORK_REQUESTS` | Log HTTP requests | `1` |
| `LOG_VERBOSE` | Log verboso | `1` |

### Backend Locale

Per sviluppare con backend locale:

```bash
# Root repository
./scripts/run-api-lan.sh

# Ottieni URL LAN
./scripts/dev-api-url.sh

# Imposta in Debug.xcconfig
API_BASE_URL = http://192.168.x.x:8080
```

## Struttura Database

Il caching offline usa Core Data con queste entità:

- **VenueCache** - Locali visitati
- **MessageCache** - Messaggi chat
- **QueuedMessage** - Messaggi in attesa (offline)
- **StoryCache** - Stories visualizzate
- **UserProfileCache** - Profili utente

## Linee Guida di Sviluppo

### Codice

- Usa `@Observable` per state management (iOS 17+)
- Preferisci `async/await` a callback
- Usa `L10n` per stringhe localizzate
- Usa `CachedImage` invece di `AsyncImage`
- Mantieni views < 300 righe (splitta se necessario)

### Git

```bash
# Branch naming
feat/nome-feature
fix/nome-fix
refactor/nome-refactor

# Commit message
feat: aggiunto caching offline per venues
fix: corretto crash in chat room
refactor: split Models.swift in file per dominio
```

### Review Checklist

- [ ] SwiftLint: nessun warning/error
- [ ] Test CloudyCore: passano
- [ ] Build: compila senza errori
- [ ] Localizzazioni: stringhe in L10n/Localizable
- [ ] Errori: messaggi localizzati (L10n.Error.*)

## Risoluzione Problemi

### Build Fallisce

```bash
# Pulisci derived data
rm -rf ~/Library/Developer/Xcode/DerivedData/*

# Risolvi pacchetti
xcodebuild -resolvePackageDependencies

# Restart Xcode
```

### Certificati/Signing

```bash
# Verifica team
defaults read com.apple.dt.Xcode IDEProvisioningTeams

# Reset signing
xcodebuild -project FriendMapSeed.xcodeproj \
  -scheme FriendMapSeed \
  CODE_SIGNING_ALLOWED=NO \
  build
```

### Core Data Migration

Se cambi il modello Core Data:

1. Incrementa `CURRENT_PROJECT_VERSION`
2. Crea nuova versione modello in Xcode
3. Abilita migration automatica

## Licenza

Proprietario — Tutti i diritti riservati.

## Contatti

- **Sviluppatore:** Luigi Negri
- **Email:** api@iron-quote.it
- **Backend:** https://api.iron-quote.it

---

**Ultimo aggiornamento:** 4 Maggio 2026  
**Versione:** 1.0.0  
**Build:** 1
