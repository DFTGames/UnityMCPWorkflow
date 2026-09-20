---
tags:
  - gdd
  - content
status: draft
updated: 2026-09-20
---

# Level 02 - Asteroid Belt

Setting: a dense belt of tumbling rock lit by a distant sun (see [[Art Direction]]). Length: about 3.5 minutes. Enemies: Dart, Weaver, Swarm drone, Gunship; splitting and solid meteors. Boss: **Rock Crusher** (below).

## Waves
Counts are for Pilot; the enemy count multiplier scales each group ([[Wave System]]). Built by
`Tools/YASS/Build Levels`: change a wave there and rebuild rather than editing the asset by hand.

| # | Wave | Groups |
| - | ---- | ------ |
| 1 | Rolling stones | 4 large splitting meteors |
| 2 | Drone swarm | 8 Swarm drones, V formation |
| 3 | Darts and rocks | 5 Darts, line + 2 solid meteors |
| 4 | First gunship | 1 Gunship, high + 4 Darts, low |
| 5 | Meteor field | 5 large splitting meteors |
| 6 | Swarm pincer | 6 Swarm drones, column high + 6 more, column low |
| 7 | Weavers | 4 Weavers, staggered |
| 8 | Gunship pair | 2 Gunships, staggered + 6 Swarm drones |
| 9 | Rock storm | 4 large splitting + 2 solid meteors |
| 10 | Pressure | 6 Darts + 4 Swarm drones + 1 Gunship |
| 11 | Final rush | 8 Swarm drones + 6 Darts + 3 large meteors |
| Boss | Rock Crusher | See below |

## Boss: Rock Crusher
A mining rig that throws the belt at you. It hurls rocks while its core is shut and fans bullets while it is open.

| Health | Cycle | Core open | Attacks |
| ------ | ----- | --------- | ------- |
| 260 | 8 s | 3.5 s | Hurl meteor every 1.6 s, and two at a time every 1.4 s below half health; 3-bullet spread every 2 s while open |

The rocks it throws are ordinary splitting meteors, so they break up and can be shot down: the fight is about clearing space, not dodging alone.

Back to [[00 GDD Home]]
