# Note iOS / MAUI

## Stato
Shell pronta per iOS-first, con mappa nativa iOS e overlay affluenza iniziale.

Il client API contiene gia i metodi per:
- dev-login JWT
- marker mappa
- check-in
- intenzioni
- creazione tavoli sociali
- registrazione token notifiche iOS
- backend URL configurabile dalla schermata login

La pagina mappa usa `Microsoft.Maui.Controls.Maps` con:
- mappa nativa iOS in stile stradale
- pin venue
- cerchi geospaziali proporzionali all'affluenza
- overlay visuale con bolle numeriche blu
- pannello venue sopra la mappa con azioni check-in, intenzione e tavolo sociale

## Integrazioni da completare
1. Bottom sheet venue completo con dettaglio locale.
2. Flussi dedicati per orario intenzione e parametri tavolo sociale.
3. Login Apple.
4. Deep links per social table / venue.

## Permessi iOS

Sono presenti:
- `Platforms/iOS/Info.plist` con descrizioni location e remote notifications
- `Platforms/iOS/Entitlements.plist` con `aps-environment=development`
- `Platforms/iOS/AppDelegate.cs` per catturare il token APNs e salvarlo in `Preferences`
- `ApnsDeviceTokenStore` per registrare il token sul backend quando iOS lo consegna

La mappa richiede `LocationWhenInUse`; le notifiche richiedono autorizzazione
alert/badge/sound e registrazione APNs.

Le APNs reali sono disattivate di default nelle build locali. Si attivano solo
con:

```bash
-p:EnablePushEntitlements=true
```

## Build

Richiede Xcode completo e workload iOS MAUI. Le sole Command Line Tools non
bastano.

Verifica:

```bash
xcode-select -p
```

Deve puntare a:

```text
/Applications/Xcode.app/Contents/Developer
```

Se punta a `/Library/Developer/CommandLineTools`, correggi con:

```bash
sudo xcode-select -s /Applications/Xcode.app/Contents/Developer
sudo xcodebuild -license accept
sudo xcodebuild -runFirstLaunch
```

Poi ripristina workload e pacchetti:

```bash
dotnet workload restore src/FriendMap.Mobile/FriendMap.Mobile.csproj
dotnet restore src/FriendMap.Mobile/FriendMap.Mobile.csproj
```

Build simulator senza firma APNs, utile per verificare C#/XAML mentre Xcode o
il provisioning sono ancora in setup:

```bash
dotnet build src/FriendMap.Mobile/FriendMap.Mobile.csproj -f net8.0-ios -p:RuntimeIdentifier=iossimulator-x64 -p:EnableCodeSigning=false --no-restore
```

Build device con entitlement APNs:

```bash
dotnet build src/FriendMap.Mobile/FriendMap.Mobile.csproj -f net8.0-ios -p:EnablePushEntitlements=true
```

Su Mac Apple Silicon usa `iossimulator-arm64` al posto di `iossimulator-x64`.
La build firmata con push reale richiede un provisioning profile Apple per
`com.friendmap.mobile` con Push Notifications abilitate.

## Flusso dev senza push

Backend:

```bash
./scripts/bootstrap-dev.sh
./scripts/run-api.sh
```

App su simulatore:
- lascia `Backend URL` su `http://127.0.0.1:8080/`
- esegui login dev

App su iPhone fisico:

```bash
./scripts/run-api-lan.sh
./scripts/dev-api-url.sh
```

Inserisci nell'app il valore stampato da `dev-api-url.sh`, ad esempio:

```text
http://192.168.1.23:8080/
```

Serve che Mac e iPhone siano sulla stessa rete Wi-Fi.

## Installazione su iPhone

Senza Apple Developer Program a pagamento non puoi usare APNs reali, ma puoi
comunque provare l'app con Personal Team.

Passi pratici:

1. In Xcode aggiungi il tuo Apple ID e verifica che compaia un `Personal Team`.
2. Collega l'iPhone al Mac e autorizza il dispositivo.
3. Fai generare a Xcode i signing assets development del tuo account.
4. Compila l'app senza `EnablePushEntitlements`.
5. Avvia API su LAN e usa nell'app l'URL del Mac.

Nota: per il deployment CLI MAUI su iPhone, i certificati e il provisioning
development devono gia esistere nel Mac. Questa e una deduzione pratica dai
requisiti Apple/Xcode e dal comportamento del toolchain .NET iOS.

## Pattern UI richiesto
- base map chiara
- niente satellite
- pin venue sostituiti da nuvolette blu o pillole con numero
- intensità cromatica crescente
- tap su nuvoletta => bottom sheet venue
