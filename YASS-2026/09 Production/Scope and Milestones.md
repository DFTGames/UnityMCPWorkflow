---
tags:
  - gdd
  - production
status: review
updated: 2026-09-20
---

# Scope and Milestones

## Scope
**In:** single-player campaign (8 levels, 8 bosses), Endless mode, 3 difficulties, pickups (health, extra life, weapon upgrades, shield), UGS online leaderboards, PC, mobile and WebGL builds.

**Out (for now):** online co-op (architecture prepared for Photon Fusion), local co-op, monetisation.

## Prototype (current)
Playable `Prototype` scene: ship movement and twin-stick or mouse firing, Dart and Weaver enemies, splitting and solid meteors, pickups, HUD and game over. A temporary spawner picks random hazards every 1.2 s (divided by the difficulty's enemy count multiplier) with weights Dart 35, Weaver 25, large splitting meteor 25, solid meteor 15; scripted waves, bosses, touch controls and final art come later.

## Milestones
| Milestone | Goal | Status |
| --------- | ---- | ------ |
| Prototype | Playable prototype with AI art and engine particles | Done |
| M1: Level 1 slice | Wave system, Level 1 waves, Hive Carrier, level-clear flow | Done |
| M2: Game flow | Title, difficulty select, pause, settings, credits, game over and sector clear screens | Done |
| M3: Game feel | Visual effects, sound effects, music, audio mixer | Done (adaptive intensity layers deferred) |
| M4: Campaign skeleton | Level-to-level progression, carried run state, saved progress, campaign ending, Endless unlock rule | Done |
| M5: Content | The remaining six enemies, levels 2 to 8 and their bosses | In progress (enemies done) |
| Later | Endless mode, UGS leaderboards, touch controls | Not started |

Back to [[00 GDD Home]]
