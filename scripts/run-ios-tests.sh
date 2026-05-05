#!/bin/bash
set -e

cd "$(dirname "$0")/../FriendMapSeed" || exit

echo "📦 Risoluzione dipendenze in corso..."
xcodebuild -resolvePackageDependencies || {
  echo "⚠️ Attenzione: Impossibile scaricare le dipendenze in automatico."
}

# Trova automaticamente l'ID del primo simulatore iPhone disponibile
SIM_ID=$(xcrun simctl list devices available | grep "iPhone" | grep -v "Watch" | grep -v "iPad" | head -n 1 | sed -E 's/.* \(([A-F0-9-]+)\).*/\1/')

if [ -z "$SIM_ID" ]; then
    echo "❌ Errore: Nessun simulatore iPhone trovato nel sistema."
    exit 1
fi

echo "🚀 Avvio della Test Suite sul Simulatore ($SIM_ID)..."
xcodebuild test \
  -project FriendMapSeed.xcodeproj \
  -scheme FriendMapSeed \
  -destination "platform=iOS Simulator,id=$SIM_ID" \
  CODE_SIGNING_ALLOWED=NO
