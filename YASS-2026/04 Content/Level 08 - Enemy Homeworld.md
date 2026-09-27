---
tags:
  - gdd
  - content
status: draft
updated: 2026-09-27
---

# Level 08 - Enemy Homeworld

Setting: orbit above a world crusted with hive structures (see [[Art Direction]]). Length: about 4.5 minutes. Enemies: every enemy type, in their largest groups. Boss: **The Overmind** (below).

## Waves
Counts below are the **authored** values in the level asset, before any difficulty is applied; the enemy count multiplier scales each group ([[Wave System]]). The level is
a `LevelDefinition` asset: change a wave by editing it in the Inspector.

| # | Wave | Groups |
| - | ---- | ------ |
| 1 | Defence line | 9 Darts + 9 Swarm drones, V |
| 2 | Interceptors | 7 Divers, staggered |
| 3 | Gun line | 4 Gunships, staggered |
| 4 | Crossfire | 4 Snipers, staggered |
| 5 | Frigate wall | 4 Frigates, staggered |
| 6 | Minefield | 4 Mine Layers, staggered |
| 7 | Bombardment | 6 large splitting + 4 solid meteors |
| 8 | The swarm | 9 Swarm drones high + 9 low, both V |
| 9 | Mixed assault | 2 Frigates + 4 Divers + 2 Gunships |
| 10 | Sniper nest | 5 Snipers + 7 Weavers |
| 11 | Pressure | 3 Mine Layers + 5 Divers + 9 Swarm drones |
| 12 | Final rush | 3 Frigates + 4 Gunships + 4 Snipers |
| 13 | Last stand | 12 Darts + 9 Swarm drones + 5 large meteors |
| Boss | The Overmind | See below |

## Boss: The Overmind
Three fights in one, each third of its health remixing an earlier boss.

| Health | Cycle | Core open | Attacks |
| ------ | ----- | --------- | ------- |
| 700 | 11 s | 4.5 s | Above two thirds: Diver launches and 7-bullet spreads (the Hive Carrier). Middle third: beam sweeps and turret volleys (the Sunforge and the Dreadnought), 9-bullet spreads. Last third: dashes, 4-Diver launches and 11-bullet spreads (the Frost Lancer, and everything left) |

Each phase is a band of health rather than a scripted sequence, so the fight changes when the player earns it. The minions it launches are Divers, not Darts: by level 8 the swarm has to commit.

Back to [[00 GDD Home]]
