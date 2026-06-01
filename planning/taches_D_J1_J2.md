# Tâches J1–J2 — D : Serveur Logique & Autorité

> **Objectif de la phase Cadrage :** comprendre le protocole conçu par A et le `WorldState` conçu par C,
> puis lister tous les cas limites que le serveur devra gérer.
> Tu ne codes pas encore — tu analyses et tu prépares.

---

## J1 — Lundi 1 juin

### 1. Lire les scripts MetaVerse pour comprendre le gameplay

Avant de penser au réseau, comprendre ce que le jeu fait localement :

- `Assets/Demos/MetaVerse/CharacterController.cs` → comment un joueur se déplace
- `Assets/Demos/MetaVerse/Bonus.cs` → comment un bonus est collecté (ligne 24 : `OnTriggerEnter`)
- `Assets/Demos/MetaVerse/CharacterScore.cs` → comment le score est géré

**Question clé à noter :** dans `Bonus.cs` ligne 32, `Destroy(gameObject)` supprime le bonus localement.
En multijoueur, qui a le droit de faire ça ? → Le serveur, pas le client. C'est ton rôle.

---

### 2. Lire le travail en cours de A

A est en train de concevoir le protocole aujourd'hui. Lis `planning/taches_A_J1_J2.md`
et note pour chaque type de message :

- Qui l'envoie ?
- Qui le reçoit ?
- Quelle action doit déclencher côté serveur ?

Exemple à compléter :

| Message | Expéditeur | Destinataire | Action serveur |
|---|---|---|---|
| `MSG_JOIN` | Client | Serveur | Créer un `PlayerState`, broadcaster `MSG_PLAYER_JOIN` à tous |
| `MSG_MOVE` | Client | Serveur | Mettre à jour position dans `WorldState` |
| `MSG_BONUS_REQUEST` | Client | Serveur | Appeler `TryCollectBonus()`, broadcaster `MSG_BONUS_TAKEN` si OK |
| ... | ... | ... | ... |

---

### 3. Lister tous les cas limites

C'est ta tâche principale du J1. Un cas limite = une situation anormale que le serveur doit gérer sans crasher.

Commence cette liste et complète-la au fil de la journée :

**Cas limites de connexion :**
- [ ] Un joueur se connecte alors que la partie est déjà commencée → que reçoit-il ?
- [ ] Deux joueurs se connectent exactement en même temps → conflit d'ID ?
- [ ] Un client envoie un `MSG_JOIN` malformé (JSON invalide) → le serveur crash ?

**Cas limites de déconnexion :**
- [ ] Un joueur ferme le jeu brutalement (pas de `MSG_LEFT` envoyé) → le serveur le détecte comment ?
- [ ] Un joueur perd sa connexion internet 30 secondes puis revient → il reprend la même session ou recommence ?
- [ ] Le serveur tente de broadcaster à un client déjà déconnecté → exception ?

**Cas limites de gameplay :**
- [ ] Deux joueurs touchent le même bonus dans le même frame → qui gagne ?
- [ ] Un joueur envoie sa position 100x/sec au lieu de 20x/sec → le serveur est surchargé ?
- [ ] Un joueur envoie une position hors de la carte (coordonnées impossibles) → on accepte ?

---

## J2 — Mardi 2 juin

### 4. Concevoir la table de routage des messages

C'est le cœur de ton travail en J3. Planifie-la aujourd'hui.

Le serveur reçoit un message JSON → il regarde le champ `type` → il appelle la bonne méthode.

```csharp
void HandleMessage(string clientId, string json)
{
    var msg = JsonUtility.FromJson<BaseMessage>(json);

    switch (msg.type)
    {
        case "JOIN":    HandleJoin(clientId, json);   break;
        case "MOVE":    HandleMove(clientId, json);   break;
        case "BONUS":   HandleBonus(clientId, json);  break;
        default:        LogUnknownMessage(msg.type);  break;
    }
}
```

Pour chaque `case`, décris ce que la méthode doit faire en français avant de la coder :

| Handler | Ce qu'il fait |
|---|---|
| `HandleJoin` | Créer `PlayerState` dans `WorldState` → broadcaster `MSG_PLAYER_JOIN` → envoyer `MSG_WELCOME` au nouveau joueur |
| `HandleMove` | Mettre à jour `X/Y/Z/RotY` du joueur dans `WorldState` (le broadcast se fait par le serveur périodiquement, pas ici) |
| `HandleBonus` | Appeler `WorldState.TryCollectBonus()` → si true : broadcaster `MSG_BONUS_TAKEN` + mettre à jour le score |

---

### 5. Planifier la détection de déconnexion

C'est ta tâche difficile des Features+ (J9–J10), mais tu dois la comprendre aujourd'hui
pour éviter de construire quelque chose d'incompatible en J3.

**Le problème :** TCP ne dit pas toujours quand un client déconnecte.
Si le joueur coupe son Wi-Fi, le serveur ne reçoit pas de notification — la connexion semble toujours ouverte.

**Les deux approches à évaluer :**

| Approche | Comment ça marche | Avantage | Inconvénient |
|---|---|---|---|
| **Détection par erreur** | Quand on tente d'écrire vers le client et que ça lève une exception → il est déconnecté | Simple, pas de surcharge | On le détecte tard (au prochain broadcast) |
| **Heartbeat (ping/pong)** | Le serveur envoie un `MSG_PING` toutes les 5 secondes, si pas de réponse en 10s → déconnecté | Détection rapide | Un message de plus dans le protocole |

> Décide avec A en fin de J2 quelle approche vous adoptez — ça impacte le protocole.

---

### Livrable fin J2 (à partager avec A, B et C)

- [ ] Table de routage complétée (tous les `case` décrits en français)
- [ ] Liste des cas limites complétée (minimum 10 cas)
- [ ] Choix de l'approche de détection de déconnexion discuté avec A
- [ ] Questions bloquantes posées à A sur le protocole

---

> **Tip :** Ton rôle est d'être le gardien de la robustesse du serveur.
> À chaque nouvelle feature que A ou C propose, ta question réflexe doit être :
> *"Qu'est-ce qui se passe si ça échoue à mi-chemin ?"*
