# FriendMap Starter Code Pack

Starter kit per FriendMap: backend ASP.NET Core, database PostgreSQL/PostGIS,
admin panel Blazor Server e shell mobile .NET MAUI iOS-first.

## Stato attuale

Ambiente locale supportato:

- backend ASP.NET Core avviabile su `http://localhost:8080`
- PostgreSQL/PostGIS e Redis via Docker Compose
- admin panel Blazor Server avviabile su `http://localhost:8090`
- EF Core migrations applicate dal bootstrap
- admin protetto da cookie login dev
- dashboard, moderazione e CRUD venue collegati alle API reali
- endpoint check-in, intenzioni, tavoli sociali e device token notifiche
- dev-login utente app con JWT bearer
- outbox notifiche e client APNs HTTP/2 token-based
- Swagger backend su `http://localhost:8080/swagger`

La parte mobile MAUI resta iOS-first e richiede workload/Xcode separati.

## Prerequisiti

- .NET SDK 8
- Docker Desktop o Docker Engine con Docker Compose
- macOS/Linux shell per usare gli script in `scripts/`

Verifica rapida:

```bash
dotnet --info
docker compose version
```

## Bootstrap locale

Dalla root del repository:

```bash
./scripts/bootstrap-dev.sh
```

Lo script:

1. avvia PostgreSQL/PostGIS e Redis;
2. esegue `dotnet restore` per backend e admin;
3. ripristina i local tool .NET;
4. applica le migrations EF Core al database;
5. compila backend e admin.

Non esegue restore/build della solution completa perché include il progetto MAUI
iOS, che richiede il workload `maui`.

## Avvio passo per passo

### 1. Avvia database e cache

```bash
docker compose -f infra/docker-compose.yml up -d postgres redis
```

PostgreSQL:

- host: `localhost`
- porta: `5432`
- database: `friendmap`
- user: `friendmap`
- password: `friendmap_dev`

Lo schema database e gestito da EF Core migrations. Lo script SQL idempotente
generato dalle migrations e disponibile in `sql/migrations.sql`.

Per ricreare il database da zero:

```bash
docker compose -f infra/docker-compose.yml down -v
./scripts/bootstrap-dev.sh
```

Attenzione: `down -v` elimina i dati locali del database.

### 2. Avvia backend

```bash
./scripts/run-api.sh
```

Verifica:

```bash
curl http://localhost:8080/health
```

Endpoint utili:

- `http://localhost:8080/health`
- `http://localhost:8080/health/db`
- `http://localhost:8080/swagger`
- `POST http://localhost:8080/api/auth/dev-login`
- `http://localhost:8080/api/venues/map?minLat=45.40&minLng=9.10&maxLat=45.55&maxLng=9.30`
- `http://localhost:8080/api/admin/dashboard`

Dev-login app:

```bash
curl -sS -X POST http://localhost:8080/api/auth/dev-login \
  -H 'Content-Type: application/json' \
  -d '{"nickname":"giulia","displayName":"Giulia Dev"}'
```

Usa `accessToken` come bearer token per check-in, intenzioni, tavoli sociali e
notifiche:

```bash
curl -H "Authorization: Bearer <token>" http://localhost:8080/api/auth/me
```

Se Swagger risponde con pagina bianca o `404`, l'API e probabilmente partita
senza ambiente `Development`. Ferma il vecchio processo e riavvia con:

```bash
./scripts/run-api.sh
```

### 3. Avvia admin panel

In un secondo terminale:

```bash
./scripts/run-admin.sh
```

Apri:

```text
http://localhost:8090
```

Credenziali dev:

- username: `admin`
- password: `admin_dev`

Se lo script dice che l'admin e gia in esecuzione, non e un errore: apri
direttamente `http://localhost:8090`. Per vedere chi usa la porta:

```bash
lsof -nP -iTCP:8090 -sTCP:LISTEN
```

Per fermare l'admin locale:

```bash
./scripts/stop-admin.sh
```

### 4. Avvio completo via Docker Compose

In alternativa puoi avviare anche API e admin dentro container SDK:

```bash
docker compose -f infra/docker-compose.yml up
```

Servizi:

- API: `http://localhost:8080`
- Admin: `http://localhost:8090`
- Postgres: `localhost:5432`
- Redis: `localhost:6379`

## Struttura

- `src/FriendMap.Api`: backend ASP.NET Core Minimal APIs + EF Core
- `src/FriendMap.Admin`: admin panel Blazor Server
- `src/FriendMap.Mobile`: shell .NET MAUI iOS-first
- `infra/docker-compose.yml`: servizi locali
- `sql/migrations.sql`: script SQL idempotente generato dalle migrations
- `sql/schema.sql`: riferimento SQL legacy, non usato dal bootstrap
- `docs/`: note architetturali e specifiche starter

## Note tecniche

- Il backend usa EF Core con naming convention `snake_case` per allinearsi allo
  schema SQL.
- La tabella utenti è mappata esplicitamente su `app_users`.
- Il campo venue `location` usa PostGIS `geography(point,4326)`.
- La mappa usa bounding box PostGIS con `ST_Intersects` e indice GIST.
- Redis è disponibile nell'ambiente locale ma non è ancora usato dal codice
  applicativo.
- L'admin usa cookie login dev. L'autenticazione utente app/JWT non è ancora
  implementata per produzione, ma esiste un dev-login JWT su `/api/auth/dev-login`.
- Le notifiche usano outbox DB. L'invio APNs reale e implementato ma disattivato
  di default. Configura `Apns__Enabled=true`, `Apns__TeamId`, `Apns__KeyId`,
  `Apns__BundleId` e `Apns__PrivateKeyPath` o `Apns__PrivateKey`.
- Il client iOS richiede workload `maui-ios`. Se manca:

```bash
dotnet workload restore src/FriendMap.Mobile/FriendMap.Mobile.csproj
```

Su macOS può richiedere privilegi elevati.
