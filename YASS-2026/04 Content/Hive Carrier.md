---
tags:
  - gdd
  - content
status: approved
updated: 2026-09-19
---

# Hive Carrier

Boss of [[Level 01 - Magenta Nebula]].

## Behaviour
- Appears 3 units beyond the right edge at mid-height, flies in at 2 units/s, then holds 3 units in from the right edge, drifting up and down by 2.2 units over a 6-second cycle.
- **Armoured hull:** takes no damage and stops every shot, including the piercing centre shot of weapon level 5. Only the **core** can be damaged.
- **Core cycle:** the core opens for **4 seconds** in every **8**.
  - While **closed**: launches **3 Darts** from its bays.
  - While **open**: fires a **5-bullet aimed spread** every **1.5 seconds**.
- **Enraged** (health at or below 50%): launches faster (every cycle launches twice) and the spread widens to **7 bullets**.

## Stats
| Value | Amount |
| ----- | ------ |
| Health | 200 (not scaled by difficulty) |
| Contact damage | 40 |
| Spread bullet speed / damage | 6 / 10 (speed scales with difficulty) |
| Spread angle | 60 degrees across the whole fan |
| Score | 10,000 × level (1) + no-damage bonus ([[Scoring]]) |
| Drops | Weapon upgrade + health ([[Items and Pickups]]) |

Ramming follows the boss rules in [[Mechanics]]. Because the ship can stay overlapping the boss, after a boss collision hurts the player (or hits the shield) further boss contact is ignored for **1 second**. _Provisional._

The armour is three colliders around the core, so shots aimed at the open core always reach it. Darts launched from the bays belong to no wave.

Back to [[00 GDD Home]]
