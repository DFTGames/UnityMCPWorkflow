---
tags:
  - gdd
  - gameplay
status: approved
updated: 2026-09-19
---

# Scoring

## Point values
| Source | Points |
| ------ | ------ |
| Small enemy | 100 |
| Medium enemy | 250 |
| Large enemy | 500 |
| Splitting meteor: large / medium / small | 50 / 30 / 20 |
| Solid meteor | 150 |
| Boss | 10,000 × level number |
| Weapon upgrade collected at level 5 | 500 |
| Level clear | remaining health % × 20 (maximum 2,000) |
| Boss defeated without taking damage | 5,000 |

Enemy size classes are listed in [[Enemies and Hazards]].

## Chain
- Each kill within **1.5 seconds** of the previous kill adds **×0.1** to the chain multiplier, up to **×3.0**. The chain starts at ×1.0.
- The chain resets to ×1.0 when the 1.5 second window runs out or when the player takes damage (a hit absorbed by the shield does not count as damage).

## Final score
Points awarded = base points × chain multiplier × difficulty score multiplier ([[Difficulty and Balancing]]) × Endless cycle multiplier ([[Core Loop#Endless mode]], ×1.0 in Campaign). Bonuses (level clear, no-damage boss, level 5 upgrade) are multiplied by difficulty and Endless multipliers but not by the chain.

## Leaderboards
Separate Unity Gaming Services leaderboards per mode (Campaign, Endless) and difficulty: 6 boards.

Back to [[00 GDD Home]]
