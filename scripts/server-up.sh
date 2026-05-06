#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if docker compose version >/dev/null 2>&1; then
  COMPOSE=(docker compose)
elif command -v docker-compose >/dev/null 2>&1; then
  COMPOSE=(docker-compose)
else
  echo "Docker Compose non trovato. Installa Docker Engine con plugin compose." >&2
  exit 1
fi

if ! docker info >/dev/null 2>&1; then
  echo "Docker non e' in esecuzione o l'utente non ha permessi sul socket Docker." >&2
  echo "Su Ubuntu di solito: sudo usermod -aG docker \$USER && newgrp docker" >&2
  exit 1
fi

if [[ ! -f ".env.local" ]]; then
  echo "Manca .env.local." >&2
  echo "Crea il file partendo da .env.local.example e inserisci i valori reali:" >&2
  echo "  cp .env.local.example .env.local" >&2
  echo "  nano .env.local" >&2
  exit 1
fi

set -a
# shellcheck disable=SC1091
source ".env.local"
set +a

echo "Avvio Cloudy server stack..."
"${COMPOSE[@]}" --env-file .env.local -f infra/docker-compose.yml up -d postgres redis api admin

echo "Attendo API..."
for _ in {1..45}; do
  if curl -fsS http://127.0.0.1:8080/health >/dev/null 2>&1; then
    break
  fi
  sleep 2
done

echo
echo "Stack avviato."
echo "API health:    http://127.0.0.1:8080/health"
echo "API Swagger:   http://127.0.0.1:8080/swagger"
echo "Admin console: http://127.0.0.1:8090"
echo
echo "Log live:"
echo "  docker compose -f infra/docker-compose.yml logs -f api admin"
