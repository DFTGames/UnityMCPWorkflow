---
tags:
  - gdd
  - gameplay
status: approved
updated: 2026-09-20
---

# Wave System

Replaces the prototype's random spawner. Also the basis for Endless mode later.

## Structure
- Each level is a data asset (`LevelDefinition`): an ordered list of **waves**, followed by a **boss**.
- A wave is one or more **spawn groups**. Each group has: hazard type (enemy or meteor), count, formation, entry height (0 = bottom, 1 = top of the spawn band), spacing between spawns (seconds) and an optional start delay within the wave.
- **Formations:**
  - **Line:** one after another at the same height.
  - **Column:** stacked vertically, entering together.
  - **V:** a V shape pointing left.
  - **Staggered:** alternating above and below the entry height.
  - **Scatter:** one after another, spread across the whole height (used for meteor fields, so a field is one group that scales with difficulty).
- Line, Staggered and Scatter groups with more than one member need a spacing above zero, or members would stack.

## Pacing
**Rule: the player is never left with an empty screen.** Gaps between waves exist to let a kill land and the screen settle, not to make the player wait. Any gap long enough to notice as waiting is a bug in the pacing, not a difficulty setting.
- The first wave starts **1 second** after the level begins.
- A wave starts **1 second** after the previous wave is **cleared** (every hazard it spawned is destroyed or has left the screen), or 10 seconds after the previous wave started, whichever comes first. The 10-second cap exists so a straggler drifting off-screen cannot stall the level.
- After the last wave is cleared (or at most 10 seconds after it started), a **3-second** boss warning plays, then the boss enters.
- Defeating the boss wins the level. From that moment the player can no longer be hurt; **2.5 seconds** later (time to collect the boss's drops) the level-clear bonus is awarded ([[Scoring]]) and the Sector Clear screen appears ([[UI Flow and Screens]]).

## Difficulty
The difficulty's enemy count multiplier ([[Difficulty and Balancing]]) scales every group's count, rounded to the nearest whole number, minimum 1. Speed and fire-rate multipliers apply to spawned enemies as before.

## Content
- [[Level 01 - Magenta Nebula]]

Back to [[00 GDD Home]]
