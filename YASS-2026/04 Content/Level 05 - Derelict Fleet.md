---
tags:
  - gdd
  - content
status: draft
updated: 2026-09-27
---

# Level 05 - Derelict Fleet

Setting: a graveyard of broken warships in cold green light (see [[Art Direction]]). Length: about 4 minutes. Enemies: Dart, Swarm drone, Gunship, Diver, Mine Layer, Frigate; solid meteors. Boss: **Dreadnought** (below).

## Waves
Counts below are the **authored** values in the level asset, before any difficulty is applied; the enemy count multiplier scales each group ([[Wave System]]). The level is
a `LevelDefinition` asset: change a wave by editing it in the Inspector.

| # | Wave | Groups |
| - | ---- | ------ |
| 1 | Salvage | 6 Darts + 3 solid meteors |
| 2 | First frigate | 1 Frigate + 5 Swarm drones |
| 3 | Gun line | 2 Gunships, staggered |
| 4 | Frigate wall | 3 Frigates, staggered |
| 5 | Divers | 5 Divers, staggered |
| 6 | Minefield | 4 Mine Layers, staggered |
| 7 | Escort | 1 Frigate + 2 Gunships |
| 8 | Swarm | 9 Swarm drones, V + 7 Darts |
| 9 | Hulks | 6 solid meteors |
| 10 | Pressure | 2 Frigates + 5 Divers |
| 11 | Final rush | 2 Frigates + 3 Gunships + 9 Swarm drones |
| Boss | Dreadnought | See below |

## Boss: Dreadnought
A battleship that fires from every turret at once and sends drones from its bays.

| Health | Cycle | Core open | Attacks |
| ------ | ----- | --------- | ------- |
| 460 | 10 s | 4 s | Turret volley every 1.4 s (0.9 s below half health); 3 Swarm drones 3 s into each armoured phase; 7-bullet spread every 2 s while open |

**Known deviation:** the GDD describes turrets destroyed one by one. They are modelled as a volley from three fixed muzzles rather than as separate destructible parts; destructible turrets are a later pass ([[Open Questions]]).

Back to [[00 GDD Home]]
