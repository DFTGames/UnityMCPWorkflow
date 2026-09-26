---
tags:
  - gdd
  - production
status: draft
updated: 2026-09-25
---

# Open Questions

%% Undecided design questions. Once answered, move the decision into the relevant page and log it in the Changelog. %%

- [ ] Health pickup: confirm the provisional 25 health.
- [ ] **On iOS there is no way to pause on purpose.** [[Controls]] says touch pauses with the "Back button or gesture" and rules out an on-screen button. The `Cancel` action binds `<Keyboard>/escape` and `<Gamepad>/buttonEast` only; Android's Back arrives as Escape so Android is covered, but iOS has no Back button and no gesture is implemented. The only pause left there is the automatic one when the OS backgrounds the app. Add a gesture (a two-finger tap, or an edge swipe), allow an on-screen button on iOS only, or accept it and say so.
- [ ] **Auto-rotation is enabled with all four orientations**, in a side-scroller. Locking to landscape would suit the game, keep the two thumbs where the sticks expect them, and remove a whole class of mid-rotation input bug. Confirm whether portrait play is wanted at all.
- [ ] Co-op (future): chain, pity timer and score are currently shared by all players. Per-player instead? With a shared score, the level-clear bonus is currently awarded once per surviving player.
- [ ] Music loops are 30-second clips that do not loop seamlessly (the generator produces a piece with a beginning and an end). Edit them into true loops, or crossfade a track with itself.
- [ ] Confirm the provisional firing arc: 35 degrees either way, with the ship's tilt showing that same angle.
- [ ] Confirm that the level 5 spread may fan outside the arc (+/-20 degrees around the aim, so up to 55 degrees), or whether the whole spread should be clamped into the arc instead.
- [ ] Confirm provisional boss rule: 1 second of boss-contact immunity after a boss collision hurts the player.
- [ ] Confirm provisional prototype values (player speed and shots, enemy and meteor stats) after playtesting.
- [ ] Confirm provisional rules: rammed meteors do not split; invulnerable ship passes through enemies; shield-absorbed hits keep the no-damage boss bonus; unshielded ramming destroys non-boss enemies; meteors count as kills for the chain and kill count.
- [ ] The Frigate can be destroyed by ramming its shielded nose, for full points (the ram rule destroys any non-boss enemy). Should a front shield turn away a ram as it turns away shots, leaving the player hurt and the Frigate intact?
- [ ] A mine shot down counts as a kill: it scores 50, extends the chain and adds to the HUD kill count. [[Scoring]] says a kill is "any destroyed enemy or meteor", and a mine is neither. Confirm or exclude it.
- [ ] A Gunship or Sniper that is never killed holds its station indefinitely and stays on screen into later waves and the boss fight. Should station-holding enemies leave after a while, and if so how?
- [ ] The Dreadnought's turrets are a volley from three fixed muzzles, not separate destructible parts as [[Levels]] describes. Build destructible turrets, or reword the design?
- [ ] Every level uses the Level 1 music. Eight per-level themes, or a smaller set shared between settings?
- [ ] Boss health runs 200 to 700 across the campaign, and the wave scripts get longer rather than faster. Confirm the curve in playtesting.
- [ ] Endless has no grace period at a cycle boundary: the campaign makes the player untouchable from a boss's defeat until Sector Clear, but in Endless the dead boss's bullets still in the air can take a life while the next cycle starts. Deliberate, or should the boundary be safe?
- [ ] Endless draws its waves from the levels up to its cycle number plus one, so by cycle 7 everything is in the pool and later cycles differ only by the speed and count multipliers, which cap at cycle 11. Does a long run need another source of escalation, or is the score multiplier enough?
- [ ] Enemy prefabs are still held by reference rather than by name. Measured, this costs about 54 KB in total (the prefab assets plus their pooled instances), because their sprites are in the shared atlas page that any one enemy keeps loaded, so there is nothing to reclaim unless the atlas is split further. Worth revisiting only if the enemy roster grows enough to need more than one page.
- [ ] Enemy and boss health, speed and damage values.
- [ ] Gameplay colour palette: the menus now have one ([[Art Direction]]); should the ships and hazards be bound to it too?
- [ ] Sound effects list.

Back to [[00 GDD Home]]

## Account management (answered 2026-09-25)

Answered by moving to Unity Player Accounts. The decision now lives in [[Scoring]] and
[[UI Flow and Screens]]; kept here only so the trail is followed.

- Signing out, and changing who is signed in, are on the account screen behind Settings. Signing out
  clears the remembered session too, so a machine handed on does not sign the first player back in.
- "Play without an account" is no longer final: the account screen signs a player in later.
- Two people can share a machine, which the pages had been claiming before it was true.
- The snag raised here was real and is what the answer had to solve: Settings is reachable from a pause
  in the level scene, where there is no account screen. The row hides itself rather than the screen being
  duplicated, which `MenuRouter.Has` decides.

The pilot name is no longer the account. A Unity account is identified by an email address, so the name
the boards show is a separate handle the player picks, and the email never leaves the account screen.
