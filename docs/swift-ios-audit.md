# Audit client iOS Swift

Data audit: 2026-04-27.

## Stato progetto

Il client iOS nativo SwiftUI vive in `FriendMapSeed/` e usa il backend
ASP.NET Core esistente. La vecchia app MAUI resta nel repository, ma per iOS
la strada operativa e:

```bash
./scripts/run-ios-device.sh
```

Stack verificato:

- app iOS SwiftUI: `FriendMapSeed/FriendMapSeed`
- package locale condiviso: `FriendMapSeed/Packages/CloudyCore`
- backend API: `src/FriendMap.Api`
- database: PostgreSQL/PostGIS via Docker Compose
- cache: Redis via Docker Compose

## Funzioni sistemate in questo passaggio

- Notifiche: aggiunto endpoint backend `GET /api/notifications`, usato dalla
  schermata notifiche Swift.
- Push iOS: aggiunta registrazione APNs lato app e invio token al backend con
  `POST /api/notifications/device-tokens`.
- Chat e messaggistica tavoli: aggiunto polling automatico ogni 5 secondi per
  aggiornare i thread senza dover rientrare nella schermata.
- Story: aggiunto upload immagini multipart lato iOS e backend
  `POST /api/stories/media`.
- Story creation: il backend ora restituisce una `UserStory` completa, cosi il
  decode Swift non fallisce dopo la creazione.
- Avatar profilo: aggiunto picker foto in Swift e upload verso
  `POST /api/users/me/avatar`.
- Tavoli sociali: corretta join table quando il client non passa esplicitamente
  `userId`; il backend usa l'utente autenticato.
- Venue detail: i pulsanti check-in e pianifica serata ora chiamano davvero le
  API invece di essere segnaposto.
- Interessi: aggiunto deck swipe stile Tinder nella modifica profilo per
  accettare/scartare interessi suggeriti.
- Posizione live: aggiunto tracking iOS attivabile dalla mappa. Il backend
  aggiorna un check-in automatico temporaneo sul locale piu vicino, senza
  pubblicare coordinate precise.
- Stories: aggiunto scatto da fotocamera, permesso camera e campo titolo.
- Flare: dopo il lancio compare subito sulla mappa con effetto burst animato.
- Mappa: i cluster densi ora usano aree aggregate e marker compatti; le
  nuvolette pesanti restano solo quando ci sono pochi locali visibili.

## Verifiche eseguite

Backend:

```bash
dotnet build src/FriendMap.Api/FriendMap.Api.csproj --no-restore
```

Esito: build riuscita, 0 warning, 0 errori.

iOS Swift:

```bash
xcodebuild -project FriendMapSeed/FriendMapSeed.xcodeproj \
  -scheme FriendMapSeed \
  -destination 'generic/platform=iOS Simulator' \
  -derivedDataPath FriendMapSeed/build \
  CODE_SIGNING_ALLOWED=NO \
  build
```

Esito: build riuscita.

## Comandi per avviare backend e servizi

Dalla root repository:

```bash
cd /Users/luiginegri/Documents/cloudy
```

Avvio database e cache:

```bash
docker compose -f infra/docker-compose.yml up -d postgres redis
```

Avvio backend locale:

```bash
./scripts/run-api.sh
```

Avvio backend raggiungibile da iPhone sulla stessa rete:

```bash
./scripts/stop-api.sh
./scripts/run-api-lan.sh
./scripts/dev-api-url.sh
```

Avvio admin:

```bash
./scripts/run-admin.sh
```

URL utili:

- API: `http://localhost:8080`
- Swagger: `http://localhost:8080/swagger`
- Admin: `http://localhost:8090`

## Comandi per installare l'app iOS Swift

Lista device e device name iPhone:

```bash
xcrun devicectl list devices
```

Installazione automatica sul primo iPhone disponibile:

```bash
./scripts/run-ios-device.sh
```

Installazione su un device specifico:

```bash
./scripts/run-ios-device.sh IDENTIFIER-IPHONE
```

Per provare posizione live e camera su iPhone fisico:

```bash
docker compose -f infra/docker-compose.yml up -d postgres redis
./scripts/run-api-lan.sh
./scripts/run-ios-device.sh
```

Poi nell'app:

- login;
- apri Mappa;
- premi il pulsante `location` in alto a destra per attivare la posizione live;
- premi `+` nelle stories e usa `Scatta adesso`;
- lancia un flare dalla mappa: vedrai l'effetto animato nel punto corrente.

Se serve cambiare team o bundle id:

```bash
DEVELOPMENT_TEAM=9YUM32FPQU \
BUNDLE_ID=it.luiginegri.FriendMapSeed \
./scripts/run-ios-device.sh IDENTIFIER-IPHONE
```

## Stop completo

Ferma API e admin avviati con script:

```bash
./scripts/stop-api.sh
./scripts/stop-admin.sh
```

Ferma Docker mantenendo i dati:

```bash
docker compose -f infra/docker-compose.yml down
```

Reset database locale, distruttivo:

```bash
docker compose -f infra/docker-compose.yml down -v
./scripts/bootstrap-dev.sh
```

## Limiti residui

- Le push vere richiedono signing Apple, provisioning con capability push,
  configurazione APNs backend e bundle id coerente. Il codice ora registra il
  token, ma l'invio reale dipende dalla configurazione Apple.
- Chat e tavoli ora si aggiornano con polling. Per una chat veramente realtime
  conviene aggiungere SignalR o WebSocket anche al client Swift.
- L'upload immagini non comprime ancora automaticamente file grandi e non fa
  conversione HEIC lato client.
- Le notifiche non hanno ancora stato letto/non letto e deep link automatico
  verso chat, tavoli o venue.
- Il deck interessi e una prima UX funzionante; si puo migliorare salvando
  suggerimenti remoti o personalizzati per zona.
