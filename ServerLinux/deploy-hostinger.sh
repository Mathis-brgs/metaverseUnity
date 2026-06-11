#!/usr/bin/env bash
# Build Unity (Linux dedicated server) + archive + déploiement VPS Hostinger en une commande.
#
# Usage :
#   ./ServerLinux/deploy-hostinger.sh 187.124.45.244
#   METAVERSE_VPS_HOST=187.124.45.244 ./ServerLinux/deploy-hostinger.sh
#
# Config persistante (optionnel) : copier deploy.env.example → deploy.env
# Build déjà fait : SKIP_BUILD=1 ./ServerLinux/deploy-hostinger.sh <IP>
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

if [[ -f "$SCRIPT_DIR/deploy.env" ]]; then
  # shellcheck source=/dev/null
  source "$SCRIPT_DIR/deploy.env"
elif [[ -f "$SCRIPT_DIR/.env" ]]; then
  # shellcheck source=/dev/null
  source "$SCRIPT_DIR/.env"
fi

VPS_HOST="${1:-${METAVERSE_VPS_HOST:-}}"
VPS_USER="${METAVERSE_VPS_USER:-root}"
VPS_PORT="${METAVERSE_VPS_SSH_PORT:-22}"
INSTALL_DIR="${METAVERSE_INSTALL_DIR:-/opt/metaverse-server}"
SKIP_BUILD="${SKIP_BUILD:-0}"
ARCHIVE="$SCRIPT_DIR/metaverse-server-linux64.tar.gz"

usage() {
  cat <<EOF
Usage: $0 <IP_VPS>

Variables d'environnement :
  METAVERSE_VPS_HOST     IP du VPS (alternative à l'argument)
  METAVERSE_VPS_USER     Utilisateur SSH (défaut: root)
  METAVERSE_VPS_SSH_PORT Port SSH (défaut: 22)
  METAVERSE_INSTALL_DIR  Dossier sur le VPS (défaut: /opt/metaverse-server)
  METAVERSE_SSH_KEY      Clé SSH (optionnel)
  UNITY_PATH             Binaire Unity (auto-détecté via Hub sinon)
  SKIP_BUILD=1           Ignore le build Unity (archive existante requise)
  SKIP_SSH_CHECK=1       Ignore le test de port avant build

Exemple :
  $0 187.124.45.244
  cp ServerLinux/deploy.env.example ServerLinux/deploy.env && $0
EOF
}

find_unity() {
  if [[ -n "${UNITY_PATH:-}" && -x "$UNITY_PATH" ]]; then
    echo "$UNITY_PATH"
    return
  fi

  local hub="/Applications/Unity/Hub/Editor"
  if [[ ! -d "$hub" ]]; then
    return
  fi

  local version_dir unity_bin
  for version_dir in $(ls -1 "$hub" 2>/dev/null | sort -Vr); do
    unity_bin="$hub/$version_dir/Unity.app/Contents/MacOS/Unity"
    if [[ -x "$unity_bin" ]]; then
      echo "$unity_bin"
      return
    fi
  done
}

is_unity_project_locked() {
  if [[ -f "$ROOT/Temp/UnityLockfile" ]]; then
    return 0
  fi
  if pgrep -fl "Unity.*${ROOT}" >/dev/null 2>&1; then
    return 0
  fi
  return 1
}

has_local_build() {
  [[ -x "$ROOT/MetaverseServer" && -d "$ROOT/MetaverseServer_Data" ]]
}

ssh_opts() {
  SSH_OPTS=(
    -o ConnectTimeout=15
    -o ServerAliveInterval=5
    -p "$VPS_PORT"
  )
  SCP_OPTS=(
    -o ConnectTimeout=15
    -o ServerAliveInterval=5
    -P "$VPS_PORT"
  )
  if [[ -n "${METAVERSE_SSH_KEY:-}" ]]; then
    SSH_OPTS+=(-i "$METAVERSE_SSH_KEY")
    SCP_OPTS+=(-i "$METAVERSE_SSH_KEY")
  fi
}

# Teste que le port TCP répond (sans auth — BatchMode bloque les clés avec passphrase).
check_tcp_port() {
  if command -v nc >/dev/null 2>&1; then
    nc -z -w 5 "$VPS_HOST" "$VPS_PORT" 2>/dev/null
    return
  fi
  (echo >/dev/tcp/"$VPS_HOST"/"$VPS_PORT") 2>/dev/null
}

preflight_ssh_or_exit() {
  echo "→ Test connectivité ${VPS_USER}@${VPS_HOST}:${VPS_PORT}…"
  if check_tcp_port; then
    echo "  ✓ Port $VPS_PORT joignable"
    echo "  (scp/ssh demanderont la passphrase de ta clé si besoin — comme un ssh manuel)"
    return 0
  fi

  echo ""
  echo "✗ Impossible de joindre le VPS sur le port $VPS_PORT (timeout / firewall)."
  echo ""
  echo "Vérifications Hostinger (hPanel) :"
  echo "  1. VPS en ligne (Overview)"
  echo "  2. Security → Firewall : autoriser TCP 22 (SSH) depuis ton IP ou « Any »"
  echo "  3. Autoriser aussi TCP 25000 et UDP 25001 (jeu)"
  echo "  4. Terminal navigateur hPanel → sur le VPS :"
  echo "       sudo ufw allow 22/tcp && sudo ufw allow 25000/tcp && sudo ufw allow 25001/udp"
  echo "       sudo ufw reload"
  echo ""
  echo "Si ssh manuel fonctionne mais pas ce script :"
  echo "  - Lance d'abord : ssh-add ~/.ssh/id_ed25519   (charge la clé dans l'agent)"
  echo "  - Ou ignore le test : SKIP_SSH_CHECK=1 $0 $VPS_HOST"
  echo ""
  if [[ -f "$ARCHIVE" ]]; then
    echo "Archive prête (déploiement manuel) :"
    echo "  $ARCHIVE"
    echo ""
    echo "Depuis le terminal navigateur Hostinger (après upload de l'archive dans /opt/) :"
    echo "  mkdir -p $INSTALL_DIR"
    echo "  systemctl stop metaverse 2>/dev/null || true"
    echo "  tar -xzf /opt/metaverse-server-linux64.tar.gz -C $INSTALL_DIR"
    echo "  chmod +x $INSTALL_DIR/MetaverseServer"
    echo "  systemctl restart metaverse || bash /opt/install-vps.sh"
  fi
  exit 1
}

if [[ -z "$VPS_HOST" ]]; then
  usage
  exit 1
fi

mkdir -p "$ROOT/Logs"

# Tester SSH avant un long build Unity
if [[ "${SKIP_SSH_CHECK:-0}" != "1" ]]; then
  preflight_ssh_or_exit
fi

if [[ "$SKIP_BUILD" != "1" ]]; then
  if is_unity_project_locked; then
    if has_local_build; then
      echo "⚠ Unity Editor a ce projet ouvert — build batch impossible."
      echo "  → Build local trouvé : déploiement sans rebuild (SKIP_BUILD=1)"
      SKIP_BUILD=1
    else
      echo "Erreur : Unity Editor a ce projet ouvert."
      echo ""
      echo "Unity n'autorise qu'une seule instance par projet."
      echo "  1. Ferme l'éditeur Unity, puis relance :"
      echo "       $0 $VPS_HOST"
      echo "  2. Ou build d'abord via MetaVerse → Server → Build Dedicated Server,"
      echo "     puis déploie sans rebuild :"
      echo "       SKIP_BUILD=1 $0 $VPS_HOST"
      exit 1
    fi
  fi
fi

if [[ "$SKIP_BUILD" != "1" ]]; then
  UNITY_BIN="$(find_unity || true)"
  if [[ -z "$UNITY_BIN" ]]; then
    echo "Erreur : Unity introuvable."
    echo "  - Installe Unity Hub + module Linux Build Support"
    echo "  - ou exporte UNITY_PATH=/chemin/vers/Unity"
    echo "  - ou build manuellement puis SKIP_BUILD=1 $0 $VPS_HOST"
    exit 1
  fi

  echo "→ Build Unity serveur dédié (5–15 min)…"
  echo "  Unity : $UNITY_BIN"
  echo "  Log   : $ROOT/Logs/deploy-build.log"

  "$UNITY_BIN" \
    -quit -batchmode -nographics \
    -projectPath "$ROOT" \
    -executeMethod ServerBuildMenu.BuildDedicatedServerHeadless \
    -logFile "$ROOT/Logs/deploy-build.log"
else
  echo "→ SKIP_BUILD=1 : build Unity ignoré"
fi

echo "→ Empaquetage…"
chmod +x "$SCRIPT_DIR/package.sh"
"$SCRIPT_DIR/package.sh"

echo "→ Envoi vers ${VPS_USER}@${VPS_HOST}:${VPS_PORT}…"
echo "  (passphrase clé SSH possible — comme pour ssh root@${VPS_HOST})"
ssh_opts
scp "${SCP_OPTS[@]}" "$ARCHIVE" "${VPS_USER}@${VPS_HOST}:/opt/metaverse-server-linux64.tar.gz"
scp "${SCP_OPTS[@]}" "$SCRIPT_DIR/install-vps.sh" "${VPS_USER}@${VPS_HOST}:/opt/install-vps.sh"

echo "→ Installation / redémarrage sur le VPS…"
ssh "${SSH_OPTS[@]}" "${VPS_USER}@${VPS_HOST}" "INSTALL_DIR='$INSTALL_DIR' bash -s" <<'REMOTE'
set -euo pipefail
INSTALL_DIR="${INSTALL_DIR:-/opt/metaverse-server}"

mkdir -p "$INSTALL_DIR"
systemctl stop metaverse 2>/dev/null || true
tar -xzf /opt/metaverse-server-linux64.tar.gz -C "$INSTALL_DIR"
chmod +x "$INSTALL_DIR/MetaverseServer"

if [[ -f /etc/systemd/system/metaverse.service ]]; then
  systemctl daemon-reload
  systemctl restart metaverse
else
  INSTALL_DIR="$INSTALL_DIR" bash /opt/install-vps.sh
fi

echo ""
systemctl status metaverse --no-pager -l || true
REMOTE

echo ""
echo "✓ Serveur déployé sur $VPS_HOST"
echo "  Clients Unity → IP $VPS_HOST | TCP 25000 | UDP 25001"
echo "  Logs VPS      → ssh ${VPS_USER}@${VPS_HOST} journalctl -u metaverse -f"
