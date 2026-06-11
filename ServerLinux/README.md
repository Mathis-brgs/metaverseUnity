# Déploiement serveur MetaVerse (VPS Hostinger)

## En une commande (recommandé)

Prérequis Mac : Unity Hub + **Linux Build Support** + accès SSH au VPS (clé ou mot de passe).

```bash
# Option A — IP en argument
./ServerLinux/deploy-hostinger.sh 187.124.45.244

# Option B — config persistante
cp ServerLinux/deploy.env.example ServerLinux/deploy.env
# éditer deploy.env puis :
./ServerLinux/deploy-hostinger.sh
```

Le script enchaîne : **build Unity** → **archive tar.gz** → **scp** → **systemctl restart metaverse**.

Build déjà fait (sans relancer Unity) :

```bash
SKIP_BUILD=1 ./ServerLinux/deploy-hostinger.sh 187.124.45.244
```

Logs build local : `Logs/deploy-build.log`

---

## Étapes manuelles

### 1. Build (Unity, sur Mac)

1. **MetaVerse → Server → Add MetaVerse Scene To Build**
2. **MetaVerse → Server → Build Dedicated Server** → sortie à la racine du projet (`MetaverseServer`)

Vérifier que ces fichiers existent à la racine :

- `MetaverseServer`
- `MetaverseServer_Data/`
- `UnityPlayer.so`
- `libdecor-0.so.0`
- `libdecor-cairo.so`

## 2. Créer l'archive

```bash
cd ServerLinux
chmod +x package.sh
./package.sh
```

Produit `ServerLinux/metaverse-server-linux64.tar.gz` (~90 Mo).

## 3. Envoyer sur le VPS Hostinger

Remplacer `TON_IP` par l'IP publique du VPS (Ubuntu 22.04 recommandé).

```bash
scp ServerLinux/metaverse-server-linux64.tar.gz root@TON_IP:/opt/
scp ServerLinux/install-vps.sh root@TON_IP:/opt/
```

## 4. Installer sur le VPS

```bash
ssh root@TON_IP

mkdir -p /opt/metaverse-server
tar -xzf /opt/metaverse-server-linux64.tar.gz -C /opt/metaverse-server
bash /opt/install-vps.sh
```

Le script installe les libs, ouvre les ports firewall, crée le service systemd `metaverse` et démarre le serveur.

## 5. Connecter les clients

Dans Unity (UI connexion ou `NetworkManager`) :

| Champ | Valeur |
|-------|--------|
| Server IP | IP publique du VPS |
| Tcp Port | 25000 |
| Udp Server Port | 25001 |

## Commandes utiles

```bash
systemctl status metaverse
systemctl restart metaverse
journalctl -u metaverse -f
tail -f /var/log/metaverse-server.log
```

## Mise à jour du serveur

```bash
systemctl stop metaverse
tar -xzf /opt/metaverse-server-linux64.tar.gz -C /opt/metaverse-server
systemctl start metaverse
```

## Option Docker

```bash
# Copier le contenu du build dans ServerLinux/ puis :
docker build -t metaverse-server .
docker run -d --restart unless-stopped \
  -p 25000:25000/tcp -p 25001:25001/udp \
  --name metaverse metaverse-server
```

## Option Fly.io

Voir `fly.toml` (ports 25000/tcp + 25001/udp).
