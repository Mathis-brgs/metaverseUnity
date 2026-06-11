#!/usr/bin/env bash
# À exécuter sur le VPS Ubuntu après extraction de metaverse-server-linux64.tar.gz
set -euo pipefail

INSTALL_DIR="${INSTALL_DIR:-/opt/metaverse-server}"
SERVICE_NAME="metaverse"

if [[ "$(id -u)" -ne 0 ]]; then
  echo "Relance avec sudo : sudo bash install-vps.sh"
  exit 1
fi

if [[ ! -x "$INSTALL_DIR/MetaverseServer" ]]; then
  echo "Erreur : $INSTALL_DIR/MetaverseServer introuvable ou non exécutable."
  echo "Extrais d'abord l'archive : tar -xzf metaverse-server-linux64.tar.gz -C $INSTALL_DIR"
  exit 1
fi

echo "→ Installation des dépendances système…"
apt-get update -qq
apt-get install -y libglib2.0-0 libstdc++6 libgcc-s1 \
  libx11-6 libxcursor1 libxrandr2 libxi6

chmod +x "$INSTALL_DIR/MetaverseServer"

echo "→ Ouverture des ports 25000/tcp et 25001/udp…"
if command -v ufw >/dev/null 2>&1; then
  ufw allow 25000/tcp
  ufw allow 25001/udp
  ufw --force enable 2>/dev/null || true
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"

cat > "$SERVICE_FILE" <<EOF
[Unit]
Description=MetaVerse Unity Dedicated Server
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
WorkingDirectory=${INSTALL_DIR}
ExecStart=${INSTALL_DIR}/MetaverseServer -batchmode -nographics -logFile /var/log/metaverse-server.log
Restart=always
RestartSec=5
LimitNOFILE=65535

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable "$SERVICE_NAME"
systemctl restart "$SERVICE_NAME"

echo ""
echo "✓ Serveur installé dans $INSTALL_DIR"
echo "  Statut  : systemctl status $SERVICE_NAME"
echo "  Logs    : journalctl -u $SERVICE_NAME -f"
echo "  Fichier : tail -f /var/log/metaverse-server.log"
echo ""
echo "Clients Unity → IP publique du VPS, TCP 25000, UDP 25001"
