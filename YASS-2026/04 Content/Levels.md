---
tags:
  - gdd
  - content
status: approved
updated: 2026-09-23
---

# Levels

%% Give each level its own page once designed in detail. %%

The campaign has **8 levels**, each about 3 to 4 minutes of waves ending with a boss. Endless mode reuses the settings and bosses ([[Core Loop#Endless mode]]).

| # | Setting | Boss | Boss signature |
| - | ------- | ---- | -------------- |
| 1 | [[Level 01 - Magenta Nebula|Magenta Nebula]] | [[Hive Carrier]] | Launches Darts; exposed weak core. |
| 2 | [[Level 02 - Asteroid Belt|Asteroid Belt]] | Rock Crusher | Hurls meteors. |
| 3 | [[Level 03 - Solar Corona|Solar Corona]] | Sunforge | Sweeping flame beams. |
| 4 | [[Level 04 - Ice Rings|Ice Rings]] | Frost Lancer | Fast dashes across the screen. |
| 5 | [[Level 05 - Derelict Fleet|Derelict Fleet]] | Dreadnought | Multiple turrets (fired as a volley; destructible turrets are an [[Open Questions|open question]]). |
| 6 | [[Level 06 - Ion Storm|Ion Storm]] | Tempest | Lightning arcs that make parts of the screen dangerous. |
| 7 | [[Level 07 - Black Holes Edge|Black Hole's Edge]] | Singularity Engine | Gravity pull on the player. |
| 8 | [[Level 08 - Enemy Homeworld|Enemy Homeworld]] | The Overmind | Multi-phase fight remixing earlier bosses' attacks. |

New enemy types are introduced per the "First level" column in [[Enemies and Hazards]].

Every boss is the same behaviour driven by different data: health, how long its core stays shut, and a table of
attacks scoped to a phase and a band of its remaining health (`BossDefinition` assets, built by
its `BossDefinition` asset). A boss's signature is which attacks it has and when, not a class of its own.

**Damage:** a shot into the armoured hull is worth **half**, and a shot into the open core is worth **full**
(both fractions are per boss, in its definition). A shot into a closed core hits the shutters over it and counts
as a hull hit. The core is therefore what a good player aims for, not what an average one has to wait for: with
the earlier rule, where the hull absorbed everything, a boss fight was mostly a wait for the next opening.

## A level is data, not a scene
All eight levels are played in the one level scene. Each is a `LevelDefinition` asset holding its waves, its
backdrop and its boss, and `Campaign.asset` lists them in play order; the run says which one is playing.
Adding or retuning a level means editing an asset in the Inspector, not generating a scene.

Back to [[00 GDD Home]]
