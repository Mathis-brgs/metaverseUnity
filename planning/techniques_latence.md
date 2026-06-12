# Techniques de réduction de latence et de bande passante

## Vue d'ensemble de l'architecture réseau

Le jeu utilise deux canaux distincts :
- **TCP port 25000** : événements ponctuels (JOIN, INIT_STATE, PLAYER_JOIN, BONUS_TAKEN, STATE)
- **UDP port 25001** : envoi des inputs joueur vers le serveur (MOVE, INPUT) — faible latence, pas de retransmission nécessaire

Le serveur est un **serveur autoritaire Unity** (Unity Dedicated Server build) déployé sur Fly.io (Paris, région `cdg`), adresse `37.16.22.189`.

---

## Technique 1 — Messages INPUT plutôt que positions (réduction de bande passante + latence)

### Problème
Envoyer la position absolue du joueur (x, y, z, rotY) à chaque frame revient à faire confiance au client pour sa propre position. C'est la méthode naïve ("force-brute").

### Solution implémentée
Le client envoie uniquement son **intention de déplacement** (`ix`, `iz`, `rotY`) :

```json
{ "type": "INPUT", "id": "p1", "ix": 0.0, "iz": 1.0, "rotY": 45.2 }
```

- `ix` / `iz` : direction normalisée [-1, 1] (entrée clavier/joystick)
- Pas de position absolue → **le serveur calcule la vraie position**

### Avantage bande passante
| Message | Champs | Taille approximative |
|---------|--------|----------------------|
| MOVE (force-brute) | type, id, x, y, z, rotY | ~90 octets |
| INPUT (optimisé) | type, id, ix, iz, rotY | ~65 octets |

Réduction de ~30 % par paquet, à 20 Hz = gain significatif sur la durée.

### Fichiers concernés
- `Assets/Game/Networking/NetworkManager.cs` — `TrySendInputTick()`, `SendInput()`
- `Assets/Game/Server/UnityGameServer.cs` — `HandleInput()`

---

## Technique 2 — Physique server-autoritaire (anti-triche + cohérence)

### Problème
Si le client envoie sa position directement, un client malveillant ou laggy peut envoyer n'importe quelle position. Les joueurs peuvent aussi diverger sans se voir.

### Solution implémentée
Le serveur possède un **proxy physique** (`ServerPlayerProxy`) pour chaque joueur : un `Rigidbody` + `CapsuleCollider` Unity qui tourne sur le serveur. Quand un INPUT arrive :

```csharp
// ServerPlayerProxy.cs
void SetInput(float ix, float iz, float rotY)
{
    rb.MovePosition(rb.position + move * Time.fixedDeltaTime);
}
```

Le moteur physique Unity (PhysX) calcule la vraie position en tenant compte des collisions. C'est la **position calculée par le serveur** qui est broadcastée à tous les clients, pas celle envoyée par le client.

### Avantages
- Cohérence garantie entre tous les clients
- Les collisions serveur déclenchent les bonus (triggers `OnTriggerEnter`)
- Autorité sur les voitures (`ServerCarAuthority`)

### Fichiers concernés
- `Assets/Game/Server/ServerPlayerProxy.cs`
- `Assets/Game/Server/ServerBootstrap.cs`

---

## Technique 3 — Broadcast STATE à 20 Hz (pas chaque frame)

### Problème
Broadcaster l'état du monde à chaque frame (60 Hz) génère beaucoup de trafic inutile : les positions changent peu entre deux frames consécutives.

### Solution implémentée
Le serveur accumule les mises à jour physiques (60 Hz) mais n'envoie l'état qu'à **20 Hz** (toutes les 50 ms) :

```csharp
// UnityGameServer.cs
public float StateBroadcastInterval = 0.05f; // 20 Hz

void LateUpdate()
{
    _broadcastTimer += Time.deltaTime;
    if (_broadcastTimer < StateBroadcastInterval) return;
    _broadcastTimer = 0f;
    BroadcastState();
}
```

### Économie de bande passante
À 2 joueurs, un paquet STATE fait ~400 octets.
- 60 Hz → 24 000 octets/s par client
- 20 Hz → **8 000 octets/s par client** (réduction de 67 %)

### Fichiers concernés
- `Assets/Game/Server/UnityGameServer.cs` — `LateUpdate()`, `BroadcastState()`

---

## Technique 4 — Interpolation côté client (fluidité sans latence ajoutée)

### Problème
Les mises à jour de position arrivent à 20 Hz. Sans interpolation, le mouvement des joueurs distants est saccadé (sauts de position toutes les 50 ms).

### Solution implémentée
`RemotePlayerManager` interpole **en continu** vers la dernière position reçue :

```csharp
// RemotePlayerManager.cs — Update()
float t = Time.deltaTime * InterpolationSpeed; // InterpolationSpeed = 12
kvp.Value.transform.position = Vector3.Lerp(
    kvp.Value.transform.position,
    _targetPos[kvp.Key],
    t
);
```

Le mouvement est fluide à 60 FPS même si les mises à jour n'arrivent qu'à 20 Hz. La position cible est mise à jour à chaque STATE reçu.

### Fichiers concernés
- `Assets/Game/Networking/RemotePlayerManager.cs` — `Update()`, `HandleState()`

---

## Technique 5 — Réconciliation locale (correction de divergence)

### Problème
Le joueur local se déplace localement (physique client) pendant que le serveur calcule sa vraie position. Sur réseau lent, les deux peuvent diverger.

### Solution implémentée
À chaque STATE reçu, si la position serveur diverge de plus de `LocalReconcileThreshold` (2,5 m) de la position locale, le client est **téléporté** vers la position autoritaire :

```csharp
// RemotePlayerManager.cs — ReconcileLocal()
if ((local.position - target).sqrMagnitude > threshold * threshold)
    local.position = target;
```

Le seuil de 2,5 m évite les micro-corrections constantes (jitter) tout en corrigeant les dérives importantes.

Note : la réconciliation n'est déclenchée que lorsque le serveur a reçu au moins un INPUT du joueur (sa position dans le WorldState est réelle, pas `(0,0,0)` par défaut).

### Fichiers concernés
- `Assets/Game/Networking/RemotePlayerManager.cs` — `ReconcileLocal()`
- `Assets/Game/Server/UnityGameServer.cs` — `BroadcastState()` (filtre `_udpEndpoints`)

---

## Technique 6 — UDP pour les inputs (latence minimale client → serveur)

### Problème
TCP garantit la livraison mais ajoute de la latence (accusés de réception, retransmissions). Pour les inputs de mouvement, un paquet légèrement perdu est moins grave qu'un paquet retardé.

### Solution implémentée
- **Client → Serveur** : MOVE et INPUT sont envoyés en **UDP** (sans garantie, faible latence)
- **Serveur → Client** : STATE, BONUS_TAKEN, PLAYER_JOIN sont envoyés en **TCP** (fiabilité garantie)

```csharp
// NetworkManager.cs
_udp = new UdpClient(0); // port aléatoire local
_udpServerEp = new IPEndPoint(IPAddress.Parse(ServerHost), UdpServerPort);
// ...
_udp.Send(bytes, bytes.Length, _udpServerEp); // envoi INPUT non-bloquant
```

### Fichiers concernés
- `Assets/Game/Networking/NetworkManager.cs` — `InitUdp()`, `SendInput()`, `SendMove()`
- `Assets/Game/Server/UnityGameServer.cs` — `UdpReceiveLoop()`, `DrainUdpInbox()`

---

## Résumé

| Technique | Gain principal |
|-----------|----------------|
| INPUT (intention) vs MOVE (position) | -30% bande passante, anti-triche |
| Physique server-autoritaire | Cohérence garantie, triggers corrects |
| Broadcast 20 Hz | -67% bande passante serveur → clients |
| Interpolation client | Fluidité 60 FPS sans latence ajoutée |
| Réconciliation locale | Correction divergences, seuil anti-jitter |
| UDP pour inputs | Latence minimale sur le chemin critique |
