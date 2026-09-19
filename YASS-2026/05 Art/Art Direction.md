---
tags:
  - gdd
  - art
status: review
updated: 2026-09-19
---

# Art Direction

%% Put reference images in _Attachments. %%

## Visual style
**Stylised 2D:** clean vector or cel-shaded look, bold colours, using URP 2D lighting for glows, explosions and engine trails. It modernises the 2010 look rather than copying its pre-rendered 3D sprites.

## Engines
- Every ship (player and enemies) has its engine exhaust as a **particle effect**, never painted into the sprite. Ship art must have no engine flames or glow.
- Exhaust particles simulate in the ship's **local space**, so the flame stays attached to the ship without trailing artefacts.
- The player's exhaust is **dynamic**: idle at rest and stronger (more, faster, larger particles) the faster the ship is actually moving, easing smoothly between the two. A ship pushed against a screen edge is not moving, so its engine idles. Enemy exhaust holds a steady level.
- Engines draw behind every gameplay sprite, so exhaust never covers bullets.
- Colours: player orange-yellow; Dart orange-red; Weaver pink-violet.

## Carried over from the original
- Painted nebula backgrounds in magenta, purple and orange, with suns and star fields.
- Orange tracer bullets and big fiery explosions.
- Per-player HUD: heart lives, health bar, kill count, player name.

## Level settings
Each of the 8 levels has its own backdrop and palette; see [[Levels]].

## Production
Art and audio are new, created with AI generation tools (Unity AI asset generation), then curated for consistency.
- Ships, meteors and the background: Gemini 3.1 Flash (Nano Banana 2). Pickup icons: Game UI Essentials 2.0. Backgrounds removed with Unity's sprite background removal, then trimmed and resized.
- Bullets, the shield bubble, starfield particles and UI fills stay procedural (simple shapes and effects AI does poorly).
- See [[Asset List]] for every asset and its source.

## Screen and orientation
- **Landscape only**, designed at a **16:9** reference.
- The playfield has a **fixed visible height**; the horizontal view widens or narrows with the aspect ratio (supported from 4:3 to 21:9, including 19.5:9 phones).
- The HUD is anchored inside the device safe area (notches, rounded corners).

## Rendering conventions
- Sorting order (default layer): background -20, starfield -10, engine exhaust -2, pickups 0, enemies and meteors 1, player shots 2, enemy shots 3, player ship 5, shield 6.
- Gameplay sprites (ships, hazards, pickups, procedural shapes) are packed into one sprite atlas, `Sprites/Gameplay.spriteatlasv2`, compressed as a whole; source sprites stay uncompressed with sizes divisible by 4.
- Backgrounds render unlit; any darkening is baked into the texture.

## Colour palette
_To be defined._

Back to [[00 GDD Home]]
