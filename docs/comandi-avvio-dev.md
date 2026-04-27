# FriendMap - comandi di avvio dev

Runbook rapido per far partire tutto in locale da root repository:

```bash
cd /Users/luiginegri/Documents/cloudy
```

## Audit rapido progetto

Stack rilevato:

- solution: `FriendMap.sln`
- backend: `src/FriendMap.Api` su .NET 8 / ASP.NET Core Minimal API
- admin: `src/FriendMap.Admin` su .NET 8 / Blazor Server
- mobile: `src/FriendMap.Mobile` su .NET MAUI iOS
- database/cache: PostgreSQL/PostGIS e Redis via `infra/docker-compose.yml`
- tool locale: `dotnet-ef` 8.0.8 in `.config/dotnet-tools.json`

Controlli eseguiti:

```bash
docker compose -f infra/docker-compose.yml config --quiet
dotnet restore src/FriendMap.Api/FriendMap.Api.csproj
dotnet build src/FriendMap.Api/FriendMap.Api.csproj --no-restore
dotnet restore src/FriendMap.Admin/FriendMap.Admin.csproj
dotnet build src/FriendMap.Admin/FriendMap.Admin.csproj --no-restore
```

Esito audit del 2026-04-26: compose valido, API e Admin compilano senza warning/errori. Non risultano progetti test nella solution.

## Prerequisiti

```bash
dotnet --info
docker compose version
xcode-select -p
```

Per iOS, `xcode-select -p` deve puntare a:

```text
/Applications/Xcode.app/Contents/Developer
```

Se punta alle sole Command Line Tools:

```bash
sudo xcode-select -s /Applications/Xcode.app/Contents/Developer
sudo xcodebuild -license accept
sudo xcodebuild -runFirstLaunch
```

## Bootstrap completo locale

Avvia Postgres/Redis, ripristina pacchetti e tool, applica migrations EF, builda API/Admin:

```bash
./scripts/bootstrap-dev.sh
```

## Avvio locale consigliato

Terminale 1, database e cache:

```bash
docker compose -f infra/docker-compose.yml up -d postgres redis
```

Terminale 2, API:

```bash
./scripts/run-api.sh
```

Terminale 3, Admin:

```bash
./scripts/run-admin.sh
```

URL:

- API health: `http://localhost:8080/health`
- DB health: `http://localhost:8080/health/db`
- Swagger: `http://localhost:8080/swagger`
- Admin: `http://localhost:8090`

Credenziali admin dev:

```text
username: admin
password: admin_dev
```

## Avvio tutto via Docker

Avvia Postgres, Redis, API e Admin in container:

```bash
docker compose -f infra/docker-compose.yml up
```

Oppure in background:

```bash
docker compose -f infra/docker-compose.yml up -d
```

Log:

```bash
docker compose -f infra/docker-compose.yml logs -f api
docker compose -f infra/docker-compose.yml logs -f admin
```

Stop:

```bash
docker compose -f infra/docker-compose.yml down
```

Reset database locale, distruttivo:

```bash
docker compose -f infra/docker-compose.yml down -v
./scripts/bootstrap-dev.sh
```

## Verifiche rapide

```bash
curl http://localhost:8080/health
curl http://localhost:8080/health/db
curl -I http://localhost:8090
```

Dev login API:

```bash
curl -sS -X POST http://localhost:8080/api/auth/dev-login \
  -H 'Content-Type: application/json' \
  -d '{"nickname":"giulia","displayName":"Giulia Dev"}'
```

Chi usa una porta:

```bash
lsof -nP -iTCP:8080 -sTCP:LISTEN
lsof -nP -iTCP:8090 -sTCP:LISTEN
```

Stop script locali:

```bash
./scripts/stop-api.sh
./scripts/stop-admin.sh
```

## Import automatico attività commerciali

Il backend supporta import venue da Foursquare Places API tramite admin.
Non serve MongoDB: le venue sono salvate in PostgreSQL/PostGIS nella tabella
`venues`, con deduplica tramite `ExternalProviderId = foursquare:{fsq_id}`.

Configura la chiave API Foursquare prima di avviare il backend. Per le nuove
Service API Key usa l'endpoint nuovo Foursquare Places API:

```bash
export Foursquare__ApiKey="LA_TUA_API_KEY"
export Foursquare__BaseUrl="https://places-api.foursquare.com/"
export Foursquare__SearchPath="places/search"
export Foursquare__AuthorizationScheme="Bearer"
export Foursquare__ApiVersion="2025-02-05"
./scripts/run-api.sh
```

Se stai usando una vecchia API key legacy, usa invece:

```bash
export Foursquare__ApiKey="LA_TUA_API_KEY"
export Foursquare__BaseUrl="https://api.foursquare.com/v3/"
export Foursquare__SearchPath="places/search"
export Foursquare__AuthorizationScheme="ApiKey"
export Foursquare__ApiVersion="1970-01-01"
./scripts/run-api.sh
```

Oppure per avvio LAN:

```bash
export Foursquare__ApiKey="LA_TUA_API_KEY"
./scripts/run-api-lan.sh
```

Poi apri l'admin:

```text
http://localhost:8090/venues
```

Flusso:

1. imposta query, coordinate, raggio e limite;
2. premi `Preview`;
3. controlla risultati e duplicati;
4. premi `Importa preview`.

Gli import nuovi entrano di default con `VisibilityStatus = review`, così puoi
controllarli prima di renderli pubblici. Le venue gia claimed non vengono
sovrascritte dall'import automatico.

### Import gratuito OpenStreetMap / Overpass

Per una prova senza costi puoi usare OpenStreetMap tramite Overpass API. Non
serve API key.

Avvia normalmente backend e admin:

```bash
./scripts/run-api.sh
./scripts/run-admin.sh
```

Poi apri:

```text
http://localhost:8090/venues
```

Nella sezione `Import venue` usa:

- `Preview OSM` per vedere cosa verrebbe importato;
- `Importa OSM` per salvare le venue viste in preview.

Puoi compilare `Area / Comune` con testo, senza coordinate:

```text
Tradate
Milano Brera
Varese centro
Como
Paris
Nice
Monaco
```

Il campo `Paesi ricerca` restringe Nominatim per codice paese ISO-2:

```text
it       Italia
fr       Francia
mc       Monaco
it,fr,mc Italia + Francia + Monaco
```

Se `Area / Comune` e vuoto, vengono usate latitudine, longitudine e raggio.
Il campo `Filtro nome` restringe i risultati dopo la ricerca: lascialo vuoto
se vuoi importare molte attivita dell'area.

Categorie OSM incluse di default:

```text
restaurant, bar, cafe, pub, fast_food, biergarten, ice_cream,
casino, gambling,
fitness_centre, sports_centre, adult_gaming_centre, amusement_arcade,
shop=bakery|coffee|confectionery|pastry|wine|alcohol|beverages|deli
```

L'import non prende alberghi, supermercati, negozi generici, uffici o artigiani.
Il limite massimo UI/backend e 500 risultati per import.

Deduplica:

- ID provider OSM: `osm:{node|way|relation}:{id}`;
- stesso nome normalizzato entro circa 80 metri da una venue esistente;
- risultati duplicati nella stessa preview vengono compressi per nome.

I casino sono inclusi con `amenity=casino` e tag affini come
`amenity=gambling`, `leisure=adult_gaming_centre`, `leisure=amusement_arcade`.

Nota licenza: i dati OSM richiedono attribuzione OpenStreetMap e rispetto della
licenza ODbL. Per produzione aggiungi attribution visibile nell'app o nelle
note legali.

## API raggiungibile da iPhone fisico

Mac e iPhone devono stare sulla stessa rete Wi-Fi.

```bash
./scripts/stop-api.sh
./scripts/run-api-lan.sh
```

In un altro terminale:

```bash
./scripts/dev-api-url.sh
```

Inserisci nell'app l'URL stampato, per esempio:

```text
http://192.168.1.23:8080/
```

Verifica da Safari su iPhone:

```text
http://IP-DEL-MAC:8080/health
```

## Pubblicare API su api.iron-quote.it

Nel repo c'e un `Caddyfile` che pubblica:

```text
api.iron-quote.it -> 127.0.0.1:8080
```

Quindi prima deve essere attiva l'API locale:

```bash
docker compose -f infra/docker-compose.yml up -d postgres redis
./scripts/run-api.sh
```

Poi, in un altro terminale, avvia Caddy con la config del repo:

```bash
sudo caddy run --config Caddyfile --adapter caddyfile
```

Se vuoi lasciarlo in background:

```bash
sudo caddy start --config Caddyfile --adapter caddyfile
```

Reload dopo modifiche al `Caddyfile`:

```bash
sudo caddy reload --config Caddyfile --adapter caddyfile
```

Stop:

```bash
sudo caddy stop
```

Verifiche:

```bash
curl http://127.0.0.1:8080/health
curl https://api.iron-quote.it/health
```

Nota: `api.iron-quote.it` deve puntare all'IP pubblico statico del router, e le porte 80/443 devono arrivare alla macchina che esegue Caddy. Con IPv4 statico Iliad l'IP pubblico previsto e `82.225.152.69`.

Record DNS richiesto:

```text
api.iron-quote.it  A  82.225.152.69
```

Se la API locale risponde ma il dominio no, il problema e Caddy spento, DNS/IP non aggiornato o port forwarding/firewall.

### Iliadbox / Freebox

Impostazioni da fare sulla Iliadbox:

1. Prenota un IP locale fisso per il Mac/server, per esempio `192.168.1.50`.
2. Apri il pannello Iliadbox/Freebox OS.
3. Vai in gestione porte / reindirizzamento porte.
4. Aggiungi due regole TCP:

```text
TCP 80  esterna -> 192.168.1.50 porta 80
TCP 443 esterna -> 192.168.1.50 porta 443
```

Non aprire `8080` verso internet: resta interna tra Caddy e API.

Con IPv4 statico/full stack le porte `80` e `443` devono essere apribili. Se la Freebox continua a imporre un range alto, la configurazione full stack non e ancora attiva o non e stata applicata al router.

## Mobile iOS / MAUI

Ripristino workload e pacchetti:

```bash
dotnet workload restore src/FriendMap.Mobile/FriendMap.Mobile.csproj
dotnet restore src/FriendMap.Mobile/FriendMap.Mobile.csproj
```

Build simulatore Intel:

```bash
dotnet build src/FriendMap.Mobile/FriendMap.Mobile.csproj \
  -f net8.0-ios \
  -p:RuntimeIdentifier=iossimulator-x64 \
  -p:EnableCodeSigning=false
```

Build simulatore Apple Silicon:

```bash
dotnet build src/FriendMap.Mobile/FriendMap.Mobile.csproj \
  -f net8.0-ios \
  -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:EnableCodeSigning=false
```

Build device senza push:

```bash
dotnet build src/FriendMap.Mobile/FriendMap.Mobile.csproj \
  -f net8.0-ios \
  -p:RuntimeIdentifier=ios-arm64
```

Build device con entitlements push, richiede provisioning Apple valido:

```bash
dotnet build src/FriendMap.Mobile/FriendMap.Mobile.csproj \
  -f net8.0-ios \
  -p:RuntimeIdentifier=ios-arm64 \
  -p:EnablePushEntitlements=true
```

## Trovare il device name / identifier iPhone

Comando principale:

```bash
xcrun devicectl list devices
```

Usa la colonna `Identifier` del dispositivo `available (paired)`.

Filtro utile:

```bash
xcrun devicectl list devices | grep -i "available"
```

Fallback storico Xcode:

```bash
xcrun xctrace list devices
```

Avvio sul device con lo script del repo:

```bash
./scripts/run-mobile-device.sh IDENTIFIER-IPHONE
```

Esempio:

```bash
./scripts/run-mobile-device.sh 1CADFC22-687B-529A-8B50-045514D95C55
```

Lo script stampa comunque la lista device prima di buildare:

```bash
./scripts/run-mobile-device.sh
```

## Comandi utili EF Core

Ripristina tool:

```bash
dotnet tool restore
```

Applica migrations:

```bash
dotnet dotnet-ef database update --project src/FriendMap.Api/FriendMap.Api.csproj
```

Rigenera script SQL idempotente:

```bash
dotnet dotnet-ef migrations script \
  --idempotent \
  --project src/FriendMap.Api/FriendMap.Api.csproj \
  --output sql/migrations.sql
```
