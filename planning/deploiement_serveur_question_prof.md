# Question prof — Déploiement serveur Unity sur Internet

## Contexte
On a migré le serveur vers Unity (GameServerMono) pour gérer la physique / collision côté serveur.
On veut obtenir les 3pts "Serveur déployé et jouable sur Internet".

## Solution technique trouvée
**Unity Dedicated Server build (Linux headless)**
1. Installer le module "Linux Dedicated Server Build Support" dans Unity Hub
2. Créer une scène avec uniquement le composant GameServerMono
3. Build Settings → Dedicated Server → Linux → Build
4. Le binaire Linux tourne sans GPU (physics fonctionne en headless)
5. Dockerfile minimal → deploy sur Fly.io (cdg = Paris, ~10ms latence)

Le serveur Unity hébergé sur Fly.io gère : physique, collisions, positions, scores, bonus.
Les clients Unity se connectent à l'IP Fly.io depuis n'importe quelle connexion internet.

## Question pour le prof
Est-ce que cette approche (Unity Dedicated Server build déployé sur Fly.io) est valide
pour obtenir le point "Serveur déployé sur Internet" ?

Sinon, quelle approche est attendue ?

## Alternative si refus
Déployer le serveur C# console (Server/Program.cs) sur Fly.io — game jouable sur internet
mais sans physique Unity côté serveur.
