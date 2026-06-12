#!/usr/bin/env bash
# Empaquette le build Linux Unity pour déploiement VPS (Hostinger, etc.)
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="$(cd "$(dirname "$0")" && pwd)"
STAGING="$OUT_DIR/staging"
ARCHIVE="$OUT_DIR/metaverse-server-linux64.tar.gz"

FILES=(
  MetaverseServer
  MetaverseServer_Data
  UnityPlayer.so
  libdecor-0.so.0
  libdecor-cairo.so
)

echo "→ Build source : $ROOT"
for f in "${FILES[@]}"; do
  if [[ ! -e "$ROOT/$f" ]]; then
    echo "Erreur : $ROOT/$f introuvable. Lance d'abord MetaVerse/Server/Build Dedicated Server."
    exit 1
  fi
done

rm -rf "$STAGING"
mkdir -p "$STAGING"
for f in "${FILES[@]}"; do
  cp -R "$ROOT/$f" "$STAGING/"
done
chmod +x "$STAGING/MetaverseServer"

tar -czf "$ARCHIVE" -C "$STAGING" .
rm -rf "$STAGING"

SIZE=$(du -h "$ARCHIVE" | cut -f1)
echo "✓ Archive créée : $ARCHIVE ($SIZE)"
echo ""
echo "Envoi sur le VPS :"
echo "  scp $ARCHIVE root@TON_IP:/opt/"
echo ""
echo "Sur le VPS :"
echo "  mkdir -p /opt/metaverse-server && tar -xzf /opt/metaverse-server-linux64.tar.gz -C /opt/metaverse-server"
echo "  bash /opt/metaverse-server/install-vps.sh   # si copié avec le repo, sinon voir README"
