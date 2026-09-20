---
tags:
  - gdd
  - gameplay
status: approved
updated: 2026-09-20
---

# Controls

%% Must stay in sync with the project's Input System actions asset (to be created under Assets/_Game). %%

## Schemes
- **Gamepad:** twin-stick. Left stick moves; the right stick aims and fires automatically when deflected beyond a **30% dead zone**. The **right trigger** fires straight forward (to the right) for players who prefer not to aim.
- **Mouse and keyboard:** WASD or arrow keys move, the mouse aims, the left mouse button fires.
- **Touch (mobile):** two virtual sticks. Left thumb moves, right thumb aims and fires while held.

## Input mapping
| Action | Keyboard and mouse | Gamepad | Touch |
| ------ | ------------------ | ------- | ----- |
| Move | WASD / arrow keys | Left stick | Left virtual stick |
| Aim | Mouse position | Right stick | Right virtual stick |
| Fire (aimed) | Left mouse button | Right stick beyond 30% dead zone | Right virtual stick held |
| Fire (forward) | | Right trigger | |
| Pause | Esc | Start / Menu | Pause button |
| Confirm (menus) | Enter | A / Cross (south button) | Tap |
| Back (menus) | Esc | B / Circle (east button) | Back button |

Menu screens are driven by their buttons rather than by a dedicated key: on Game Over, Retry is selected when the panel opens, so Confirm retries. Esc in a level with nothing open opens the pause menu. The on-screen pause button arrives with the touch controls.

If the right stick is deflected while the trigger is held, the shot follows the stick.

## Firing arc and aim tilt
This is a side-scroller, so the ship fires **within a forward arc of 35 degrees either way**, never behind itself (free 360-degree aiming belongs to top-down games).
- **Shots travel along the ship's nose:** the aim is clamped into the arc, and the ship's visible tilt is that same angle, so what the ship points at is what it hits.
- **Aiming outside the arc** clamps to its edge; aiming behind is mirrored onto the forward half (up-and-back fires up-and-forward, straight back fires straight ahead).
- The visuals (sprite and engine exhaust) ease into the tilt and back to level when firing stops. The hitbox never rotates.
- The level 5 spread fans +/-20 degrees around that direction, so its outer shots can sit outside the arc. _Provisional._

## Feel and responsiveness
_To be tuned in playtesting._

Back to [[00 GDD Home]]
