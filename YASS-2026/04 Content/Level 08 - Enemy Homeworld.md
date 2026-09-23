---
tags:
  - gdd
  - content
status: draft
updated: 2026-09-22
---

# Level 08 - Enemy Homeworld

Setting: orbit above a world crusted with hive structures (see [[Art Direction]]). Length: about 4.5 minutes. Enemies: every enemy type, in their largest groups. Boss: **The Overmind** (below).

## Waves
Counts are for Pilot; the enemy count multiplier scales each group ([[Wave System]]). The level is
a `LevelDefinition` asset: change a wave by editing it in the Inspector.

| # | Wave | Groups |
| - | ---- | ------ |
| 1 | Defence line | 8 Darts + 8 Swarm drones, V |
| 2 | Interceptors | 6 Divers, staggered |
| 3 | Gun line | 3 Gunships, staggered |
| 4 | Crossfire | 4 Snipers, staggered |
| 5 | Frigate wall | 3 Frigates, staggered |
| 6 | Minefield | 4 Mine Layers, staggered |
| 7 | Bombardment | 5 large splitting + 3 solid meteors |
| 8 | The swarm | 8 Swarm drones high + 8 low, both V |
| 9 | Mixed assault | 2 Frigates + 3 Divers + 2 Gunships |
| 10 | Sniper nest | 4 Snipers + 6 Weavers |
| 11 | Pressure | 3 Mine Layers + 4 Divers + 8 Swarm drones |
| 12 | Final rush | 3 Frigates + 3 Gunships + 4 Snipers |
| 13 | Last stand | 10 Darts + 8 Swarm drones + 4 large meteors |
| Boss | The Overmind | See below |

## Boss: The Overmind
Three fights in one, each third of its health remixing an earlier boss.

| Health | Cycle | Core open | Attacks |
| ------ | ----- | --------- | ------- |
| 700 | 11 s | 4.5 s | Above two thirds: Diver launches and 7-bullet spreads (the Hive Carrier). Middle third: beam sweeps and turret volleys (the Sunforge and the Dreadnought), 9-bullet spreads. Last third: dashes, 4-Diver launches and 11-bullet spreads (the Frost Lancer, and everything left) |

Each phase is a band of health rather than a scripted sequence, so the fight changes when the player earns it. The minions it launches are Divers, not Darts: by level 8 the swarm has to commit.

Back to [[00 GDD Home]]
