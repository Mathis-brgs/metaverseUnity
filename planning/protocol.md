# Protocole de communication — MetaVerse MMO

## Règle TCP vs UDP

| Critère | Protocole |
|---|---|
| Message rare, doit arriver absolument | TCP |
| Message fréquent (~20x/sec), perte acceptable | UDP |

## Table des messages

| Message | Expéditeur | Destinataire | Protocole | Description |
|---|---|---|---|---|
| `JOIN` | Client | Serveur | TCP | Le joueur signale au serveur qu'il veut rejoindre |
| `INIT_STATE` | Serveur | Nouveau client | TCP | État complet du monde à la connexion |
| `PLAYER_JOIN` | Serveur | Tous les autres | TCP | Notifie les autres qu'un nouveau joueur est connecté |
| `PLAYER_LEFT` | Serveur | Tous | TCP | Un joueur vient de se déconnecter |
| `MOVE` | Client | Serveur | UDP | (legacy) Position du joueur — toléré pendant la migration |
| `INPUT` | Client | Serveur | UDP | Intention de déplacement (serveur autoritaire Physics) |
| `STATE` | Serveur | Tous | UDP | Broadcast positions joueurs + voitures (~20x/sec) |
| `TAKE` | Client | Serveur | TCP | (legacy) Demande de collecte — le serveur Unity détecte par trigger |
| `BONUS_TAKEN` | Serveur | Tous | TCP | Confirme qu'un bonus a été collecté et par qui |
| `CAR_ENTER` | Client | Serveur | TCP | Le joueur demande à monter dans une voiture |
| `CAR_EXIT` | Client | Serveur | TCP | Le joueur demande à descendre |
| `CAR_ENTERED` | Serveur | Tous | TCP | Confirme qu'un joueur conduit une voiture |
| `CAR_EXITED` | Serveur | Tous | TCP | Confirme qu'une voiture est libérée |

> **Serveur Unity (autorité Physics)** : depuis la migration, le serveur charge la scène MetaVerse,
> simule les joueurs (proxies Rigidbody), détecte les collisions et les triggers de bonus, et conduit les voitures.
> Les clients envoient des **intentions** (`INPUT`, `CAR_ENTER`/`CAR_EXIT`) et affichent l'état renvoyé par le serveur.
> `MOVE`/`TAKE` restent acceptés pour compatibilité pendant la transition.

## Format des messages

### JOIN — client → serveur (TCP)
```json
{ "type": "JOIN", "name": "Alice" }
```

### INIT_STATE — serveur → nouveau client (TCP)
```json
{
  "type": "INIT_STATE",
  "playerId": "p1",
  "players": [
    { "id": "p2", "name": "Bob", "character": "knight", "x": 1.5, "y": 0.0, "z": 3.2, "rotY": 45.0, "score": 3 }
  ],
  "bonuses": [
    { "id": "b0", "x": 2.0, "y": 0.5, "z": -1.0 }
  ],
  "cars": [
    { "id": "Car1", "x": 10.0, "y": 0.0, "z": 5.0, "rotY": 90.0, "driverId": "" }
  ]
}
```

### PLAYER_JOIN — serveur → tous les autres (TCP)
```json
{ "type": "PLAYER_JOIN", "id": "p1", "name": "Alice", "character": "mage", "x": 0.0, "y": 0.0, "z": 0.0 }
```

### PLAYER_LEFT — serveur → tous (TCP)
```json
{ "type": "PLAYER_LEFT", "id": "p1" }
```

### MOVE — client → serveur (UDP, legacy)
```json
{ "type": "MOVE", "id": "p1", "x": 1.5, "y": 0.0, "z": 3.2, "rotY": 45.0 }
```

### INPUT — client → serveur (UDP, autorité Physics)
```json
{ "type": "INPUT", "id": "p1", "ix": 0.0, "iz": 1.0, "rotY": 45.0 }
```
`ix`/`iz` dans [-1, 1] : à pied = direction de déplacement monde (déjà relative caméra) ;
en voiture = `ix` = braquage, `iz` = accélération.

### STATE — serveur → tous (UDP, ~20x/sec)
```json
{
  "type": "STATE",
  "players": [
    { "id": "p1", "x": 1.5, "y": 0.0, "z": 3.2, "rotY": 45.0, "inCarId": "" }
  ],
  "cars": [
    { "id": "Car1", "x": 11.0, "y": 0.0, "z": 5.0, "rotY": 92.0, "driverId": "p2" }
  ]
}
```

### TAKE — client → serveur (TCP, legacy)
```json
{ "type": "TAKE", "playerId": "p1", "bonusId": "b0" }
```

### BONUS_TAKEN — serveur → tous (TCP)
```json
{ "type": "BONUS_TAKEN", "bonusId": "b0", "byPlayerId": "p1", "newScore": 4 }
```

### CAR_ENTER / CAR_EXIT — client → serveur (TCP)
```json
{ "type": "CAR_ENTER", "playerId": "p1", "carId": "Car1" }
{ "type": "CAR_EXIT", "playerId": "p1" }
```
`carId` peut être vide : le serveur choisit la voiture la plus proche du joueur.

### CAR_ENTERED / CAR_EXITED — serveur → tous (TCP)
```json
{ "type": "CAR_ENTERED", "carId": "Car1", "driverId": "p1" }
{ "type": "CAR_EXITED", "carId": "Car1" }
```

## Schéma de flux

```
Client A ──JOIN(TCP)─────────────────► SERVEUR ──INIT_STATE(TCP)──────► Client A
                                               ──PLAYER_JOIN(TCP)────► B, C, D

Client A ──MOVE(UDP)─────────────────► SERVEUR ──STATE(UDP broadcast)─► A, B, C, D
Client B ──MOVE(UDP)─────────────────►
Client C ──MOVE(UDP)─────────────────►

Client A ──TAKE(TCP)─────────────────► SERVEUR ──BONUS_TAKEN(TCP)────► A, B, C, D

Client A  déconnecte                   SERVEUR ──PLAYER_LEFT(TCP)─────► B, C, D
```

## Délimiteur de messages

Chaque message TCP se termine par `\n` pour éviter la concaténation dans le buffer.
UDP n'a pas ce problème (chaque datagramme est un paquet indépendant).
