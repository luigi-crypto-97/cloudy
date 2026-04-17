#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

DESIRED_URLS="${ASPNETCORE_URLS:-http://127.0.0.1:8080}"
DB_HOST="${FRIENDMAP_DB_HOST:-127.0.0.1}"
DB_PORT="${FRIENDMAP_DB_PORT:-5432}"

is_db_reachable() {
  if command -v pg_isready >/dev/null 2>&1 && pg_isready -h "$DB_HOST" -p "$DB_PORT" -q >/dev/null 2>&1; then
    return 0
  fi

  if command -v nc >/dev/null 2>&1 && nc -z "$DB_HOST" "$DB_PORT" >/dev/null 2>&1; then
    return 0
  fi

  if [[ "$DB_PORT" == "5432" ]] &&
     [[ "$DB_HOST" == "127.0.0.1" || "$DB_HOST" == "localhost" ]] &&
     command -v docker >/dev/null 2>&1 &&
     docker info >/dev/null 2>&1 &&
     docker ps --format '{{.Names}} {{.Ports}}' | grep -Eq '^friendmap-postgres .*0\.0\.0\.0:5432->5432/tcp'; then
    return 0
  fi

  return 1
}

if [[ "${SKIP_DB_PREFLIGHT:-0}" != "1" ]]; then
  if ! is_db_reachable; then
    echo "PostgreSQL is not reachable at ${DB_HOST}:${DB_PORT}." >&2
    echo >&2

    if command -v docker >/dev/null 2>&1 && ! docker info >/dev/null 2>&1; then
      echo "Docker is installed but the daemon is not running." >&2
      echo "Start Docker Desktop first, then run:" >&2
    else
      echo "Start the local development services first:" >&2
    fi

    echo "  ./scripts/bootstrap-dev.sh" >&2
    echo >&2
    echo "Then retry:" >&2
    echo "  ./scripts/run-api.sh" >&2
    echo "  ./scripts/run-api-lan.sh" >&2
    echo >&2
    echo "If you are using a custom PostgreSQL instance, make sure it is reachable on ${DB_HOST}:${DB_PORT}" >&2
    echo "or export FRIENDMAP_DB_HOST / FRIENDMAP_DB_PORT before running this script." >&2
    exit 1
  fi
fi

if lsof -nP -iTCP:8080 -sTCP:LISTEN >/dev/null 2>&1; then
  LISTENERS="$(lsof -nP -iTCP:8080 -sTCP:LISTEN || true)"

  if [[ "$DESIRED_URLS" == *"0.0.0.0:8080"* ]] && echo "$LISTENERS" | grep -q "127.0.0.1:8080"; then
    echo "Port 8080 is already used by a localhost-only instance of the API." >&2
    echo "Stop it first, then restart in LAN mode:" >&2
    echo "  ./scripts/stop-api.sh" >&2
    echo "  ./scripts/run-api-lan.sh" >&2
    exit 1
  fi

  if curl -fsS http://127.0.0.1:8080/health >/dev/null 2>&1; then
    if [[ "$DESIRED_URLS" == *"0.0.0.0:8080"* ]]; then
      echo "FriendMap API already appears to be running and reachable on port 8080."
    else
      echo "FriendMap API already appears to be running at http://localhost:8080"
    fi
    exit 0
  fi

  echo "Port 8080 is already in use by another process:" >&2
  echo "$LISTENERS" >&2
  echo >&2
  echo "Stop that process or run the API on another port." >&2
  exit 1
fi

ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS="$DESIRED_URLS" \
  dotnet run --no-launch-profile --project src/FriendMap.Api/FriendMap.Api.csproj
