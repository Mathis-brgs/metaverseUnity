# Tâches J1–J2 — A : Architecte Réseau

> **Objectif de la phase Cadrage :** sortir avec un protocole documenté et une architecture serveur claire,
> que B, C et D peuvent utiliser comme référence dès J3.
> **D dépend directement de ton travail** — il ne peut pas concevoir le routage des messages sans ton protocole finalisé.

---

## J1 — Lundi 1 juin ✅

### ✅ 1. Lire et auditer le code réseau existant

- `Assets/Demos/TCP/TCPServer.cs`
- `Assets/Demos/TCP/TCPClient.cs`

**Limitations identifiées :**

| Problème | Ligne concernée | Impact |
|---|---|---|
| Pas d'identifiant client | `Connections` = liste de `TcpClient` bruts | On ne sait pas quel joueur envoie quoi |
| Suppression pendant itération | `TCPServer.cs:113` — `Connections.Remove(client)` dans un `foreach` | `InvalidOperationException` au runtime dès qu'un client déconnecte. Le `return` masque le bug mais saute la lecture des autres clients ce frame-là |
| Pas de délimiteur de message | `ParseString()` ligne 133 | TCP est un flux : deux messages envoyés d'affilée peuvent arriver collés dans le même `Read()`. `ParseString` ne sait pas où l'un finit et l'autre commence → **solution : terminer chaque message par `\n`** |
| Lecture bloquante dans `Update()` | `ReceiveTCP()` | Risque de freeze Unity si beaucoup de data |
| Pas de thread dédié | tout le fichier | Scalabilité limitée |

---

### ✅ 2. Décider TCP vs UDP par type de message

| Type d'événement | Fréquence | Protocole | Raison |
|---|---|---|---|
| Position du joueur | ~20x/sec | **UDP** | Perte acceptable, vitesse prioritaire |
| Connexion / déconnexion | 1x | **TCP** | Fiabilité obligatoire |
| Collecte d'un bonus | 1x | **TCP** | Fiabilité obligatoire (anti race condition) |
| État initial complet | 1x à la connexion | **TCP** | Doit arriver complet et dans l'ordre |
| Instantiation d'un joueur | 1x | **TCP** | Fiabilité obligatoire |

---

### ✅ 3. Définir les types de messages du protocole

| Message | Expéditeur | Destinataire | Protocole |
|---|---|---|---|
| `JOIN` | Client | Serveur | TCP |
| `INIT_STATE` | Serveur | Nouveau client | TCP |
| `PLAYER_JOIN` | Serveur | Tous les autres | TCP |
| `PLAYER_LEFT` | Serveur | Tous | TCP |
| `MOVE` | Client | Serveur | UDP |
| `STATE` | Serveur | Tous | UDP |
| `TAKE` | Client | Serveur | TCP |
| `BONUS_TAKEN` | Serveur | Tous | TCP |

---

## J2 — Mardi 2 juin

### ✅ 4. Écrire le format exact de chaque message

→ Voir `planning/protocol.md` (fichier créé, tous les formats JSON documentés)

---

### ✅ 5. Documenter l'architecture serveur

**Architecture réseau locale (réseau école) :**

```
PC de A ──────────────────────────────────────────────
│  Serveur C# console app                             │
│  IP locale ex: 192.168.x.x                          │
│  Port TCP : 25000  |  Port UDP : 25001              │
└─────────────────────────────────────────────────────┘
         ↑              ↑              ↑
      PC de B        PC de C        PC de D
    (Unity client)  (Unity client)  (Unity client)
```

**Pourquoi console app et pas Unity :**
- Unity a besoin d'une fenêtre et d'un GPU pour tourner — inutile côté serveur
- Une console app C# tourne partout, déployable sur cloud Linux sans écran
- Pas de dépendance à la boucle de rendu Unity

**Trouver son IP locale (Mac) :**
```bash
ipconfig getifaddr en0
```

**Schéma de flux :**
```
Client A ──JOIN(TCP)─────────────────► SERVEUR ──INIT_STATE(TCP)──────► Client A
                                               ──PLAYER_JOIN(TCP)────► B, C, D

Client A ──MOVE(UDP)─────────────────► SERVEUR ──STATE(UDP broadcast)─► A, B, C, D
Client B ──MOVE(UDP)─────────────────►
Client C ──MOVE(UDP)─────────────────►

Client A ──TAKE(TCP)─────────────────► SERVEUR ──BONUS_TAKEN(TCP)────► A, B, C, D

Client A  déconnecte                   SERVEUR ──PLAYER_LEFT(TCP)─────► B, C, D
```

---

### Livrable fin J2 (à partager avec B, C et D)

- [x] `planning/protocol.md` complété avec tous les formats de messages
- [x] Tableau TCP/UDP validé
- [x] Limitations du `TCPServer.cs` documentées
- [x] Architecture serveur standalone planifiée (console C#, pas Unity)
- [ ] Schéma de flux partagé avec D pour qu'il construise sa table de routage dès J3

---

## Avance sur J3 — Mardi 2 juin

### ✅ Serveur console opérationnel (`Server/Program.cs`)
- Accepte plusieurs clients simultanément
- Parse les messages JSON et route par `type`
- Détecte les déconnexions et broadcast `PLAYER_LEFT`
- `Send` par client + `Broadcast` vers tous (avec exclusion optionnelle)
- IP locale affichée au démarrage (`10.10.167.15`)
- Testé avec `nc` — réception et parsing confirmés

### Reste à faire en J3 (Mer 3 juin)
- [ ] JOIN complet : envoyer `INIT_STATE` au nouveau joueur + `PLAYER_JOIN` aux autres
- [ ] Intégrer `WorldState` de C (positions, bonus)
- [ ] UDP port 25001 : recevoir `MOVE` + broadcaster `STATE` 20x/sec
