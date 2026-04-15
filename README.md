# FriendMap Starter Code Pack

Pacchetto starter generato per il progetto di app social map-first.

## Contenuto
- `src/FriendMap.Api`: backend ASP.NET Core + EF Core + OpenAPI
- `src/FriendMap.Admin`: pannello admin Blazor Server starter
- `src/FriendMap.Mobile`: shell .NET MAUI iOS-first
- `infra/docker-compose.yml`: stack locale e deployment base
- `infra/nginx/friendmap.conf`: reverse proxy
- `infra/proxmox/DEPLOYMENT.md`: note operative Proxmox
- `sql/schema.sql`: schema iniziale PostgreSQL
- `docs/openapi-summary.md`: endpoint principali
- `docs/decisions.md`: decisioni architetturali
- `docs/privacy-aggregation.md`: regole tecniche di aggregazione

## Nota importante
Questo non è "codice nascosto recuperato": è uno **starter kit nuovo**, coerente con le specifiche tecniche e pronto da usare con Codex/AI coding per accelerare lo sviluppo.

## Stack
- Backend: ASP.NET Core Minimal APIs
- DB: PostgreSQL + PostGIS
- Cache: Redis
- Admin: Blazor Server
- Mobile: .NET MAUI (iOS-first)
- Reverse proxy: Nginx
- Hosting: VM Linux su Proxmox

## Flusso consigliato
1. Aprire la solution in Visual Studio.
2. Far completare a Codex i TODO indicati nei file.
3. Agganciare PostgreSQL/PostGIS e Redis.
4. Implementare autenticazione reale (JWT + Apple Sign In/OTP).
5. Integrare provider mappe vettoriali non satellitari.
6. Implementare moderazione, audit e notifiche push.
