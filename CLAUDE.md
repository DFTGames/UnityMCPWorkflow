# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A Unity 6 (editor `6000.6.0f1`, see `ProjectSettings/ProjectVersion.txt`) 2D project created from the Universal 2D template, used as a tutorial for driving Unity from Claude Code over MCP. Product name is "YASS 2026". Claude writes all game code and creates all assets (see "Development workflow"). `Assets/Welcome/` and `Assets/TextMesh Pro/` are template/package content, not project code.

## Project folder structure (required)

Never put our own code or assets directly in the `Assets/` root or in the template folders. Everything we create lives under `Assets/_Game/`, organised by type, for example:

```
Assets/_Game/
  Animations/
  Audio/
  Fonts/
  Materials/
  Prefabs/
  Resources/
  Scenes/
  ScriptableObjects/
  Scripts/            (YASS.Game assembly)
    Core/             (YASS.Game.Core assembly: engine-free rules)
    Editor/           (YASS.Game.Editor assembly)
  Shaders/
  Sprites/
  Textures/
  Tests/
    EditMode/         (YASS.Game.Tests.EditMode assembly)
    PlayMode/         (YASS.Game.Tests.PlayMode assembly)
```

- Add further subfolders (by type, then by feature inside them) as needed, and keep the structure consistent as the project grows.
- Empty folders hold a `.gitkeep` so they survive in git (Unity would otherwise delete the orphaned folder `.meta`). Remove it once the folder has real content.
- `Resources/` is special in Unity (its contents are always included in builds and loadable with `Resources.Load`), so only put assets there that are genuinely loaded by path.
- `Assets/Settings/`, `Assets/Welcome/`, `Assets/TextMesh Pro/` and the template's `Assets/Scenes/SampleScene.unity` came from the template or packages; they are not part of `_Game`.
- To move existing assets, use the Editor or MCP so their `.meta` files and GUID references stay intact, rather than moving files on disk.

## Game Design Document (Obsidian vault)

`YASS-2026/` is an Obsidian vault holding the **GDD, the authoritative source for the game's design**. Claude creates and maintains it.

- Entry point is `00 GDD Home.md`, which links every page. Sections are numbered folders (`01 Vision` to `09 Production`); new pages go in the matching folder and must be linked from `00 GDD Home.md` or their section page.
- Read the relevant GDD pages before implementing a feature. If the request conflicts with the GDD, or the GDD is silent, raise it rather than guessing, and update the GDD once the decision is made.
- After a design decision or a change that affects design (mechanics, tuning values, controls, content), update the affected pages, bump their `updated` property, and add an entry to `09 Production/Changelog.md` (newest first).
- Every page has frontmatter `tags` (`gdd` plus its section), `status` (`draft`, `review`, `approved`, `deprecated`) and `updated` (YYYY-MM-DD). Confirm with the user before building on content that is not yet `approved`.
- Use `[[wikilinks]]` between pages, `%% ... %%` for authoring notes, `_Templates/Feature Template.md` for new feature pages, and `_Attachments/` for images.
- Unresolved questions go in `09 Production/Open Questions.md`.
- Only the vault's shared `.obsidian` settings are tracked (see `.gitignore`).

## Working with the Unity Editor (MCP)

- `.mcp.json` registers the `unity-mcp` server, which runs the Unity AI Assistant relay (`%USERPROFILE%\.unity\relay\relay_win.exe --mcp`). It comes from the `com.unity.ai.assistant` package and only works while the Editor has this project open.
- Prefer the `mcp__unity-mcp__Unity_*` tools for anything the Editor owns: creating/editing scenes, GameObjects, assets, materials, packages, and reading the console. Hand-editing `.unity`, `.prefab`, `.asset` YAML or `.meta` files risks broken GUIDs and references.
- After creating or changing C# scripts, check for compile errors with `Unity_GetConsoleLogs` / `Unity_ReadConsole` (or `Unity_ValidateScript`) before reporting the work as done.
- Every asset under `Assets/` needs its `.meta` file; when creating files on disk directly, let Unity generate the `.meta` (refresh via MCP) and keep both in version control.

## Development workflow (required)

Claude creates all of the game's code and assets. Every change to code follows this loop:

1. **Check the GDD** for the feature being built (see above).
2. **Implement with tests.** All code gets unit tests wherever it is technically possible. Structure code to make that possible: keep game rules and state in plain C# classes (no `MonoBehaviour` dependency) that `MonoBehaviour`s delegate to, and inject dependencies such as time, randomness and input rather than reading them statically. If something genuinely cannot be unit tested (for example pure scene wiring), say so and cover it with a PlayMode test where practical.
3. **Run the tests** and confirm they pass, along with a clean console (no compile errors).
4. **Run two independent code review agents** in parallel with the Agent tool, each given the changed files and the relevant GDD pages (not Claude's own conclusions):
   - **Correctness reviewer:** bugs, edge cases, logic errors, mismatches with the GDD, gaps in test coverage.
   - **Unity and quality reviewer:** Unity-specific pitfalls (lifecycle order, serialisation, null/destroyed object checks, allocations in `Update`, physics in `FixedUpdate`, Input System usage), performance, readability, and adherence to this file's conventions.
5. **Verify and fix.** Check each finding against the code before acting on it, fix the valid ones (adding tests for any bug found), re-run the tests, and report which findings were fixed and which were rejected, with the reason.

### Code and test layout

- Game rules: `Assets/_Game/Scripts/Core/` in the `YASS.Game.Core` assembly, namespace `YASS.Core`. It has `noEngineReferences: true`, so it cannot touch `UnityEngine` (use `System.Numerics.Vector2` and `MathF`; convert at the adapter edge). Rules-state mutators are `internal`; presentation changes state only through `GameSession` (`Tick` plus `Report*` methods), called from one fixed-step loop (`FixedUpdate`). `InternalsVisibleTo` exposes internals to the test assemblies.
- Runtime code: `Assets/_Game/Scripts/` in the `YASS.Game` assembly (`YASS.Game.asmdef`, references `Unity.InputSystem` and `YASS.Game.Core`), namespace `YASS` (sub-namespaces by feature). Editor-only code goes in `Assets/_Game/Scripts/Editor/` in `YASS.Game.Editor`.
- Tests: `Assets/_Game/Tests/EditMode/` (`YASS.Game.Tests.EditMode`) and `Assets/_Game/Tests/PlayMode/` (`YASS.Game.Tests.PlayMode`), both test assemblies referencing `YASS.Game` and `YASS.Game.Core`. Code must live in an asmdef for tests to reference it; nothing of ours goes in `Assembly-CSharp`.
- Prefer EditMode tests (fast); use PlayMode tests only for behaviour that needs the player loop, physics or scenes.

## Build and test

There is no CLI build script. Builds and tests run through the Editor (via MCP or manually), using `com.unity.test-framework` (NUnit). With the Editor open, run tests through MCP. This works (verified): first `Unity_RunCommand` with `AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport)` and confirm a clean console, then a `Unity_RunCommand` that creates a `TestRunnerApi` (`ScriptableObject.CreateInstance<TestRunnerApi>()`), registers an `ICallbacks` collector, and calls `Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "YASS.Game.Tests.EditMode" } }) { runSynchronously = true })`. Log `RunFinished`'s pass/fail counts and each failed leaf test's `FullName` and `Message`. `runSynchronously` only works for EditMode; PlayMode tests run asynchronously, so write results to a file from the callbacks and read it afterwards.

If running tests headless, the Editor must **not** already have the project open:

```
"D:\UnityInstalls\6000.6.0f1\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml
```

Use `-testPlatform PlayMode` for play mode tests and `-testFilter <FullyQualifiedName>` to run a single test.

## Project configuration worth knowing

- Render pipeline: URP 2D (`Assets/Settings/UniversalRP.asset` with `Renderer2D.asset`). New materials and shaders must be URP/2D compatible, not Built-in.
- Input: the **new Input System only** (`activeInputHandler: 1`). Use `UnityEngine.InputSystem`, not `UnityEngine.Input`. Default action map is `Assets/Settings/InputSystem_Actions.inputactions`.
- The only scene in the build is `Assets/Scenes/SampleScene.unity`.
- The generated `*.csproj` / `*.sln` files at the root are regenerated by Unity; do not edit them.

## Repository layout notes

- `.claude/` and `.remember/` are gitignored; `.mcp.json` is intended to be tracked.
