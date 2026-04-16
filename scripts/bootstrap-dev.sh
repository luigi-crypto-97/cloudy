#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK 8 is required but was not found in PATH." >&2
  exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker is required but was not found in PATH." >&2
  exit 1
fi

if docker compose version >/dev/null 2>&1; then
  COMPOSE=(docker compose)
elif command -v docker-compose >/dev/null 2>&1; then
  COMPOSE=(docker-compose)
else
  echo "Docker Compose is required but was not found." >&2
  exit 1
fi

if ! docker info >/dev/null 2>&1; then
  cat >&2 <<'ERROR'
Docker is installed, but the Docker daemon is not reachable.

Start Docker Desktop, wait until it reports that Docker is running, then retry:
  ./scripts/bootstrap-dev.sh

On macOS you can usually start it from Applications or with:
  open -a Docker

ERROR
  exit 1
fi

echo "Starting PostgreSQL/PostGIS and Redis..."
"${COMPOSE[@]}" -f infra/docker-compose.yml up -d postgres redis

echo "Waiting for PostgreSQL..."
for attempt in {1..30}; do
  if docker exec friendmap-postgres pg_isready -U friendmap -d friendmap >/dev/null 2>&1; then
    break
  fi

  if [ "$attempt" -eq 30 ]; then
    echo "PostgreSQL did not become ready in time." >&2
    exit 1
  fi

  sleep 1
done

echo "Restoring backend packages..."
dotnet restore src/FriendMap.Api/FriendMap.Api.csproj

echo "Restoring admin packages..."
dotnet restore src/FriendMap.Admin/FriendMap.Admin.csproj

echo "Restoring local dotnet tools..."
dotnet tool restore

echo "Applying EF Core migrations..."
if ! dotnet dotnet-ef database update --project src/FriendMap.Api/FriendMap.Api.csproj; then
  cat >&2 <<'MIGRATION_ERROR'

EF migration failed. If this database was created by the older sql/schema.sql
bootstrap, reset the local development volume and retry:
  docker compose -f infra/docker-compose.yml down -v
  ./scripts/bootstrap-dev.sh

MIGRATION_ERROR
  exit 1
fi

echo "Building backend..."
dotnet build src/FriendMap.Api/FriendMap.Api.csproj --no-restore

echo "Building admin..."
dotnet build src/FriendMap.Admin/FriendMap.Admin.csproj --no-restore

cat <<'NEXT_STEPS'

Development bootstrap completed.

Run the backend:
  ./scripts/run-api.sh

Run the backend on LAN for a physical iPhone:
  ./scripts/run-api-lan.sh

Run the admin panel in another terminal:
  ./scripts/run-admin.sh

Print the backend URL to insert in the iPhone app:
  ./scripts/dev-api-url.sh

Open:
  API health: http://localhost:8080/health
  Swagger:    http://localhost:8080/swagger
  Admin:      http://localhost:8090

NEXT_STEPS
