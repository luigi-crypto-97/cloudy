#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://0.0.0.0:8080}"
exec ./scripts/run-api.sh
