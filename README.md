## Bonus
- Ajout d'autres cubes sur la map
- Affiche un message quand un joueur prend un cube
- Ajout d'un panneau de score en haut à gauche
- Pouvoir déplacer la vue de la caméra
- Prendre un cube augmente le déplacement du personnage (augmentation progressive selon le nombre de cube récupérer, max : 2f)
## Les joueurs 
- Les joueurs peuvent prendre la voiture (ne peux pas prendre de cube dans une voiture)
- Les joueurs peuvent se battre (au bout de 3 coups, l'autre tombe = ralentissement)
- changement sur le mouvement des joueurs (en rapport avec la caméra)
## Les voitures
- Les voitures s'arrêtent devant les joueurs et reprennent la route quand il n'y a plus d'obstacle
- 
## Réseau
Les états réseau ont été ajoutés :
- PlayerState : informations du joueur, position, rotation et score
- BonusState : informations du bonus, position et s’il est déjà ramassé
- WorldState : état complet du monde avec les joueurs et les bonus

- Les éléments du jeu sont dynamique et s'adapte selon le nombre de joueur