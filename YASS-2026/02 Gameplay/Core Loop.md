---
tags:
  - gdd
  - gameplay
status: approved
updated: 2026-09-20
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

## Campaign runs (as built)
A campaign is **one run through the levels**, not a series of separate games.
- Lives, health and weapon level carry from each cleared level into the next; score and kills accumulate across the whole run, and the results screens show the run's totals rather than the level's.
- Clearing a level shows **Sector Clear**, and Continue moves on to the next. Clearing the last one shows the **campaign ending** with the final totals.
- **Retry** after a game over replays the current level, keeping what the run has banked from earlier ones.
- Progress is saved per difficulty: how many levels have been cleared, and whether the campaign has been finished. Finishing on any difficulty unlocks Endless; replaying an early level never lowers what has been reached.
- The campaign's levels are listed in `Assets/_Game/Resources/Campaign.asset`, so the run is won after the last level in that list. All eight are built, so a campaign run is the full eight levels.

## Win and lose conditions
- Lose: all lives lost (see [[Mechanics]]).
- Win (Campaign): defeat the final boss of level 8.
- Endless has no win condition; the score is the result.

## Endless mode
- Play runs in **cycles** of 10 waves followed by a boss chosen at random from the 8 campaign bosses.
- Each completed cycle adds **+10% enemy speed** and **+15% enemy count**, each capped at **×2** of the cycle 1 values, and **+×0.25** to the score multiplier (uncapped).
- The background moves on to the next level's setting every cycle, looping after level 8.
- Difficulty modifiers ([[Difficulty and Balancing]]) apply on top.

### As built
- **Waves are drawn from the campaign, not authored again.** Each cycle picks its ten waves at random from the
  levels up to **its cycle number plus one**, capped at what exists: cycle 1 draws from levels 1 and 2, and by
  cycle 7 anything can turn up. A run therefore opens at about the campaign's difficulty and stops being
  predictable after that. The same wave can be drawn twice in a cycle.
- **The boss** is drawn from all eight however early the cycle is, but never the same one twice running, so a run
  cannot stall on one fight. A boss is worth its own level's points ([[Scoring]]), multiplied by the cycle.
- **One scene, no breaks.** Endless is a single scene: beating a boss starts the next cycle where the last one
  ended, with the sky changed and the multipliers raised. There is no Sector Clear between cycles, and the only
  way out is running out of lives. The results screen reports the cycle reached along with the score.
- The HUD shows the cycle; the campaign's HUD has nothing there.
- **Entering it:** the difficulty screen's Endless entry is a switch rather than a fourth difficulty (Endless
  needs one too, since the leaderboards are per mode and difficulty). Turn it on, then pick Cadet, Pilot or Ace.
  It is dead until the campaign has been finished on any difficulty. _Provisional: the screen's shape is not in
  [[UI Flow and Screens]] yet._

Back to [[00 GDD Home]]
