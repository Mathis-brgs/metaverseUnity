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
| `MOVE` | Client | Serveur | UDP | Position du joueur mise à jour |
| `STATE` | Serveur | Tous | UDP | Broadcast des positions de tous les joueurs (~20x/sec) |
| `TAKE` | Client | Serveur | TCP | Le joueur demande à collecter un bonus |
| `BONUS_TAKEN` | Serveur | Tous | TCP | Confirme qu'un bonus a été collecté et par qui |

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
    { "id": "p2", "name": "Bob", "x": 1.5, "y": 0.0, "z": 3.2, "rotY": 45.0, "score": 3 }
  ],
  "bonuses": [
    { "id": "b0", "x": 2.0, "y": 0.5, "z": -1.0 },
    { "id": "b1", "x": -3.0, "y": 0.5, "z": 4.0 }
  ]
}
```

### PLAYER_JOIN — serveur → tous les autres (TCP)
```json
{ "type": "PLAYER_JOIN", "id": "p1", "name": "Alice", "x": 0.0, "y": 0.0, "z": 0.0 }
```

### PLAYER_LEFT — serveur → tous (TCP)
```json
{ "type": "PLAYER_LEFT", "id": "p1" }
```

### MOVE — client → serveur (UDP)
```json
{ "type": "MOVE", "id": "p1", "x": 1.5, "y": 0.0, "z": 3.2, "rotY": 45.0 }
```

### STATE — serveur → tous (UDP, ~20x/sec)
```json
{
  "type": "STATE",
  "players": [
    { "id": "p1", "x": 1.5, "y": 0.0, "z": 3.2, "rotY": 45.0 },
    { "id": "p2", "x": -2.0, "y": 0.0, "z": 1.0, "rotY": 90.0 }
  ]
}
```

### TAKE — client → serveur (TCP)
```json
{ "type": "TAKE", "playerId": "p1", "bonusId": "b0" }
```

### BONUS_TAKEN — serveur → tous (TCP)
```json
{ "type": "BONUS_TAKEN", "bonusId": "b0", "byPlayerId": "p1", "newScore": 4 }
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
