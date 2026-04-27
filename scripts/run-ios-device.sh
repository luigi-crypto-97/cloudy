#!/usr/bin/env bash
#
# Build & install del client iOS nativo (SwiftUI) su iPhone fisico.
# Sostituisce il vecchio scripts/run-mobile-device.sh basato su .NET MAUI.
#
# Prerequisiti (una volta sola):
#   - Xcode 16+ installato in /Applications/Xcode.app
#   - sudo xcode-select -s /Applications/Xcode.app/Contents/Developer
#   - Apple ID gia configurato in Xcode -> Settings -> Accounts (team 9YUM32FPQU
#     o cambia DEVELOPMENT_TEAM via env var)
#   - iPhone collegato via USB, sbloccato, "Modalita sviluppatore" attiva
#   - "Autorizza questo computer" gia accettato
#
# Uso:
#   ./scripts/run-ios-device.sh                      # auto-detect device
#   ./scripts/run-ios-device.sh IDENTIFIER-IPHONE    # device specifico
#
# Variabili opzionali:
#   DEVELOPMENT_TEAM   default: 9YUM32FPQU
#   BUNDLE_ID          default: it.luiginegri.FriendMapSeed
#   CONFIGURATION      default: Debug
#

set -euo pipefail

cd "$(dirname "$0")/.."

PROJECT="FriendMapSeed/FriendMapSeed.xcodeproj"
SCHEME="FriendMapSeed"
CONFIGURATION="${CONFIGURATION:-Debug}"
DEVELOPMENT_TEAM="${DEVELOPMENT_TEAM:-9YUM32FPQU}"
BUNDLE_ID="${BUNDLE_ID:-it.luiginegri.FriendMapSeed}"
DERIVED_DATA="$(pwd)/FriendMapSeed/build"

echo "==> Devices iOS connessi:"
xcrun devicectl list devices || true
echo

# 1. Device id: argomento CLI, oppure auto-detect del primo "available (paired)"
DEVICE_ID="${1:-}"
if [[ -z "$DEVICE_ID" ]]; then
  DEVICE_ID="$(
    xcrun devicectl list devices 2>/dev/null \
      | awk '/available \(paired\)/ {print $NF; exit}'
  )" || true
fi

if [[ -z "$DEVICE_ID" ]]; then
  cat <<EOF
ERRORE: nessun iPhone collegato e abbinato.

Checklist:
  - cavo USB collegato
  - iPhone sbloccato, schermo acceso
  - "Autorizza questo computer" accettato sull'iPhone
  - Impostazioni iPhone -> Privacy e sicurezza -> Modalita sviluppatore = ON
  - dopo l'attivazione della Modalita sviluppatore l'iPhone va riavviato

Poi rilancia questo script.
EOF
  exit 1
fi
echo "==> Userò device id: $DEVICE_ID"
echo

# 2. Risolvi i package SPM (CloudyCore) prima del build
echo "==> Risolvo package SPM..."
xcodebuild \
  -project "$PROJECT" \
  -scheme "$SCHEME" \
  -derivedDataPath "$DERIVED_DATA" \
  -resolvePackageDependencies \
  -quiet

# 3. Build per device fisico (arm64), firmato con automatic signing
echo "==> Build $CONFIGURATION per device $DEVICE_ID..."
xcodebuild \
  -project "$PROJECT" \
  -scheme "$SCHEME" \
  -configuration "$CONFIGURATION" \
  -destination "id=$DEVICE_ID" \
  -derivedDataPath "$DERIVED_DATA" \
  -allowProvisioningUpdates \
  CODE_SIGN_STYLE=Automatic \
  DEVELOPMENT_TEAM="$DEVELOPMENT_TEAM" \
  PRODUCT_BUNDLE_IDENTIFIER="$BUNDLE_ID" \
  build

# 4. Localizza l'.app costruita
APP_PATH="$(
  /usr/bin/find "$DERIVED_DATA/Build/Products/${CONFIGURATION}-iphoneos" \
    -maxdepth 2 -name '*.app' -type d | head -n1
)"
if [[ -z "$APP_PATH" || ! -d "$APP_PATH" ]]; then
  echo "ERRORE: non trovo l'.app buildata in $DERIVED_DATA/Build/Products/${CONFIGURATION}-iphoneos/"
  exit 1
fi
echo "==> App: $APP_PATH"

# 5. Install + launch via devicectl (Xcode 15+)
echo "==> Installazione su device..."
xcrun devicectl device install app --device "$DEVICE_ID" "$APP_PATH"

echo "==> Avvio app..."
xcrun devicectl device process launch \
  --device "$DEVICE_ID" \
  --console \
  "$BUNDLE_ID" || {
    echo
    echo "Avvio fallito (spesso 'Locked' = iPhone bloccato a schermo)."
    echo "Sblocca l'iPhone e rilancia con:"
    echo "  xcrun devicectl device process launch --device $DEVICE_ID $BUNDLE_ID"
  }
