---
tags:
  - gdd
  - content
status: draft
updated: 2026-09-22
---

# Level 03 - Solar Corona

Setting: the outer atmosphere of a star, all arcs of plasma and drifting embers (see [[Art Direction]]). Length: about 3.5 minutes. Enemies: Dart, Weaver, Swarm drone, Gunship, Diver; solid and splitting meteors. Boss: **Sunforge** (below).

## Waves
Counts are for Pilot; the enemy count multiplier scales each group ([[Wave System]]). The level is
a `LevelDefinition` asset: change a wave by editing it in the Inspector.

| # | Wave | Groups |
| - | ---- | ------ |
| 1 | Flare runners | 6 Darts, line |
| 2 | First divers | 3 Divers, staggered |
| 3 | Weaver screen | 5 Weavers, staggered |
| 4 | Divers and drones | 4 Divers + 8 Swarm drones, V |
| 5 | Burnt rock | 4 solid meteors |
| 6 | Gun line | 2 Gunships + 5 Darts |
| 7 | Dive pincer | 3 Divers, column high + 3 more, column low |
| 8 | Swarm and weavers | 8 Swarm drones, V + 4 Weavers |
| 9 | Heat | 6 Darts + 3 Divers |
| 10 | Corona storm | 4 large splitting + 2 solid meteors |
| 11 | Final rush | 4 Divers + 8 Swarm drones + 6 Darts |
| Boss | Sunforge | See below |

## Boss: Sunforge
A forge-ship that sweeps a flame beam across the screen while its core is shut.

| Health | Cycle | Core open | Attacks |
| ------ | ----- | --------- | ------- |
| 320 | 9 s | 4 s | Beam sweep 0.6 s into each armoured phase, and a second at 2.8 s below half health; 5-bullet spread every 1.8 s while open |

The beam turns steadily from 140 to 220 degrees over 2 seconds and burns anything it rests on every half second, so the answer is to cross it rather than sit ahead of it.

A sweep has to finish inside the armoured phase that started it, which is why the sweep is 2 seconds rather than 2.5: at 2.5 the second sweep of its enraged half would have nowhere to happen, and would simply never occur.

Back to [[00 GDD Home]]
