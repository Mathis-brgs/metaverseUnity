# Planning — MetaVerse MMO | 4 développeurs

## Calendrier

| Phase | Dates |
|---|---|
| Cadrage | Lun 1 – Mar 2 juin |
| Réseau Core | Mer 3 – Jeu 4 juin |
| MVP | Ven 5 & Lun 8 juin |
| Features+ | Mar 9 – Mer 10 juin |
| Finitions | Jeu 11 juin |
| Présentation | Ven 12 juin |


---

## Répartition des tâches

| | A — Serveur Core | B — Client Réseau Unity | C — Logique Jeu | D — Serveur Logique & Autorité |
|---|---|---|---|---|
| **Cadrage** Lun 1–2 | Design protocole + choix TCP/UDP + format messages | Étude scène MetaVerse + code réseau existant | Design `WorldState` (joueurs, bonus) + setup Git | Lire design de A et C, lister tous les cas limites |
| **Réseau Core** Mer 3–4 | Serveur TCP standalone C# (multi-clients, broadcast) | `NetworkManager.cs` : connexion + envoi/réception | `PlayerState`, `BonusState`, `WorldState` | Routage des messages entrants vers les méthodes de C |
| **MVP** Ven 5 & Lun 8 | Gestion connexions/déconnexions + broadcast état | Sync position force-brute + réception positions | Sync bonus côté serveur | Validation actions joueurs + broadcast autorisé |
| **Features+** Mar 9–10 | TCP/UDP en parallèle + `TryCollectBonus` thread-safe | Interpolation positions joueurs distants | Objets partagés + spawn autres joueurs côté client | Gestion déconnexion propre + cleanup `WorldState` |
| **Finitions** Jeu 11 | `MSG_WELCOME` complet + déploiement cloud | UI connexion (IP/port/erreurs) | Graphismes + polish | Tests serveur multi-clients + monitoring connexions |
| **Présentation** Ven 12 | Démo live + explication protocole | Explication client réseau | Explication WorldState | Explication gestion déconnexions + robustesse |

---

## Tests collectifs

| Moment | Test |
|---|---|
| Fin Jeu 4 | Réseau 2 machines — A + B + C + D |
| Fin Lun 8 | MVP 4 joueurs simultanés |
| Jeu 11 soir | Run complet bout-en-bout avant présentation |
