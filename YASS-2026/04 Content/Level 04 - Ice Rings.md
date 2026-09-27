---
tags:
  - gdd
  - content
status: draft
updated: 2026-09-27
---

# Level 04 - Ice Rings

Setting: the ring system of a gas giant, pale ice shards against a banded planet (see [[Art Direction]]). Length: about 3.5 minutes. Enemies: Dart, Weaver, Swarm drone, Gunship, Diver, Mine Layer; splitting and solid meteors. Boss: **Frost Lancer** (below).

## Waves
Counts below are the **authored** values in the level asset, before any difficulty is applied; the enemy count multiplier scales each group ([[Wave System]]). The level is
a `LevelDefinition` asset: change a wave by editing it in the Inspector.

| # | Wave | Groups |
| - | ---- | ------ |
| 1 | Ice shards | 6 large splitting meteors |
| 2 | First layer | 1 Mine Layer + 5 Darts |
| 3 | Drone wedge | 9 Swarm drones, V |
| 4 | Minefield | 2 Mine Layers, staggered |
| 5 | Divers | 5 Divers, staggered |
| 6 | Gunship and mines | 1 Gunship + 2 Mine Layers |
| 7 | Ring debris | 5 solid + 3 large splitting meteors |
| 8 | Weaver screen | 7 Weavers, staggered |
| 9 | Mixed | 2 Mine Layers + 7 Darts + 5 Swarm drones |
| 10 | Dive storm | 6 Divers, staggered |
| 11 | Final rush | 2 Gunships + 3 Mine Layers + 9 Swarm drones |
| Boss | Frost Lancer | See below |

## Boss: Frost Lancer
A lance that winds up, then dashes the whole width of the screen and comes back.

| Health | Cycle | Core open | Attacks |
| ------ | ----- | --------- | ------- |
| 360 | 9 s | 3 s | Dash 0.5 s into each armoured phase, and a second at 3.2 s below half health; 3-bullet spread every 1.4 s while open |

The wind-up is 0.7 seconds of holding still: that is the tell. It crosses at 22 units a second, so it cannot be outrun, only left, and returns to its station at 14.

A dash takes about two and a half seconds from wind-up to station. Its armoured phase is six seconds long so that both dashes of its second half fit inside one: an attack that cannot finish before its phase ends is simply lost, and the boss would stop escalating without anything saying so.

Back to [[00 GDD Home]]
