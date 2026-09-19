---
tags:
  - gdd
  - gameplay
status: approved
updated: 2026-09-19
---

# Core Loop

## Game modes
- **Campaign:** 8 levels, each about 3 to 4 minutes of escalating enemy waves and meteor fields, ending with a boss. Beating the level 8 boss wins the game. Full campaign: about 40 to 50 minutes.
- **Endless:** unlocked by completing the campaign. See [[#Endless mode]].

## Moment-to-moment
The screen scrolls horizontally. The player moves freely and aims and fires independently (twin-stick), destroying enemies and meteors while dodging bullets and collisions, and collecting pickups.

## Session loop
Pick mode and difficulty, play a run until victory or game over, submit the score to the leaderboard, retry.

## Long-term loop
Beat the campaign on higher difficulties, unlock Endless, climb the leaderboards.

## Win and lose conditions
- Lose: all lives lost (see [[Mechanics]]).
- Win (Campaign): defeat the final boss of level 8.
- Endless has no win condition; the score is the result.

## Endless mode
- Play runs in **cycles** of 10 waves followed by a boss chosen at random from the 8 campaign bosses.
- Each completed cycle adds **+10% enemy speed** and **+15% enemy count**, each capped at **×2** of the cycle 1 values, and **+×0.25** to the score multiplier (uncapped).
- The background moves on to the next level's setting every cycle, looping after level 8.
- Difficulty modifiers ([[Difficulty and Balancing]]) apply on top.

Back to [[00 GDD Home]]
