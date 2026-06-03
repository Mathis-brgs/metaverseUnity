# Tâches J1–J2 — B : Dev Unity Client

> **Objectif de la phase Cadrage :** comprendre la scène MetaVerse de fond en comble,
> identifier exactement où et comment brancher le réseau, et sortir avec un plan d'intégration clair.

---

## J1 — Lundi 1 juin

### 1. Lancer et explorer la scène MetaVerse

- Ouvrir Unity → scène `Assets/Demos/MetaVerse/MetaVerse.unity`
- Jouer la scène : bouger les 2 personnages, collecter des bonus
- Observer : qu'est-ce qui se passe visuellement quand un bonus est collecté ?
- Ouvrir la hiérarchie Unity et noter tous les GameObjects présents

---

### 2. Lire les scripts MetaVerse

Lire ces fichiers dans l'ordre :

**`Assets/Demos/MetaVerse/CharacterController.cs`**

Points clés à comprendre :
- Le mouvement se passe dans `FixedUpdate()` via `rb.MovePosition()` et `rb.MoveRotation()`
- La position vient de `PlayerAction.ReadValue<Vector2>()` (input clavier)
- Pour les **autres joueurs** (distants), on n'aura pas d'input clavier → il faudra remplacer cette logique par des données réseau

> Question à noter pour J2 : comment séparer "mon personnage" (contrôlé au clavier)
> des "autres personnages" (contrôlés par le réseau) ?

**`Assets/Demos/MetaVerse/Bonus.cs`**

Points clés à comprendre :
- La collecte se fait via `OnTriggerEnter()` — event physique Unity local
- `Destroy(gameObject)` supprime le bonus **localement** (ligne 32)
- En multijoueur, ce `Destroy` devra être déclenché par le **serveur**, pas par le client

> Question à noter : qui a l'autorité sur la collecte d'un bonus ? → Le serveur (anti race condition)

**`Assets/Demos/MetaVerse/CharacterScore.cs`**

- Comprendre comment le score est affiché (`TextMeshPro`)
- En multijoueur, chaque client devra afficher le score de **tous** les joueurs

---

### 3. Identifier les événements réseau côté client

Remplis ce tableau en lisant le code :

| Événement Unity | Déclenché où | Action réseau à prévoir |
|---|---|---|
| Joueur bouge | `CharacterController.FixedUpdate()` | Envoyer `MSG_MOVE` au serveur |
| Joueur entre en contact avec bonus | `Bonus.OnTriggerEnter()` | Envoyer `MSG_BONUS_REQUEST` au serveur |
| Serveur dit qu'un bonus est pris | réception réseau | `Destroy()` le bonus + mettre à jour le score |
| Serveur envoie `MSG_STATE` | réception réseau | Mettre à jour la position de tous les autres joueurs |
| Serveur dit qu'un joueur a rejoint | réception réseau | Instantier un nouveau personnage dans la scène |
| Serveur dit qu'un joueur est parti | réception réseau | Détruire son personnage de la scène |

---

## J2 — Mardi 2 juin

### 4. Lire le code réseau existant

**`Assets/Demos/TCP/TCPClient.cs`**

Points clés :
- `Connect(handler)` : se connecter + enregistrer le callback de réception
- `SendTCPMessage(string)` : envoyer un message texte
- `Update()` → `ReceiveTCP()` : réception dans la boucle Unity
- `IsConnected` : vérifier l'état de la connexion

**`Assets/Demos/UDP/UDPSender.cs` et `UDPReceiver.cs`**

- UDP Sender : stateless, envoie et oublie
- UDP Receiver : écoute sur un port local, callback à la réception

> Le client Unity va utiliser **TCP** pour les événements (connexion, bonus, spawn)
> et **UDP** pour les mises à jour de position (haute fréquence).

---

### 5. Planifier la structure du NetworkManager

Tu dois concevoir un script Unity `NetworkManager.cs` qui va être le **point central** du réseau côté client.

Voici la structure à planifier (pas à coder aujourd'hui, juste à esquisser) :

```csharp
public class NetworkManager : MonoBehaviour
{
    // Identité du joueur local
    public string MyPlayerId;

    // Connexion TCP pour les événements
    TCPClient tcpClient;

    // UDP pour les positions
    UDPSender udpSender;
    UDPReceiver udpReceiver;

    // Dictionnaire des autres joueurs dans la scène
    // clé = playerId, valeur = GameObject du personnage
    Dictionary<string, GameObject> remotePlayers;

    // Appelé quand on reçoit un message TCP du serveur
    void OnTCPMessage(string json) { /* parser le type, router vers la bonne méthode */ }

    // Appelé quand on reçoit un paquet UDP du serveur
    void OnUDPMessage(string json) { /* mettre à jour les positions */ }

    // Envoi de la position locale ~20x/sec
    void SendMyPosition() { /* UDP vers serveur */ }

    // Instantier un personnage distant
    void SpawnRemotePlayer(string playerId, Vector3 position) { }

    // Mettre à jour la position d'un personnage distant
    void UpdateRemotePlayer(string playerId, Vector3 position, float rotY) { }
}
```

---

### 6. Identifier le Prefab à utiliser pour les autres joueurs

- Dans Unity, chercher les Prefabs dans `Assets/Demos/MetaVerse/Prefabs/`
- Identifier quel Prefab représente un personnage (celui utilisé dans la scène actuelle)
- Ce Prefab sera utilisé pour **instantier les autres joueurs** en J7

---

### Livrable fin J2 (à partager avec A et C)

- [ ] Tableau des événements réseau complété
- [x] `NetworkManager.cs` — `Assets/Game/Networking/` (protocole A : TCP + UDP, `INIT_STATE`, `TAKE`, etc.)
- [ ] Prefab du personnage identifié
- [ ] Liste des questions/blocages à discuter avec A sur le protocole

> **Tip :** Garde la scène MetaVerse ouverte dans Unity pendant toute la phase Cadrage.
> Chaque fois que tu lis une ligne de code, teste son effet en jouant la scène.
