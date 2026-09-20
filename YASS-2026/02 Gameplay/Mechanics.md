---
tags:
  - gdd
  - gameplay
status: approved
updated: 2026-09-20
---

# Mechanics

%% One section per mechanic. For large mechanics, create a dedicated page from the Feature template and link it here. %%

## Scrolling
Horizontal side-scrolling. The player is kept within the visible playfield.

## Movement and aiming
Twin-stick: move freely, and aim independently of movement. Firing is limited to a forward arc (35 degrees either way), as a side-scroller should be; see [[Controls]].

## Lives and health
- The player starts with a number of lives (hearts) set by difficulty, and a **health bar of 100** for each life.
- Enemy damage values are fixed per source and scaled by the difficulty's damage-taken multiplier.
- Health reaching 0 costs a life; the next life starts at full health with **2 seconds of invulnerability**, shown by the ship blinking.
- No lives left: game over.

## Weapon upgrades
Five levels. The player starts at level 1.

| Level | Pattern | Fire rate |
| ----- | ------- | --------- |
| 1 | Single shot | 8 shots/s |
| 2 | Double parallel shot | 8 shots/s |
| 3 | Triple spread (±10°) | 8 shots/s |
| 4 | Triple spread (±10°) | 11 shots/s |
| 5 | Five-way spread (±20°), centre shot pierces | 11 shots/s |

- A weapon upgrade pickup raises the level by one. At level 5 it awards **500 points** instead.
- Losing a life drops the weapon **one** level (never below 1).

## Shield
- Absorbs up to **3 hits** or lasts **10 seconds**, whichever comes first.
- Shown as a bubble around the ship that flickers during its last 2 seconds.
- Collecting another shield refreshes it to full (3 hits, 10 seconds); shields do not stack.
- While shielded, colliding with a non-boss enemy destroys that enemy (and scores it). Colliding with a boss consumes one shield hit.

## Collisions with enemies
- Shielded: see [[#Shield]].
- Unshielded, non-boss enemy: the player takes the enemy's contact damage and the enemy is destroyed (and scores normally). _Provisional: chosen during implementation, awaiting approval._
- Unshielded, boss: the player takes the boss's contact damage.
- Ramming a meteor destroys it without splitting, so fragments cannot immediately hit the ship again. _Provisional._
- While invulnerable after losing a life, the ship passes through enemies harmlessly: no damage, and the enemy is not destroyed. _Provisional._

## Provisional player values
Player speed 8 units/s (kept 0.5 units inside the screen edges); player shots travel 18 units/s, deal 1 damage and spawn 0.55 units from the ship's centre in the aim direction. Kills credited to a player whose game is over (bullets still in flight) score nothing. The visible playfield is 10 units tall. _Provisional, to be tuned in playtesting._

## Meteors
Some split into smaller meteors when destroyed; others take several hits and are destroyed outright. See [[Enemies and Hazards]].

## Enemy waves and bosses
Scripted, escalating waves of enemies entering from the right. One boss ends each campaign level. See [[Enemies and Hazards]] and [[Levels]].

## Scoring
See [[Scoring]].

## Multiplayer readiness
Single player ships first. Game state and input must be separated from presentation so that up to 4-player online co-op with Photon Fusion can be added later. See [[Technical Design]].

Back to [[00 GDD Home]]
