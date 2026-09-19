---
tags:
  - gdd
  - art
status: draft
updated: 2026-09-19
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
| Bosses, remaining enemies, level backgrounds 2 to 8 | Sprites | | | Not started |

All paths are under `Assets/_Game/`.

### Target world sizes
Pixels per unit is set per sprite so each keeps its intended size on the 10-unit-tall playfield:

| Sprite | World size (units) |
| ------ | ------------------ |
| Player ship | 1.1 wide |
| Dart | 0.85 wide |
| Weaver | 0.75 wide |
| Meteors | 1.0 across at scale 1 (scaled per size by the meteor data) |
| Pickups | 0.55 across |
| Background | 23.5 x 13.2 (covers 4:3 to 21:9) |

Back to [[00 GDD Home]]
