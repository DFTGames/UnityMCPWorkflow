---
tags:
  - gdd
  - technical
status: review
updated: 2026-09-19
---

# Technical Design

## Engine and packages
- Unity 6 (6000.6.0f1), URP with the 2D renderer
- Input System (new) only: gamepad, mouse and keyboard, and touch (on-screen sticks)
- Unity Gaming Services: Authentication and Leaderboards (cross-platform, including WebGL)
- Future: Photon Fusion for online co-op (up to 4 players)
- See the repository `CLAUDE.md` for project conventions, including the `Assets/_Game` folder structure and testing rules

## Target platforms
Windows (Steam), iOS, Android, WebGL.

## Architecture
Principles:
- Game rules and state in plain, testable C# classes; MonoBehaviours only adapt them to Unity.
- **Multiplayer ready:** player input is gathered into per-tick input structs and applied by the simulation, rather than read directly by gameplay code; players are identified by an index or id, never assumed to be a singleton; game state is kept in data that could later become networked state. This keeps a later move to Photon Fusion practical.
- Rules live in the engine-free `YASS.Game.Core` assembly. `GameSession` is the single entry point: `Tick(dt, commands, shots)` plus `Report*` event methods, all called from one fixed-step loop (`FixedUpdate`, later Fusion's `FixedUpdateNetwork`) so timing does not depend on frame rate.
- Prototype deviation: per-hazard health and fire timers, and the temporary spawner, live in the Unity layer rather than `GameSession`. Move them into the session before co-op.
- Snapshot/restore of rules state (needed for Fusion prediction) is deferred until co-op is scheduled; state is kept in a few small classes so it can be added then.
- Leaderboards sit behind an interface so the UGS implementation can be swapped or mocked in tests.

## Scenes
_TBD_

## Data
ScriptableObjects for enemies, waves, levels, pickups and difficulty settings.

## Performance targets
_TBD (mobile and WebGL are the constraints)._

Back to [[00 GDD Home]]
