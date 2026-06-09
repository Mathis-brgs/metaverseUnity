# Réseau MetaVerse — client + serveur Unity

Le serveur est désormais **un serveur Unity dédié** (autorité Physics), plus le serveur console C#.
Protocole : `planning/protocol.md`.

## Architecture

- **Serveur** : instance Unity lancée en mode serveur (voir `Assets/Game/Server/`). Charge la scène
  MetaVerse, simule les joueurs (proxies Rigidbody), détecte les triggers de bonus et conduit les voitures.
- **Client** : `NetworkManager` + `TCPClient`. Envoie des **intentions** (`INPUT`, `CAR_ENTER`/`CAR_EXIT`)
  et affiche l'état renvoyé par le serveur (`STATE`, `INIT_STATE`, etc.).

## Setup client (scène MetaVerse)

1. GameObject avec **`TCPClient`** + **`NetworkManager`** + **`RemotePlayerManager`**.
2. `NetworkManager` :
   - `Server IP` = IP du serveur, `Tcp Port` = 25000, `Udp Server Port` = 25001
   - **`Send Input Automatically`** = true, **`Input Source`** = le `CharacterController` du joueur local
   - (legacy) `Send Move Automatically` reste possible mais le serveur autoritaire attend `INPUT`
3. `RemotePlayerManager` : renseigner `CharacterPrefabs` (un prefab par personnage).

## Lancer le serveur

Voir `Assets/Game/Server/README.md`. En résumé :
- **Éditeur** : menu `MetaVerse/Server/Force Server In Editor`, puis Play sur MetaVerse.
- **Build** : menu `MetaVerse/Server/Build Dedicated Server`, puis `MetaverseServer -batchmode -nographics`.

## Messages gérés (client)

| Direction | Protocole | Handler |
|-----------|-----------|---------|
| `JOIN` | TCP → | `Connect()` |
| `INIT_STATE` | TCP ← | `OnInitState`, `MyPlayerId` |
| `PLAYER_JOIN` / `PLAYER_LEFT` / `BONUS_TAKEN` | TCP ← | UnityEvents |
| `CAR_ENTERED` / `CAR_EXITED` / `ERROR` | TCP ← | UnityEvents |
| `INPUT` | UDP → | `SendInput()` / `Send Input Automatically` |
| `MOVE` | UDP → | `SendMove()` (legacy) |
| `STATE` | UDP ← | `OnState` (joueurs + voitures) |
| `CAR_ENTER` / `CAR_EXIT` | TCP → | `SendCarEnter()` / `SendCarExit()` |

## Important

- Un seul serveur à la fois (ports 25000/25001).
- Le serveur console C# (`Server/`) est **déprécié et supprimé** : l'autorité est désormais dans Unity.
