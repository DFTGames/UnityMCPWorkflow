---
tags:
  - gdd
  - gameplay
status: approved
updated: 2026-09-19
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

If the right stick is deflected while the trigger is held, the shot follows the stick.

## Feel and responsiveness
_To be tuned in playtesting._

Back to [[00 GDD Home]]
