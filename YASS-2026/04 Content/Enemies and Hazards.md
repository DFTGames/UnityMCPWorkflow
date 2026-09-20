---
tags:
  - gdd
  - content
status: approved
updated: 2026-09-20
---

# Enemies and Hazards

## Meteors
| Type | Behaviour | Points |
| ---- | --------- | ------ |
| Splitting meteor (large, medium, small) | Large splits into medium, medium into small; small is destroyed. | 50 / 30 / 20 |
| Solid meteor | Takes several hits, then is destroyed outright. | 150 |

## Enemies
| Name | Size | Behaviour | First level |
| ---- | ---- | --------- | ----------- |
| Dart | Small | Fast, flies straight, does not shoot. | 1 |
| Weaver | Small | Sine-wave path, occasional aimed shot. | 1 |
| Swarm drone | Small | Groups of 8 in formation. | 2 |
| Gunship | Large | Slow; stops in the right third of the screen and fires 3-shot bursts. | 2 |
| Diver | Medium | Enters, locks onto the player's position, then charges. | 3 |
| Mine Layer | Medium | Drops proximity mines. | 4 |
| Frigate | Large | Front shield; must be hit from the sides or behind (uses twin-stick aiming). | 5 |
| Sniper | Medium | Shows a warning line, then fires a laser. | 6 |

Size sets points ([[Scoring]]).

### Provisional prototype values
Base values before difficulty scaling; stored in `Assets/_Game/ScriptableObjects/`. _Provisional, to be tuned in playtesting._

| Name | Health | Speed | Contact damage | Motion | Weapon |
| ---- | ------ | ----- | -------------- | ------ | ------ |
| Dart | 1 | 7 | 20 | Straight | None |
| Weaver | 3 | 3 | 20 | Sine wave, amplitude 1.5, 0.4 Hz | Aimed shot every 2 s (first after 0.8 s), speed 6, damage 10 |
| Swarm drone | 1 | 6 | 15 | Straight; spawned eight at a time in a wedge or line ([[Wave System]] formations) | None |
| Gunship | 12 | 2.5 | 30 | Flies in, stops a third of the way in from the right edge | Bursts of 3 shots 0.15 s apart, 2.2 s between bursts, speed 7, damage 10 |
| Diver | 4 | 3 | 30 | Enters for 0.8 s, stops and locks on for 0.6 s, then charges at 12 along the line it locked | None |
| Mine Layer | 5 | 2.5 | 20 | Straight, dropping a mine every 1.6 s | Mines (below) |
| Frigate | 16 | 2 | 30 | Straight and slow | None: a shielded wall to be flown around |
| Sniper | 3 | 3 | 20 | Flies in, stops a quarter of the way in from the right edge | Warns for 1.2 s along a fixed line, then a 0.25 s beam (damage 20, half-width 0.15), 1.5 s before the next |

### The new enemies' rules
%% Implemented in Core (EnemyBehaviours.cs) and driven by EnemyView. %%
- **The Diver commits.** It takes its aim at the end of the lock and cannot steer after that, so stopping still is the tell and moving is the answer.
- **The Sniper's line is fixed** when the warning starts and the beam never follows the player; the warning is the whole fight. The line is shown from the moment it is chosen, fading up as the shot nears, and goes out between shots. The beam is a line, not a projectile: it hits the instant it fires, once per shot.
- **Difficulty scales every clock, not just the guns.** A harder run means a faster charge, mines laid sooner and a shorter warning; the beam's own quarter-second is fixed.
- **The Frigate's shield covers 70 degrees either side of its nose.** Shots into its front are turned away, so it has to be hit from the side or behind: this is what the twin-stick aiming is for ([[Controls]]).
- **Mines arm after 0.6 s**, so a Mine Layer cannot kill the ship chasing it, trigger within 1.5 units, deal 25 damage, and expire after 12 s if nobody comes near. A mine can also be shot down (50 points), but the blast is the same wherever it is set off, so clearing one from close range costs health: mines are laid to be flown around, not tidied up. They drift left with the field and belong to no wave, so a wave is never held open by mines nobody went near.

| Meteor | Health | Speed | Contact damage | Size (units) |
| ------ | ------ | ----- | -------------- | ------------ |
| Splitting, large | 4 | 1.5 to 2.5 | 25 | 1.6 |
| Splitting, medium | 2 | 2 to 3 | 15 | 1.0 |
| Splitting, small | 1 | 2.5 to 3.5 | 10 | 0.6 |
| Solid | 8 | 1 to 2 | 30 | 1.4 |

Fragments fly off at 25 degrees either side of the parent's direction, at a speed drawn from their own size's range. Meteors also drift up to 0.5 units/s vertically and spin up to 60 degrees/s; horizontal and vertical speed (not spin) scale with the difficulty's enemy speed multiplier. Wave-flying enemies spawn far enough from the top and bottom edges that their whole path stays on screen.

## Bosses
One per campaign level. See [[Levels]].

Back to [[00 GDD Home]]
