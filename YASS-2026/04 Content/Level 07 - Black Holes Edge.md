---
tags:
  - gdd
  - content
status: draft
updated: 2026-09-27
---

# Level 07 - Black Hole's Edge

Setting: the rim of a black hole, starlight smeared into arcs around it (see [[Art Direction]]). Length: about 4 minutes. Enemies: every enemy type. Boss: **Singularity Engine** (below).

## Waves
Counts below are the **authored** values in the level asset, before any difficulty is applied; the enemy count multiplier scales each group ([[Wave System]]). The level is
a `LevelDefinition` asset: change a wave by editing it in the Inspector.

| # | Wave | Groups |
| - | ---- | ------ |
| 1 | Event horizon | 7 Darts + 4 Swarm drones |
| 2 | Interceptors | 6 Divers, staggered |
| 3 | Crossfire | 4 Snipers, staggered |
| 4 | Frigate wall | 2 Frigates + 7 Swarm drones |
| 5 | Drawn in | 7 solid meteors |
| 6 | Gun line | 3 Gunships, staggered |
| 7 | Mines and weavers | 4 Mine Layers + 4 Weavers |
| 8 | Swarm | 9 Swarm drones high + 10 low, both V |
| 9 | Mixed assault | 2 Frigates + 3 Divers + 3 Snipers |
| 10 | Debris storm | 5 large splitting + 4 solid meteors |
| 11 | Final rush | 3 Gunships + 5 Divers + 9 Swarm drones |
| 12 | Last stand | 2 Frigates + 4 Snipers + 7 Darts |
| Boss | Singularity Engine | See below |

## Boss: Singularity Engine
A gravity well on a hull: it drags the ship towards it, then fills the space it has dragged you into.

| Health | Cycle | Core open | Attacks |
| ------ | ----- | --------- | ------- |
| 560 | 10 s | 4 s | Gravity pull 0.5 s into each armoured phase, 2.5 s long (a second one below half health); 2 solid meteors every 2.2 s; 7-bullet spread every 1.5 s while open |

The pull is 4 units a second at its centre, fading to nothing 16 units out, so the screen edges stay a refuge. It never moves the ship off the playfield, and the player can still fly out of it: half the ship's top speed, not all of it.

Back to [[00 GDD Home]]
