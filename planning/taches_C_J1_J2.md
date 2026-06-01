# Tâches J1–J2 — C : Dev Logique Jeu

> **Objectif de la phase Cadrage :** mettre en place l'infrastructure Git du projet
> et designer la structure de données `WorldState` que le serveur utilisera pour maintenir l'état du jeu.

---

## J1 — Lundi 1 juin

### 1. Créer et configurer le repo GitHub


#### a) Créer le `.gitignore` Unity

Créer un fichier `.gitignore` à la racine avec ce contenu minimal :

```
# Unity
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/
*.csproj
*.sln
*.slnx

# OS
.DS_Store
Thumbs.db
```

> **Important :** Le dossier `Library/` fait plusieurs centaines de Mo.
> Il est régénéré automatiquement par Unity → ne jamais le committer.

#### b) Structure de branches

```bash
git checkout -b dev
git push -u origin dev

git checkout -b feature/server
git push -u origin feature/server

git checkout -b feature/client
git push -u origin feature/client
```

**Règle d'équipe :** on ne travaille jamais directement sur `main`.
- `main` = version stable, démo possible à tout moment
- `dev` = intégration continue, merge des features
- `feature/XXX` = travail en cours

#### c) Conventions de commit

Décider et noter dans le README les conventions :

```
feat:     nouvelle fonctionnalité
fix:      correction de bug
refactor: réorganisation du code sans changer le comportement
test:     ajout ou modification de tests
docs:     documentation uniquement
chore:    tâches diverses (gitignore, config...)

Exemples :
feat: ajouter réception MSG_MOVE côté serveur
fix: corriger crash déconnexion client pendant itération
```

---

### 2. Lire les scripts MetaVerse pour comprendre l'état du jeu

Lire ces fichiers pour comprendre ce que le serveur devra suivre :

- `Assets/Demos/MetaVerse/CharacterController.cs` → position, rotation de chaque joueur
- `Assets/Demos/MetaVerse/Bonus.cs` → état des bonus (présent / collecté)
- `Assets/Demos/MetaVerse/CharacterScore.cs` → score de chaque joueur

---

## J2 — Mardi 2 juin

### 3. Designer la structure WorldState

Le `WorldState` est la **source de vérité** du serveur. Il représente l'état complet du jeu à un instant T.

#### a) Identifier ce qui doit être suivi

| Donnée | Pourquoi le serveur doit la connaître |
|---|---|
| Liste des joueurs connectés | Savoir qui est en jeu |
| Position + rotation de chaque joueur | Broadcast aux autres clients |
| Score de chaque joueur | Affiché chez tous |
| Liste des bonus et leur état (présent/collecté) | Anti race condition + sync initial |

#### b) Esquisser les classes C# du WorldState

Ce code sera écrit en J3, mais tu dois le planifier aujourd'hui :

```csharp
// État d'un joueur
public class PlayerState
{
    public string Id;        // identifiant unique (ex: "p1", "p2")
    public string Name;      // nom affiché
    public float X, Y, Z;   // position dans la scène
    public float RotY;       // rotation verticale (yaw)
    public int Score;
}

// État d'un objet bonus
public class BonusState
{
    public string Id;        // identifiant unique (ex: "b0", "b1")
    public float X, Y, Z;   // position dans la scène
    public bool IsCollected; // true = détruit côté clients
}

// État complet du monde
public class WorldState
{
    public Dictionary<string, PlayerState> Players;
    public Dictionary<string, BonusState> Bonuses;

    // Générer un playerId unique
    public string GeneratePlayerId() { ... }

    // Mettre à jour la position d'un joueur
    public void UpdatePlayerPosition(string playerId, float x, float y, float z, float rotY) { ... }

    // Marquer un bonus comme collecté (retourner false si déjà pris → race condition)
    public bool TryCollectBonus(string bonusId, string byPlayerId) { ... }
}
```

#### c) Lister tous les événements qui modifient le WorldState

| Événement | Modification dans WorldState |
|---|---|
| Un joueur se connecte | Ajouter `PlayerState` dans `Players` |
| Un joueur se déconnecte | Retirer `PlayerState` de `Players` |
| Un joueur bouge | Mettre à jour `X, Y, Z, RotY` du `PlayerState` |
| Un joueur collecte un bonus | `BonusState.IsCollected = true` + incrémenter `Score` |
| Un bonus est collecté (race condition) | `TryCollectBonus()` retourne false si déjà pris |

#### d) Réfléchir à la synchronisation initiale

Quand un nouveau joueur se connecte, le serveur doit lui envoyer **l'état complet du monde**.
Ça correspond au message `MSG_WELCOME` défini par A.

Questions à poser à A en fin de J2 :
- Le `MSG_WELCOME` contient bien tous les joueurs + tous les bonus ?
- Comment on identifie les bonus dans la scène Unity ? (par position ? par nom ?)
- Est-ce que les positions des bonus sont hardcodées ou dynamiques ?

---

### Livrable fin J2 (à partager avec A et B)

- [ ] Repo GitHub créé, `.gitignore` en place, branches créées
- [ ] README.md avec nom du projet, membres de l'équipe, instructions de lancement
- [ ] Conventions de commit documentées dans le README
- [ ] Classes `PlayerState`, `BonusState`, `WorldState` esquissées (même en pseudo-code)
- [ ] Liste des événements qui modifient le WorldState complétée
- [ ] Questions sur la sync initiale posées à A

---

### Point de sync équipe — fin J2 (30 min)

Faire un appel ou se retrouver pour aligner :

1. **A** présente le protocole JSON finalisé
2. **B** présente le plan du `NetworkManager`
3. **C** présente le `WorldState`

Vérifier que les noms de champs JSON de A correspondent aux propriétés de C
(ex : A utilise `"x"` et C a `float X` → OK, mais le vérifier ensemble).
