# NetworkManager — client MetaVerse

Implémente le protocole dans `planning/protocol.md` (A).

## Setup dans Unity

1. GameObject avec **`TCPClient`** (ex. scène `Demos/TCP/TCP.unity` ou MetaVerse).
2. Add Component → **`NetworkManager`** (même objet).
3. **`TCPClient`** : `Destination IP` = IP du serveur, port = **Tcp Port** (défaut 25000).
4. **`NetworkManager`** :
   - `Tcp Port` = 25000 (identique à `TCPClient.DestinationPort`)
   - `Udp Server Port` = port UDP du serveur A (souvent **25001** — confirmer avec A)
   - `Connect On Start` = true pour test auto
   - `Player Name` = pseudo

5. Lancer le **serveur A**, puis Play.

## Messages gérés

| Direction | Protocole | Handler |
|-----------|-----------|---------|
| `JOIN` | TCP → | `Connect()` |
| `INIT_STATE` | TCP ← | `OnInitState`, `MyPlayerId` |
| `PLAYER_JOIN` / `PLAYER_LEFT` / `BONUS_TAKEN` | TCP ← | UnityEvents |
| `MOVE` | UDP → | `SendMove()` ou `Send Move Automatically` |
| `STATE` | UDP ← | `OnState` |
| `TAKE` | TCP → | `SendTake(bonusId)` |

## Test MOVE automatique

Cocher **Send Move Automatically** et assigner **Move Source** (Transform du joueur local).

## Important

- Ne pas démarrer le **TCPServer** Unity si le serveur console A tourne déjà (même port).
- UDP utilise **un seul socket** (envoi + réception) pour que le serveur puisse répondre au bon port client.

## Prochaine étape (B)

Brancher `OnInitState` / `OnState` / `OnPlayerJoin` pour spawn et sync des avatars MetaVerse.
