---
tags:
  - gdd
  - production
status: draft
updated: 2026-09-20
---

# Open Questions

%% Undecided design questions. Once answered, move the decision into the relevant page and log it in the Changelog. %%

- [ ] Health pickup: confirm the provisional 25 health.
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
- [ ] Enemy and boss health, speed and damage values.
- [ ] Gameplay colour palette: the menus now have one ([[Art Direction]]); should the ships and hazards be bound to it too?
- [ ] Sound effects list.

Back to [[00 GDD Home]]
