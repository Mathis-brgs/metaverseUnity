# Table de routage des messages — D : Serveur Logique

> **Contexte :** Ce document décrit ce que le serveur fait quand il reçoit un message.
> La table de routage = le `switch (msg.type)` du serveur.
> **On ne code pas encore** — on décrit en français, on code en J3.

---

## Principe général

```
Message JSON arrive
       ↓
Désérialiser → lire le champ "type"
       ↓
switch (msg.type)
  ├── "JOIN"   → HandleJoin(clientId, json)
  ├── "MOVE"   → HandleMove(clientId, json)
  ├── "TAKE"   → HandleTake(clientId, json)
  └── default  → LogUnknownMessage(msg.type)   // ne jamais crasher
```

**Règle d'or :** le `default` ne doit **jamais** lever d'exception.
Un message inconnu → on logge et on ignore.

---

## Messages que le serveur REÇOIT (client → serveur)

### CASE "JOIN" → `HandleJoin(clientId, json)`

**Format du message reçu (TCP) :**
```json
{ "type": "JOIN", "name": "Alice" }
```

**Ce que le handler doit faire (dans cet ordre) :**
1. Vérifier que `clientId` n'est pas déjà dans `WorldState` (anti double-JOIN)
2. Vérifier que le nombre de joueurs n'atteint pas le maximum (ex: 4 joueurs max)
3. Créer un `PlayerState` dans `WorldState` avec un ID unique, le nom reçu, et une position de spawn
4. Envoyer `INIT_STATE` **uniquement au nouveau joueur** avec : son ID, la liste des joueurs existants, la liste des bonus restants
5. Broadcaster `PLAYER_JOIN` **à tous les autres joueurs** (pas au nouveau, il a déjà tout dans INIT_STATE)

**Lit dans le message :** `name`
**Modifie dans WorldState :** ajoute un `PlayerState`
**Envoie en sortie :** `INIT_STATE` au nouveau + `PLAYER_JOIN` broadcast

---

### CASE "MOVE" → `HandleMove(clientId, json)`

**Format du message reçu (UDP, ~20x/sec) :**
```json
{ "type": "MOVE", "id": "p1", "x": 1.5, "y": 0.0, "z": 3.2, "rotY": 45.0 }
```

**Ce que le handler doit faire :**
1. Vérifier que `id` existe dans `WorldState` (anti MOVE avant JOIN)
2. Vérifier que les coordonnées sont valides : pas NaN, pas Infinity, pas hors de la carte
3. Mettre à jour `x`, `y`, `z`, `rotY` du joueur dans `WorldState`
4. **Ne pas broadcaster ici** — le broadcast de positions se fait séparément, à intervalle fixe (boucle `STATE` ~20x/sec)

**Lit dans le message :** `id`, `x`, `y`, `z`, `rotY`
**Modifie dans WorldState :** met à jour la position d'un `PlayerState`
**Envoie en sortie :** rien (le broadcast STATE est géré par une boucle séparée)

---

### CASE "TAKE" → `HandleTake(clientId, json)`

**Format du message reçu (TCP) :**
```json
{ "type": "TAKE", "playerId": "p1", "bonusId": "b0" }
```

**Ce que le handler doit faire :**
1. Vérifier que `playerId` existe dans `WorldState`
2. Vérifier que `bonusId` existe encore dans `WorldState` (anti double-collect)
3. Appeler `WorldState.TryCollectBonus(bonusId, playerId)`
   - Si `false` (déjà collecté) → ignorer silencieusement
   - Si `true` :
     a. Retirer le bonus de `WorldState`
     b. Ajouter les points au score du joueur dans `WorldState`
     c. Calculer le nouveau score du joueur
     d. Broadcaster `BONUS_TAKEN` à tous les clients avec `newScore`

**Lit dans le message :** `playerId`, `bonusId`
**Modifie dans WorldState :** retire un bonus + met à jour un score
**Envoie en sortie :** `BONUS_TAKEN` broadcast (seulement si collecte réussie)

---

### DEFAULT → `LogUnknownMessage(type)`

**Ce que le handler doit faire :**
1. Logger le type inconnu avec l'ID du client qui l'a envoyé
2. **Ne rien faire d'autre** — surtout pas lever d'exception

---

## Messages que le serveur ENVOIE (serveur → clients)

Ces messages ne sont pas dans le switch — ils sont *émis* par les handlers ci-dessus
ou par des boucles périodiques.

| Message émis | Émis par | Destinataire | Protocole |
|---|---|---|---|
| `INIT_STATE` | `HandleJoin` | nouveau joueur uniquement | TCP |
| `PLAYER_JOIN` | `HandleJoin` | tous les autres joueurs | TCP |
| `STATE` | boucle périodique (~20x/sec) | tous les joueurs | UDP |
| `BONUS_TAKEN` | `HandleTake` | tous les joueurs | TCP |
| `PLAYER_LEFT` | détection de déconnexion | tous les joueurs | TCP |
