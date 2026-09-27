---
tags:
  - gdd
  - art
status: draft
updated: 2026-09-27
---

# Asset List

%% Every art asset the game needs. Unity location should be under Assets/_Game. %%

| Asset | Type | Unity location | Source | Status |
| ----- | ---- | -------------- | ------ | ------ |
| Player ship | Sprite | `Sprites/Ships/PlayerShip.png` | AI (Gemini 3.1 Flash) | Prototype |
| Dart | Sprite | `Sprites/Ships/Dart.png` | AI (Gemini 3.1 Flash) | Prototype |
| Weaver | Sprite | `Sprites/Ships/Weaver.png` | AI (Gemini 3.1 Flash) | Prototype |
| Splitting meteor (all sizes) | Sprite | `Sprites/Hazards/MeteorSplitting.png` | AI (Gemini 3.1 Flash) | Prototype |
| Solid meteor | Sprite | `Sprites/Hazards/MeteorSolid.png` | AI (Gemini 3.1 Flash) | Prototype |
| Health, weapon, shield, extra-life pickups | Sprites | `Sprites/Pickups/` | AI (Game UI Essentials 2.0) | Prototype |
| Nebula background (level 1) | Sprite, 2048x1152, unlit, darkening baked in | `Textures/Backgrounds/NebulaBackground.png` | AI (Gemini 3.1 Flash, AI upscaled) | Prototype |
| Engine exhaust | Particle systems in ship prefabs, `Materials/EngineExhaust.mat` | `Prefabs/` | Procedural | Prototype |
| Player and enemy bullets, shield bubble, star, UI white | Sprites | `Sprites/Procedural/` | Procedural (script) | Placeholder |
| Starfield | Particle system in the scene, `Materials/Starfield.mat` | `Scenes/Prototype.unity` | Procedural | Prototype |
| Explosions, hit flashes | Effects | | | Not started |
| Swarm drone, Gunship, Diver, Mine Layer, Frigate, Sniper | Sprites | `Sprites/Ships/` | AI (Gemini 3.1 Flash) | Prototype |
| Proximity mine | Sprite | `Sprites/Hazards/Mine.png` | AI (Gemini 3.1 Flash) | Prototype |
| Beams and warning lines (Sniper and three bosses) | Three additive layers driven by `BeamView`: a white hot shaft, a wide soft glow, and a flare covering the muzzle. `BeamShaft.png`, `BeamGlow.png`, `BeamFlare.png` on `Materials/Beam.mat` | `Prefabs/Sniper.prefab`, `Sunforge`, `Overmind`, `Tempest` | Procedural | Built |
| Rock Crusher, Sunforge, Frost Lancer, Dreadnought, Tempest, Singularity Engine, The Overmind | Sprites | `Resources/Bosses/` (outside the atlas, loaded by name) | AI (Gemini 3.1 Flash) | Prototype |
| Backdrops for levels 2 to 8 | Sprites, 2048x1152, unlit, darkening baked in | `Textures/Backgrounds/` | AI (Gemini 3.1 Flash) | Prototype |
| Game icon | Eight square textures, 1024 down to 16, for Windows; a 432 background and foreground pair for Android's adaptive icon | `Icons/` | Ship AI (Gemini 3.1 Flash), cut out and composited over a procedural violet gradient | Built |
| Logo | Sprite, 1252x424, transparent, outside the atlas so it compresses itself (sides divisible by 4). The wordmark plus the tagline as one lockup; cropped on visible alpha, with the glow finishing inside the canvas rather than at its edge | `Sprites/UI/Logo.png` | Typographic (Segoe UI Black Italic, Bahnschrift), composited procedurally | Built |

All paths are under `Assets/_Game/`.

### Target world sizes
Pixels per unit is set per sprite so each keeps its intended size on the 10-unit-tall playfield:

| Sprite | World size (units) |
| ------ | ------------------ |
| Player ship | 1.1 wide |
| Dart | 0.85 wide |
| Weaver | 0.75 wide |
| Swarm drone | 0.6 wide |
| Gunship | 1.7 wide |
| Diver | 0.95 wide |
| Mine Layer | 1.1 wide |
| Frigate | 2.0 wide |
| Sniper | 1.0 wide |
| Proximity mine | 0.45 across |
| Bosses | 3.6 to 5.2 wide, growing with the level |
| Meteors | 1.0 across at scale 1 (scaled per size by the meteor data) |
| Pickups | 0.55 across |
| Background | 23.5 x 13.2 (covers 4:3 to 21:9) |

Back to [[00 GDD Home]]
