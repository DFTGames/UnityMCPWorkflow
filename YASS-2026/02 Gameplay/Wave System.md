---
tags:
  - gdd
  - gameplay
status: approved
updated: 2026-09-27
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
**Rule: the player is never left with an empty screen.** There is **no gap between waves**: clearing a wave brings the next one on the following fixed step. An earlier version allowed a one-second pause to let a kill land and the screen settle, and that second was the only moment in a level with nothing to shoot, which is the one thing this rule exists to prevent. The explosion and the incoming wave now simply overlap.
- The first wave starts **1 second** after the level begins. That delay stays: it covers the level fading in, and the player has not started playing yet, so it is not dead time in the same sense.
- A wave starts on the **next fixed step** after the previous wave is **cleared** (every hazard it spawned is destroyed or has left the screen), or 10 seconds after the previous wave started, whichever comes first. The step is not pedantry: a clear is noticed in a collision callback, which Unity runs after the step that would have acted on it, so the soonest a wave can follow is 20 ms later. The 10-second cap exists so a straggler drifting off-screen cannot stall the level.
- **This removes the gap between waves and nothing else.** A group still has its own start delay and its members their spacing, so a wave whose first group is delayed shows an empty screen for that long, and a player who kills each meteor of a widely spaced field as it arrives will still wait between them. Those are authored per group in the level assets, and they are where the remaining dead time lives; the rule above is about the seam between waves, not a promise that the screen is never empty.
- After the last wave is cleared (or at most 10 seconds after it started), a **3-second** boss warning plays, then the boss enters.
- A boss does nothing until it has reached its station: its entrance takes a few seconds, and a fight that began while it was still a shape at the edge of the screen would spend its opening attacks where nobody could see them.
- Defeating the boss wins the level. From that moment the player can no longer be hurt; **2.5 seconds** later (time to collect the boss's drops) the level-clear bonus is awarded ([[Scoring]]) and the Sector Clear screen appears ([[UI Flow and Screens]]).

## Difficulty
The difficulty's enemy count multiplier ([[Difficulty and Balancing]]) scales every group's count, rounded to the nearest whole number, minimum 1. Speed and fire-rate multipliers apply to spawned enemies as before.

## Content
- [[Level 01 - Magenta Nebula]]
- [[Level 02 - Asteroid Belt]]
- [[Level 03 - Solar Corona]]
- [[Level 04 - Ice Rings]]
- [[Level 05 - Derelict Fleet]]
- [[Level 06 - Ion Storm]]
- [[Level 07 - Black Holes Edge]]
- [[Level 08 - Enemy Homeworld]]

Back to [[00 GDD Home]]
