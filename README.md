<div align="center">

<img src="Store/itch-cover-630x500.png" alt="YASS 2026" width="630">

**Yet Another Space Shooter.** Fast, hard, and unapologetically arcade.

[**▶ Play it in your browser on itch.io**](https://dftgames.itch.io/yass-2026)

</div>

---

This repository is two things at once.

It is a **complete arcade shoot-em-up** for browser, PC and mobile: eight sectors, eight bosses, an endless mode and online leaderboards.

It is also a **tutorial on building software with an agentic AI workflow**. Every line of game code, every test, every sprite and every sound in this project was produced by an AI agent ([Claude Code](https://claude.com/claude-code)) driving the Unity Editor directly over MCP, with a human directing, reviewing and deciding. The whole history is here, including the mistakes and the reasoning.

If you came for the game, play it on [itch.io](https://dftgames.itch.io/yass-2026). If you came for the workflow, start with [`CLAUDE.md`](CLAUDE.md).

---

## The game

A 2026 remake of *YASS* (DFT Games Studios, Xbox 360, 2010), a side-scrolling shoot-em-up from the Xbox Live Indie Games era. Your ship holds the left of the screen. Everything else comes at you from the right.

### Eight sectors, eight bosses

Magenta Nebula, Asteroid Belt, Solar Corona, Ice Rings, Derelict Fleet, Ion Storm, Black Hole's Edge, and finally the Enemy Homeworld. Each ends with a boss that has armour you have to break and a core that only opens for a few seconds at a time. Learn the pattern or die learning it.

### Chase the score

- **Chain your kills.** Every kill within 1.5 seconds of the last one raises the multiplier, up to ×3. Take a hit and it is gone.
- **Three difficulties:** Cadet, Pilot and Ace, each with its own score multiplier and its own leaderboard.
- **Endless mode.** Cycles of ten waves and a boss, drawn from the whole campaign. Every cycle is faster, fuller and worth more. There are no breaks and no ending: you stop when you run out of lives.
- **Six leaderboards**, one per mode and difficulty, on Unity Gaming Services. Sign in with your itch.io account in the browser and your runs follow you between devices, or play without one and keep your scores locally.

### Play it your way

Gamepad, mouse and keyboard, or touch. On a phone there are two floating sticks that appear wherever your thumbs land: left to fly, right to aim and fire.

---

## How it is built

The interesting part is the discipline around the agent, not the code generation.

### A design document is the source of truth

[`YASS-2026/`](YASS-2026) is an Obsidian vault holding the Game Design Document. The agent must read the relevant pages before building a feature, raise a conflict rather than guess, and update the affected pages when a decision changes. Every design decision lands in [`09 Production/Changelog.md`](YASS-2026/09%20Production/Changelog.md) with the reasoning behind it, which is worth reading on its own.

### Everything that can be tested is tested

Game rules live in plain C# classes with no engine dependency at all: the `YASS.Game.Core` assembly sets `noEngineReferences`, so it cannot so much as reference `UnityEngine`. `MonoBehaviour`s delegate to it, and time, randomness and input are injected rather than read statically. That is what makes **974 tests** (821 EditMode, 153 PlayMode) possible, and they run on every change.

### Every change is reviewed by two independent agents

One for correctness, one for engine-specific pitfalls, each given the changed files and the relevant design pages rather than the author's own conclusions. Every finding is checked against the code before it is acted on, and rejections are reported with reasons. This has caught real bugs that looked perfect in a screenshot.

### The agent keeps its own notes

[`CLAUDE.md`](CLAUDE.md) is the operating manual the agent reads at the start of every session. It is the most genuinely useful artefact in this repository: an accumulating record of hard-won, verified facts about the engine and the tooling, each one written down the moment it cost something to learn. A representative sample:

- A clean console is not proof a test run used your code, and the tell is the pass *count*.
- A Unity 6 build profile carries its own copy of the player settings, so `PlayerSettings` writes can silently land somewhere that is never built.
- Unity Authentication refuses a player name containing whitespace, and it refuses it in the middle of submitting a score.
- `window.open` needs the transient activation of the click, so an awaited call before it means the popup is blocked every time.

### No generators

Assets are edited directly, never regenerated from tables. A script that rebuilds assets destroys hand edits on its next run, which cost this project real work more than once. The tests are what keep the assets honest instead.

---

## Repository layout

```
Assets/_Game/              everything the project owns
  Scripts/Core/            engine-free game rules (System.Numerics, no UnityEngine)
  Scripts/                 MonoBehaviours, UI, feedback, touch, adapters
  Scenes/                  Title.unity and Level.unity; that is the whole game
  ScriptableObjects/       levels, bosses, enemies as data
  Tests/EditMode/          821 tests
  Tests/PlayMode/          153 tests
YASS-2026/                 the Game Design Document (Obsidian vault)
CloudCode/                 Unity Cloud Code: verifies itch.io sign-in server side
docs/                      the OAuth callback page, served by GitHub Pages
Store/                     cover art and sources for the itch.io page
CLAUDE.md                  the agent's operating manual
```

**A level is data, not a scene.** Every campaign level and an entire Endless run all play in the same `Level.unity`; a level is a `LevelDefinition` asset. Adding a level means making its definition and dropping it in the campaign list. No new scene, no build-settings entry.

---

## Running it yourself

You need **Unity 6000.6.0f1** (see [`ProjectSettings/ProjectVersion.txt`](ProjectSettings/ProjectVersion.txt)).

1. Clone the repository. It uses **Git LFS** for art and audio, so make sure LFS is installed before cloning.
2. Open the project in Unity.
3. Open `Assets/_Game/Scenes/Title.unity` and press Play.

### Tests

From the Editor, use `Tools/YASS/Run EditMode Tests` or `Tools/YASS/Run PlayMode Tests`; results are written to `Temp/YASS-TestResults-*.txt` and to the console. Or headless, with the Editor closed:

```bash
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml
```

### Driving Unity from Claude Code

[`.mcp.json`](.mcp.json) registers the `unity-mcp` server, which comes from the `com.unity.ai.assistant` package and only works while the Editor has this project open. With it running, the agent can create scenes and assets, edit GameObjects, run tests and read the console without a human touching the Editor.

---

## Credits

Built by [DFT Games Studios](https://dftgames.com) with [Claude Code](https://claude.com/claude-code).

Based on [the original *YASS*](https://dftgames.com/yass/), released 22 April 2010 on Xbox Live Indie Games. No original source or assets survived; this remake was rebuilt from videos and memory.

Art and audio are generated with Unity AI asset generation and then curated for consistency; see [`05 Art/Asset List.md`](YASS-2026/05%20Art/Asset%20List.md) for the source of every asset.

The game is **free to play and non-commercial**. No ads, no purchases, nothing to buy.

---

## Licence

Licensed under the [Apache License, Version 2.0](LICENSE).

You may use, modify and redistribute this project, including commercially, **provided the attribution travels with it**. Section 4(d) requires that anyone distributing this work or a derivative of it includes the contents of [`NOTICE`](NOTICE), which credits DFT Games Studios as the author of *YASS*. That obligation is the point of choosing this licence rather than a bare permissive one: credit is a condition, not a courtesy.

The licence covers the code and the assets in this repository. It does **not** grant any right to the *YASS* name or branding, which Apache 2.0 explicitly withholds (section 6, Trademarks). Build on this freely, but release the result under a name of your own.
