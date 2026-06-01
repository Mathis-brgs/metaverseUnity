# Tâches J1–J2 — A : Architecte Réseau

> **Objectif de la phase Cadrage :** sortir avec un protocole documenté et une architecture serveur claire,
> que B et C peuvent utiliser comme référence dès J3.

---

## J1 — Lundi 1 juin

### 1. Lire et auditer le code réseau existant

Lire ces deux fichiers en entier et noter les limitations :

- `Assets/Demos/TCP/TCPServer.cs`
- `Assets/Demos/TCP/TCPClient.cs`

**Limitations à identifier (déjà repérées) :**

| Problème | Ligne concernée | Impact |
|---|---|---|
| Pas d'identifiant client | `Connections` = liste de `TcpClient` bruts | On ne sait pas quel joueur envoie quoi |
| Suppression pendant itération | `TCPServer.cs:113` | Crash potentiel si un client déconnecte |
| Pas de délimiteur de message | `ParseString()` | Deux messages consécutifs peuvent arriver concaténés |
| Lecture bloquante dans `Update()` | `ReceiveTCP()` | Risque de freeze Unity si beaucoup de data |
| Pas de thread dédié | tout le fichier | Scalabilité limitée |

> Ces limitations, tu n'as **pas** à les corriger aujourd'hui. Tu dois juste les noter pour concevoir ton serveur standalone proprement dès J3.

---

### 2. Décider TCP vs UDP par type de message

Le projet doit utiliser **les deux protocoles** pour avoir les points "réduction latence/bande passante".

Complète ce tableau et valide-le avec l'équipe en fin de J1 :

| Type d'événement | Fréquence | Protocole choisi | Raison |
|---|---|---|---|
| Position du joueur | ~20x/sec | **UDP** | Perte acceptable, vitesse prioritaire |
| Connexion / déconnexion | 1x | **TCP** | Fiabilité obligatoire |
| Collecte d'un bonus | 1x | **TCP** | Fiabilité obligatoire (anti race condition) |
| État initial complet | 1x à la connexion | **TCP** | Doit arriver complet et dans l'ordre |
| Instantiation d'un joueur | 1x | **TCP** | Fiabilité obligatoire |

---

### 3. Définir les types de messages du protocole

Ton protocole doit avoir un champ `type` dans chaque message.
Commence avec **JSON** (plus facile à déboguer), on optimisera après.

Liste minimale de messages à définir aujourd'hui :

```
MSG_JOIN          client → serveur   (je veux rejoindre)
MSG_WELCOME       serveur → client   (bienvenue, voici ton ID + état du monde)
MSG_MOVE          client → serveur   (ma nouvelle position)
MSG_STATE         serveur → tous     (état global ou delta de positions)
MSG_PLAYER_JOIN   serveur → tous     (un joueur vient de rejoindre)
MSG_PLAYER_LEFT   serveur → tous     (un joueur vient de partir)
MSG_BONUS_TAKEN   serveur → tous     (bonus X a été collecté par joueur Y)
```

---

## J2 — Mardi 2 juin

### 4. Écrire le format exact de chaque message

Pour chaque message listé ci-dessus, rédige le JSON complet.
Voici un exemple à compléter et étendre :

```json
// MSG_JOIN  — client → serveur
{ "type": "JOIN", "playerName": "Alice" }

// MSG_WELCOME  — serveur → client
{
  "type": "WELCOME",
  "playerId": "p1",
  "players": [
    { "id": "p2", "name": "Bob", "x": 1.5, "y": 0.0, "z": 3.2, "rotY": 45.0, "score": 3 }
  ],
  "bonuses": [
    { "id": "b0", "x": 2.0, "y": 0.5, "z": -1.0 },
    { "id": "b1", "x": -3.0, "y": 0.5, "z": 4.0 }
  ]
}

// MSG_MOVE  — client → serveur (UDP)
{ "type": "MOVE", "playerId": "p1", "x": 1.5, "y": 0.0, "z": 3.2, "rotY": 45.0 }

// MSG_STATE  — serveur → tous les clients (UDP, ~20x/sec)
{
  "type": "STATE",
  "players": [
    { "id": "p1", "x": 1.5, "y": 0.0, "z": 3.2, "rotY": 45.0 },
    { "id": "p2", "x": -2.0, "y": 0.0, "z": 1.0, "rotY": 90.0 }
  ]
}

// MSG_BONUS_TAKEN  — serveur → tous (TCP)
{ "type": "BONUS_TAKEN", "bonusId": "b0", "byPlayerId": "p1" }

// MSG_PLAYER_JOIN  — serveur → tous (TCP)
{ "type": "PLAYER_JOIN", "playerId": "p3", "name": "Charlie", "x": 0.0, "y": 0.0, "z": 0.0 }

// MSG_PLAYER_LEFT  — serveur → tous (TCP)
{ "type": "PLAYER_LEFT", "playerId": "p3" }
```

---

### 5. Documenter l'architecture serveur

Rédige un fichier `planning/protocol.md` avec :

1. Le schéma de flux de données (qui envoie quoi à qui)
2. Le tableau TCP vs UDP finalisé
3. Tous les formats de messages JSON

**Schéma de flux à rédiger :**

```
Client A ──MOVE(UDP)──────────────────┐
Client B ──MOVE(UDP)──────────────────┤
Client C ──MOVE(UDP)──────────────────► SERVEUR ──STATE(UDP broadcast)──► tous les clients
                                      │
Client A ──JOIN(TCP)──────────────────┤
                                      └──WELCOME(TCP)──► Client A
                                       ──PLAYER_JOIN(TCP broadcast)──► B, C
```

---

### Livrable fin J2 (à partager avec B et C)

- [ ] `planning/protocol.md` complété avec tous les formats de messages
- [ ] Tableau TCP/UDP validé avec l'équipe
- [ ] Liste des limitations du `TCPServer.cs` existant documentée
- [ ] Architecture serveur standalone planifiée (console C#, pas Unity)

> **Note :** Le serveur que tu vas construire en J3 sera une **application console C# standalone**,
> pas un MonoBehaviour Unity. Plus simple à déployer sur cloud et plus performant.
