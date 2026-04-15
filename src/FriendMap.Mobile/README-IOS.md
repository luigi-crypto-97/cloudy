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

La pagina mappa usa `Microsoft.Maui.Controls.Maps` con:
- mappa nativa iOS in stile stradale
- pin venue
- cerchi geospaziali proporzionali all'affluenza
- overlay visuale con bolle numeriche blu

## Integrazioni da completare
1. UI check-in / intenzione / tavolo sociale.
2. Gestione permessi location foreground.
3. Integrazione permessi push e token APNs lato iOS.
4. Login Apple.
5. Deep links per social table / venue.

## Build

Richiede workload iOS MAUI:

```bash
dotnet workload restore src/FriendMap.Mobile/FriendMap.Mobile.csproj
```

Poi:

```bash
dotnet build src/FriendMap.Mobile/FriendMap.Mobile.csproj -f net8.0-ios
```

## Pattern UI richiesto
- base map chiara
- niente satellite
- pin venue sostituiti da nuvolette blu o pillole con numero
- intensità cromatica crescente
- tap su nuvoletta => bottom sheet venue
