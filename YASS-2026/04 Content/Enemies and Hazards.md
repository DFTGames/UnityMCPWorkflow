---
tags:
  - gdd
  - content
status: approved
updated: 2026-09-19
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
