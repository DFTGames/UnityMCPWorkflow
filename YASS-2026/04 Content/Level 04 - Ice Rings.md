---
tags:
  - gdd
  - content
status: draft
updated: 2026-09-22
---

# Level 04 - Ice Rings

Setting: the ring system of a gas giant, pale ice shards against a banded planet (see [[Art Direction]]). Length: about 3.5 minutes. Enemies: Dart, Weaver, Swarm drone, Gunship, Diver, Mine Layer; splitting and solid meteors. Boss: **Frost Lancer** (below).

## Waves
Counts are for Pilot; the enemy count multiplier scales each group ([[Wave System]]). The level is
a `LevelDefinition` asset: change a wave by editing it in the Inspector.

| # | Wave | Groups |
| - | ---- | ------ |
| 1 | Ice shards | 5 large splitting meteors |
| 2 | First layer | 1 Mine Layer + 4 Darts |
| 3 | Drone wedge | 8 Swarm drones, V |
| 4 | Minefield | 2 Mine Layers, staggered |
| 5 | Divers | 4 Divers, staggered |
| 6 | Gunship and mines | 1 Gunship + 2 Mine Layers |
| 7 | Ring debris | 4 solid + 3 large splitting meteors |
| 8 | Weaver screen | 6 Weavers, staggered |
| 9 | Mixed | 2 Mine Layers + 6 Darts + 4 Swarm drones |
| 10 | Dive storm | 5 Divers, staggered |
| 11 | Final rush | 2 Gunships + 3 Mine Layers + 8 Swarm drones |
| Boss | Frost Lancer | See below |

## Boss: Frost Lancer
A lance that winds up, then dashes the whole width of the screen and comes back.

| Health | Cycle | Core open | Attacks |
| ------ | ----- | --------- | ------- |
| 360 | 9 s | 3 s | Dash 0.5 s into each armoured phase, and a second at 3.2 s below half health; 3-bullet spread every 1.4 s while open |

The wind-up is 0.7 seconds of holding still: that is the tell. It crosses at 22 units a second, so it cannot be outrun, only left, and returns to its station at 14.

A dash takes about two and a half seconds from wind-up to station. Its armoured phase is six seconds long so that both dashes of its second half fit inside one: an attack that cannot finish before its phase ends is simply lost, and the boss would stop escalating without anything saying so.

Back to [[00 GDD Home]]
