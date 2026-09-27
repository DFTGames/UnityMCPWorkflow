---
tags:
  - gdd
  - gameplay
status: approved
updated: 2026-09-27
---

# Difficulty and Balancing

## Difficulty levels
Three difficulties: **Cadet**, **Pilot** and **Ace**. Leaderboards are kept separately per difficulty and mode.

## Tuning values
_The authoritative value lives here; code and data assets must match._

| Parameter | Cadet | Pilot | Ace |
| --------- | ----- | ----- | --- |
| Starting lives | 5 | 3 | 2 |
| Health per life | 100 | 100 | 100 |
| Damage taken multiplier | ×0.75 | ×1.0 | ×1.5 |
| Enemy speed and bullet speed multiplier | ×0.85 | ×1.0 | ×1.2 |
| Enemy count multiplier | ×0.75 | ×1.0 | ×1.3 |
| Enemy fire rate multiplier | ×0.7 | ×1.0 | ×1.3 |
| Pickup drop chance multiplier | ×1.25 | ×1.0 | ×0.75 |
| Score multiplier | ×1.0 | ×1.5 | ×2.5 |

**Do not use the enemy count multiplier to change how full the game is.** Its job is the spacing between the three difficulties, and it is a blunt instrument for anything else, because it is applied **per group and rounded** ([[Wave System]]) and the groups are small (median four). Raising all three by 15% was tried on 27 September 2026 and delivered Cadet +9.2%, Pilot +14.0% and Ace +11.1%: groups of one or two gained nothing on any difficulty, Level 01's final Dart rush was unchanged on Ace, and the gap between Cadet and Pilot silently narrowed. It was reverted. **To change how many enemies the game has, change the authored counts in the level assets**, where the number is exact and every difficulty scales from it in proportion.

Other values (weapon, shield, invulnerability, drops, scoring) are on [[Mechanics]], [[Items and Pickups]] and [[Scoring]].

## Difficulty curve
Waves escalate within a level and across the 8 levels; Endless escalates per cycle ([[Core Loop#Endless mode]]). The game should stay hard: the original was known for most players not surviving.

Back to [[00 GDD Home]]
