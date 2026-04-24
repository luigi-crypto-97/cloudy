# 🚀 FriendMap — Launch Checklist

> Questo documento elenca **tutte le configurazioni manuali** necessarie per portare l'app in produzione. Tutt'il codice è pronto, ma questi step richiedono i tuoi account/credenziali.

---

## 1️⃣ Microsoft App Center (Crashlytics + Analytics)

**Perché serve:** Telemetria crash e tracking funnel.

- [ ] Crea un account su [https://appcenter.ms](https://appcenter.ms)
- [ ] Crea una nuova app: **FriendMap.iOS** (platform = iOS, OS = Objective-C/Swift)
- [ ] Prendi la **App Secret** dall'app App Center (es. `12345678-1234-1234-1234-123456789012`)
- [ ] In `FriendMap.Mobile/MauiProgram.cs` sostituisci:
  ```csharp
  const string appSecret = "ios=YOUR_IOS_APP_SECRET;android=YOUR_ANDROID_APP_SECRET";
  ```
  con:
  ```csharp
  const string appSecret = "ios=12345678-1234-1234-1234-123456789012";
  ```
- [ ] (Facoltativo) Aggiungi anche Android secret quando farai il porting.

---

## 2️⃣ Push Notifications — Apple APNs (iOS)

**Perché serve:** Invio push reali (friend request, messaggi, inviti tavolo).

### 2A. Configurazione Apple Developer Portal
- [ ] Vai su [https://developer.apple.com/account](https://developer.apple.com/account)
- [ ] Crea un **Key** per APNs (Certificates, Identifiers & Profiles → Keys → APNs)
- [ ] Scarica il file `.p8` e prendi il **Key ID**
- [ ] Prendi il **Team ID** dalla pagina Membership

### 2B. Configurazione Backend
- [ ] In `FriendMap.Api/appsettings.json` aggiungi la sezione:
  ```json
  "Apns": {
    "TeamId": "TUO_TEAM_ID",
    "KeyId": "TUO_KEY_ID",
    "BundleId": "com.friendmap.mobile",
    "P8FilePath": "AuthKey_XXXXXX.p8"
  }
  ```
- [ ] Copia il file `.p8` nella cartella di esecuzione del backend (o in un volume Docker)
- [ ] Verifica che `FriendMap.Api/Services/ApnsClient.cs` punti al path corretto.

### 2C. Configurazione Mobile
- [ ] In `FriendMap.Mobile/Platforms/iOS/Info.plist` verifica che esista:
  ```xml
  <key>UIBackgroundModes</key>
  <array>
    <string>remote-notification</string>
  </array>
  ```
- [ ] In `AppDelegate.cs` la riga `#if FRIENDMAP_APNS_ENABLED` deve essere attivata rimuovendo il commento o definendo il simbolo di compilazione.

---

## 3️⃣ Database Migrations (Nuove tabelle)

**Perché serve:** Le nuove feature (Stories, Achievements) richiedono tabelle nel DB.

- [ ] Nel terminale, nella cartella `FriendMap.Api`:
  ```bash
  cd /Users/luiginegri/Documents/cloudy/src/FriendMap.Api
  dotnet ef migrations add ViralFeatures --context AppDbContext
  dotnet ef database update --context AppDbContext
  ```
- [ ] Se `dotnet ef` non è installato:
  ```bash
  dotnet tool install --global dotnet-ef
  ```

---

## 4️⃣ Rate Limiting / Abuse Prevention (Già attivo)

**Stato:** ✅ Il codice è già deployato. Non serve configurazione manuale.

- 100 req/min per utente/IP (globale)
- 30 req/min su endpoint social/messaggi

---

## 5️⃣ SignalR Real-Time Chat (Già attivo)

**Stato:** ✅ Il codice è già deployato.

- Il client si connette automaticamente a `<BASE_URL>/hubs/chat`
- Verifica che il backend sia raggiungibile dalla rete del telefono (non localhost)

---

## 6️⃣ iOS Permissions (Contact Picker + Location)

**Perché serve:** L'invite page accede ai contatti nativi.

- [ ] In `FriendMap.Mobile/Platforms/iOS/Info.plist` aggiungi:
  ```xml
  <key>NSContactsUsageDescription</key>
  <string>FriendMap usa i contatti per trovare amici e invitarli.</string>
  ```
- [ ] Verifica che esista già:
  ```xml
  <key>NSLocationWhenInUseUsageDescription</key>
  <string>...</string>
  ```

---

## 7️⃣ App Store / TestFlight

### 7A. Firma e Provisioning
- [ ] In `FriendMap.Mobile.csproj` verifica:
  ```xml
  <CodesignKey>Apple Development: TUO_NOME</CodesignKey>
  <CodesignProvision>...</CodesignProvision>
  ```
- [ ] Per distribuzione TestFlight, passa a **Distribution** certificate e provisioning profile.

### 7B. Privacy Manifest (iOS 17+)
- [ ] Verifica che `PrivacyInfo.xcprivacy` esista in `Platforms/iOS/Resources/` con le dichiarazioni per:
  - NSPrivacyAccessedAPICategoryDiskSpace (se usi cache)
  - NSPrivacyAccessedAPICategoryUserDefaults (se usi Preferences)

### 7C. App Store Review Info
- [ ] Compila la sezione **App Review Information** con:
  - Demo account credentials (se usi dev-login, creane uno di test)
  - Contact information

---

## 8️⃣ Deep Linking (Già registrato)

**Stato:** ✅ Schema `friendmap://` registrato in `Info.plist` e `AppDelegate.cs`.

- [ ] Per condivisione via web (es. `https://friendmap.app/venue/xyz`), configura **Universal Links** nel Apple Developer portal e nel backend (file `apple-app-site-association`).

---

## 9️⃣ Backend Deployment

### 9A. Porte
- [ ] API: `http://localhost:8080` (o `https://api.friendmap.app` in produzione)
- [ ] Admin: `http://localhost:8090` (o `https://admin.friendmap.app`)

### 9B. Environment Variables
- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] `ConnectionStrings__Postgres=Host=...;Database=friendmap;...`
- [ ] `Jwt__SigningKey` → genera una chiave lunga casuale (min 32 chars)

### 9C. SSL / HTTPS
- [ ] Usa un reverse proxy (Nginx / Caddy / Cloudflare) con certificato TLS valido.
- [ ] Aggiorna `ApiBaseUrlKey` nel mobile con l'URL HTTPS pubblico.

---

## 🔟 Post-Launch Monitoraggio

- [ ] Controlla **App Center Crashes** dopo i primi 48h
- [ ] Controlla **App Center Analytics** per eventi:
  - `login`, `check_in`, `send_message`, `friend_request`, `share`, `invite`
- [ ] Controlla i log del backend (`Serilog` in console/file)
- [ ] Verifica che le push notification arrivino (friend request → badge count)

---

## 📱 Tabella Riassuntiva

| # | Task | Stato | Dove si configura |
|---|------|-------|-------------------|
| 1 | App Center Secret | ⏳ Manuale | `MauiProgram.cs` |
| 2 | APNs certificati `.p8` | ⏳ Manuale | `appsettings.json` + Apple Dev Portal |
| 3 | EF Migrations | ⏳ Manuale | `dotnet ef migrations add` |
| 4 | Rate limiting | ✅ Automatico | — |
| 5 | SignalR | ✅ Automatico | — |
| 6 | iOS Permissions | ⏳ Manuale | `Info.plist` |
| 7 | App Store / TestFlight | ⏳ Manuale | Xcode / App Store Connect |
| 8 | Deep links | ✅ Automatico | — |
| 9 | Backend deploy | ⏳ Manuale | Server / Docker / Cloud |
| 10 | Monitoraggio | ⏳ Manuale | App Center + backend logs |

---

**Ultimo aggiornamento:** 2026-04-24
