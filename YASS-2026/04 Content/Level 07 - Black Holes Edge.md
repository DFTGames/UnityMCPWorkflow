---
tags:
  - gdd
  - content
status: draft
updated: 2026-09-20
---

# Level 07 - Black Hole's Edge

Setting: the rim of a black hole, starlight smeared into arcs around it (see [[Art Direction]]). Length: about 4 minutes. Enemies: every enemy type. Boss: **Singularity Engine** (below).

## Waves
Counts are for Pilot; the enemy count multiplier scales each group ([[Wave System]]). Built by
`Tools/YASS/Build Levels`: change a wave there and rebuild rather than editing the asset by hand.

| # | Wave | Groups |
| - | ---- | ------ |
| 1 | Event horizon | 6 Darts + 4 Swarm drones |
| 2 | Interceptors | 5 Divers, staggered |
| 3 | Crossfire | 3 Snipers, staggered |
| 4 | Frigate wall | 2 Frigates + 6 Swarm drones |
| 5 | Drawn in | 6 solid meteors |
| 6 | Gun line | 3 Gunships, staggered |
| 7 | Mines and weavers | 3 Mine Layers + 4 Weavers |
| 8 | Swarm | 8 Swarm drones high + 8 low, both V |
| 9 | Mixed assault | 2 Frigates + 3 Divers + 2 Snipers |
| 10 | Debris storm | 5 large splitting + 3 solid meteors |
| 11 | Final rush | 3 Gunships + 4 Divers + 8 Swarm drones |
| 12 | Last stand | 2 Frigates + 3 Snipers + 6 Darts |
| Boss | Singularity Engine | See below |

## Boss: Singularity Engine
A gravity well on a hull: it drags the ship towards it, then fills the space it has dragged you into.

| Health | Cycle | Core open | Attacks |
| ------ | ----- | --------- | ------- |
| 560 | 10 s | 4 s | Gravity pull 0.5 s into each armoured phase, 2.5 s long (a second one below half health); 2 solid meteors every 2.2 s; 7-bullet spread every 1.5 s while open |

The pull is 4 units a second at its centre, fading to nothing 16 units out, so the screen edges stay a refuge. It never moves the ship off the playfield, and the player can still fly out of it: half the ship's top speed, not all of it.

Back to [[00 GDD Home]]
