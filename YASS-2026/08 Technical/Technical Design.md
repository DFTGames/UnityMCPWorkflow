---
tags:
  - gdd
  - technical
status: review
updated: 2026-09-20
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
- Deviation: per-hazard health and fire timers, the boss brain and the wave director live in the Unity layer rather than `GameSession`. Move them into the session before co-op. Boss positions (hold point, spawn point) come from each client's playfield and would need a fixed logical playfield for co-op.
- Snapshot/restore of rules state (needed for Fusion prediction) is deferred until co-op is scheduled; state is kept in a few small classes so it can be added then.
- Leaderboards sit behind an interface so the UGS implementation can be swapped or mocked in tests.

## Scenes
`Title` (build index 0) and one scene per level, starting with `Level01` (index 1). The title scene holds the Title, Difficulty, Settings and Credits screens; each level scene holds its own Pause, Settings, Game Over and Sector Clear screens, on a canvas drawn above the HUD.

- **Menus are built by editor scripts**, not by hand: `Tools/YASS/Build Title Scene` and `Tools/YASS/Build Level Menus` (`Assets/_Game/Scripts/Editor/`). Re-running them rebuilds the screens after a layout or palette change, and keeps every screen consistent. Hand edits inside those objects are overwritten.
- **What crosses a scene change** lives in `GameFlow` (static): the chosen difficulty and the settings service. A level started from the menus uses the chosen difficulty; opened directly in the Editor it falls back to its own serialised field, so a scene can still be played on its own.
- **Which screen is showing** is `MenuStack` (engine-free, in `YASS.Game.Core`): a stack, because Settings is reachable from both the Title and Pause and must return where it came from. It also decides when the game is stopped, and the `MenuRouter` applies that as `Time.timeScale`, restoring it when the scene is left so a paused level cannot freeze the next one.
- **Accepted deviation:** `MenuStack`'s mutators are public rather than internal (the router lives in another assembly), as with `WaveDirector.SkipToBoss`. Menu state is presentation, not rules, so it does not go through `GameSession`.
- **Settings** are held by `SettingsService` (engine-free) over an `ISettingsStore`; the Unity implementation is PlayerPrefs, which works on every target including WebGL. Tests use an in-memory store, so they never touch the player's own saved settings.

## Data
ScriptableObjects for enemies, waves, levels, pickups and difficulty settings.

## Performance targets
_TBD (mobile and WebGL are the constraints)._

Back to [[00 GDD Home]]
