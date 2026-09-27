---
tags:
  - gdd
  - content
status: draft
updated: 2026-09-27
---

# Level 06 - Ion Storm

Setting: a charged storm of violet gas and static (see [[Art Direction]]). Length: about 4 minutes. Enemies: Dart, Weaver, Swarm drone, Gunship, Diver, Mine Layer, Frigate, Sniper; splitting and solid meteors. Boss: **Tempest** (below).

## Waves
Counts below are the **authored** values in the level asset, before any difficulty is applied; the enemy count multiplier scales each group ([[Wave System]]). The level is
a `LevelDefinition` asset: change a wave by editing it in the Inspector.

| # | Wave | Groups |
| - | ---- | ------ |
| 1 | Static | 7 Darts, line |
| 2 | First sniper | 1 Sniper + 5 Darts |
| 3 | Crossfire | 2 Snipers, staggered |
| 4 | Swarm and weavers | 9 Swarm drones, V + 5 Weavers |
| 5 | Sniper and gunship | 1 Sniper, high + 1 Gunship, low |
| 6 | Divers | 6 Divers, staggered |
| 7 | Mines and snipers | 2 Mine Layers + 2 Snipers |
| 8 | Frigate wall | 3 Frigates, staggered |
| 9 | Storm debris | 4 large splitting + 3 solid meteors |
| 10 | Pressure | 3 Snipers + 7 Darts |
| 11 | Final rush | 2 Snipers + 3 Frigates + 9 Swarm drones |
| Boss | Tempest | See below |

## Boss: Tempest
A storm generator that throws short lightning arcs across the screen, again and again.

| Health | Cycle | Core open | Attacks |
| ------ | ----- | --------- | ------- |
| 500 | 9 s | 3.5 s | Arc every 2.6 s while armoured (1.6 s sweep, narrower and weaker than the Sunforge beam); 5-bullet spread every 1.2 s while open, 9 below half health |

The arcs come often rather than slowly, so the fight is about constant small movements instead of one big dodge.

Back to [[00 GDD Home]]
