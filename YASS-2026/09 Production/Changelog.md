---
tags:
  - gdd
  - production
status: draft
updated: 2026-09-19
---

# Changelog

%% Newest first. Record significant design decisions and which pages changed. %%

## 2026-09-19
- Prototype code review: late kills after game over score nothing; meteor fragments use their own speed range; vertical meteor drift scales with difficulty; shots spawn in the aim direction; restart moved from gamepad Start (reserved for Pause) to the south button. Recorded the prototype's deviation from the "all rules state in GameSession" principle. Pages updated: Mechanics, Enemies and Hazards, Controls, Technical Design.
- Playable prototype: recorded provisional player, enemy and meteor values, the temporary spawner, and the rule that rammed meteors do not split. Pages updated: Mechanics, Enemies and Hazards, Scope and Milestones, Open Questions.
- Code review of the rules layer: added provisional rules (invulnerable ship passes through enemies; shield-absorbed hits keep the no-damage boss bonus; boss kills extend the chain), clarified that the pity rule forces a single drop and that level clear uses the unrounded health percentage. Technical Design: engine-free rules assembly and fixed-step simulation. Pages updated: Mechanics, Scoring, Items and Pickups, Technical Design, Open Questions.
- Implementation of the rules layer surfaced gaps; provisional answers recorded pending approval: health pickup restores 25; unshielded collision destroys non-boss enemies and damages the player; meteors count as kills. Pages updated: Mechanics, Scoring, Items and Pickups, Open Questions.
- Approved proposed designs for the open questions: difficulty names and tuning (Cadet, Pilot, Ace); 5 weapon levels; shield rules; pickup drop chances, weights and pity rule; gamepad firing (right stick and right trigger); scoring and chain; 8-enemy roster; 8 level settings and bosses; Endless cycles; landscape-only 16:9 with fixed playfield height; synthwave adaptive music. New page: Scoring. Pages updated: Core Loop, Mechanics, Controls, Difficulty and Balancing, Items and Pickups, Enemies and Hazards, Levels, Art Direction, Audio Direction, Open Questions, GDD Home. Game Overview and Progression and Economy approved.
- Recorded the core design from research on the original YASS and the designer's answers: faithful remake; PC, mobile and WebGL; single player first, ready for Photon Fusion co-op; stylised 2D AI-generated art and audio; Campaign (8 levels, 8 bosses) and Endless modes; lives plus health; health, extra life, weapon upgrade and shield pickups; weapon loses one level on death; splitting and solid meteors; twin-stick, mouse and keyboard, and two-stick touch controls; three difficulties affecting enemies, player toughness and score multiplier; UGS leaderboards; free, no monetisation. Pages updated: Game Overview, Core Loop, Mechanics, Controls, Difficulty and Balancing, Progression and Economy, Levels, Enemies and Hazards, Items and Pickups, Art Direction, Audio Direction, Technical Design, Scope and Milestones, Open Questions.
- Created the GDD structure.

Back to [[00 GDD Home]]
