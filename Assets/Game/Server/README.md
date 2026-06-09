# Serveur Unity dédié — MetaVerse

Serveur de jeu **autoritaire** intégré à Unity. Remplace l'ancien serveur console C# (`Server/`).
Il charge la scène MetaVerse et fait autorité sur la Physics : déplacements (collisions murs/obstacles),
collecte des bonus (triggers serveur) et conduite des voitures.

## Composants

| Script | Rôle |
|--------|------|
| `ServerMode` | Détecte si l'instance tourne en serveur (`-server`, batchmode, build `UNITY_SERVER`, ou toggle éditeur) |
| `ServerBootstrap` | Au chargement de scène : désactive caméras/audio/contrôles/UI client, démarre le serveur, branche les autorités, spawn les proxies joueurs |
| `UnityGameServer` | TCP (accept + lecture par `\n`) + UDP (thread de réception) + WorldState + broadcast `STATE` 20 Hz |
| `ServerPlayerProxy` | Rigidbody + capsule autoritaire par joueur (applique `INPUT`, écrit la position dans le WorldState) |
| `ServerBonusAuthority` | Enregistre les positions réelles des bonus de la scène dans le WorldState |
| `ServerCarAuthority` | Enregistre les voitures, valide `CAR_ENTER`/`CAR_EXIT`, conduit les voitures occupées |

`Bonus.cs` (trigger Physics) et `DrivableCar.cs` (pilotage réseau) contiennent les branches serveur.

## Tester dans l'éditeur

1. Menu **`MetaVerse/Server/Force Server In Editor`** (coché).
2. Ouvrir `Assets/Demos/MetaVerse/MetaVerse.unity`, appuyer sur **Play** → cette instance devient le serveur.
3. Lancer des clients (autre éditeur ou build) avec `Force Server In Editor` décoché.

## Build serveur dédié (production)

1. Menu **`MetaVerse/Server/Add MetaVerse Scene To Build`**.
2. Menu **`MetaVerse/Server/Build Dedicated Server`**, choisir le dossier de sortie.
3. Lancer :

```bash
./MetaverseServer -batchmode -nographics
```

Le serveur écoute sur TCP **25000** et UDP **25001**. Ouvrir ces ports (LAN ou VPS).

## Pré-requis de scène

- Les `Bonus` doivent être sur un layer inclus dans leur `CollisionLayers` (par défaut layer 6) — le proxy
  joueur est placé sur `CharacterLayer` (6) pour déclencher les triggers.
- Les voitures sont les `DrivableCar` de la scène (détectées automatiquement).
