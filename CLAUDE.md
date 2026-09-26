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
  Input/              (Input System actions: YASS.inputactions)
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
  Tests/
    EditMode/         (YASS.Game.Tests.EditMode assembly)
    PlayMode/         (YASS.Game.Tests.PlayMode assembly)
```

- Add further subfolders (by type, then by feature inside them) as needed, and keep the structure consistent as the project grows.
- Empty folders hold a `.gitkeep` so they survive in git (Unity would otherwise delete the orphaned folder `.meta`). Remove it once the folder has real content.
- `Resources/` is special in Unity (its contents are always included in builds and loadable with `Resources.Load`), so only put assets there that are genuinely loaded by path. **The backdrops and Endless skies live there on purpose**: they are the heavy assets, and they are loaded and freed by name as the game needs them (see "Loading only what is being used").
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
- `Unity_RunCommand` quirks (all verified):
  - **The script must be one class named exactly `CommandScript`, implementing `IRunCommand`** (the tool wraps it in the `Unity.AI.Assistant.Agent.Dynamic.Extension.Editor` namespace, so do not declare that namespace or `using` it):
    ```csharp
    internal class CommandScript : IRunCommand
    {
        public void Execute(ExecutionResult result) { Debug.Log("hello"); }
    }
    ```
    Bare statements fail to compile (`CS8805: Program using top-level statements must be an executable`), and **a class with any other name compiles but fails at run time with the useless "UNEXPECTED_ERROR: Execution failed: No logs available"**: the package's macro evaluator only looks for `CommandScript`, so nothing is instantiated. The tell is a 0-byte dll left in `Library/AssistantRunCommand/`. This error is not a broken bridge or a stale Editor; restarting Unity does not help. Nested helper classes inside `CommandScript` are fine (the tool also emits a copy of them beside it).
  - Scripts using `System.IO.File.Delete` or `Object.DestroyImmediate` are rejected ("User interactions are not supported"). Use `result.DestroyObject(go)` and delete files from the shell instead.
  - The script is wrapped in a `Unity.AI...` namespace where `Image` resolves to a namespace: alias it (`using UImage = UnityEngine.UI.Image;`).
  - `GetInstanceID()` is obsolete (compile error); use `GetEntityId()`.
  - `System.Reflection` is a blocked namespace, and `System.Numerics` types cannot be referenced (so internal test seams and `PlayerCommand` are out of reach). To drive the game, simulate devices instead: `InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.W, Key.D))`, and release with an empty `KeyboardState`.
  - Never use `??` or `?.` on Unity objects (`GetComponent<T>() ?? AddComponent<T>()` throws): Unity's fake-null defeats them. Use explicit `== null` checks.
  - Opening a scene while another is dirty would prompt: build new scenes additively (`NewSceneMode.Additive`), save, then `EditorSceneManager.CloseScene(old, true)`.
- AI asset generation (`Unity_AssetGeneration_GenerateAsset`): ask the user once per conversation before the first call (it blocks until done and may use credits). Generated sprites come back as opaque RGB, sometimes with a fake checkerboard painted in, so always follow with `RemoveSpriteBackground`, then trim and resize (PIL) and set pixels per unit to the intended world size. Images come back square (1024x1024) whatever the prompt says; crop for other aspect ratios. Ask prompts for "no engine flames or glow": engines are particles (GDD Art Direction).
- Play mode only advances while the Editor is focused unless `Application.runInBackground = true` (set it at runtime through `Unity_RunCommand`; PlayMode tests set it in their setup). `Unity_Camera_Capture` fails on game cameras; to see the game, render `Camera.main` into a `RenderTexture`, save a PNG under `Temp/` and read it.

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

There is no CLI build script. Builds and tests run through the Editor (via MCP or manually), using `com.unity.test-framework` (NUnit). With the Editor open, run tests through MCP.

**Simplest route (verified; no `Unity_RunCommand` needed):**

1. Compile: a `Unity_RunCommand` that calls `AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport)`. **Only a refresh discovers scripts that were created on disk**; `Unity_ManageAsset` with `action: import` re-imports assets Unity already knows about, so a folder import silently leaves new test files out of the assembly and the run reports the old pass count.
2. `Unity_ReadConsole` to confirm no compile errors.
3. `Unity_ManageMenuItem` with `action: execute` and `menuPath: Tools/YASS/Run EditMode Tests` (or `Run PlayMode Tests`), from `Assets/_Game/Tests/EditMode/TestRunnerMenu.cs`.
4. Read the result. `TestResultsWriter.cs` reports every run twice, from the same text: to `Temp/YASS-TestResults-EditMode.txt` / `-PlayMode.txt`, and to the console (so it is in `Logs/Editor.log` too). A clean run is an Info line `[YASS tests] <mode>` with the counts; **a run with any failure or inconclusive result is logged as an Error** carrying the counts and each failing test's name and message, so a bad run cannot be missed by a console check. Runs are asynchronous: poll the file's timestamp rather than trusting stale contents (EditMode takes a few seconds, PlayMode about 45 s). `Unity_ManageEditor` with `action: GetState` shows whether play mode is still running.

**Save the open scenes before launching a run.** A test run starting while a scene has unsaved changes puts up the modal "Scene(s) Have Been Modified" before it restores the scene setup, and that blocks the main thread: the run never starts, no results file is written, and every MCP call times out. The symptoms point the wrong way (the process reports itself responsive, `Editor.log` stops at start-up, `Temp/` looks emptied), so it reads as a hung Editor rather than one waiting for a click. Listing the Unity process's visible top-level windows is the one-call diagnosis; only a person can dismiss it. Note that ordinary-looking MCP calls dirty a scene, so having saved it two commands ago is not a guarantee.

**The PlayMode touch tests are flaky, and a green run after one change proves nothing.** `TouchMergeTests` and `TouchStickViewTests` have been seen to fail together (seven at once, always the same seven: the keyboard reads zero and the sticks are never held) and then pass again with **no code change at all**. They were twice blamed on an unrelated edit to `MenuScreenView`, on the strength of one green run after reverting it; both times that was a coincidence, and the file was innocent. To tell a flake from a cause here, re-run **without changing anything**.

The likely mechanism, not yet proved: the editor has real input devices, so the fixtures' own keyboard and touchscreen are the second of each in the system, and an action samples whichever device is *current*. If a leftover from an earlier fixture is current, the test drives one keyboard while `PlayerInputReader` reads another. A diagnostic run showed `keyboards=2 touchscreens=2 current=129 mine=129` on a passing run; the thing to capture on a failing one is whether `current` is still the fixture's own device.

**Check the console for compile errors before trusting a run**: when a test assembly fails to compile, the run silently uses the last good assembly and reports the old results as passing. The same trap catches editor scripts: a compile error anywhere in `YASS.Game.Editor` leaves the *previous* assembly loaded, so a builder run through `Unity_RunCommand` reports success while quietly doing nothing. Also note that a `Unity_RunCommand` which refreshes and then calls a builder in the same snippet runs the code from **before** the refresh: refresh in one command, build in the next.

`Unity_RunCommand` can also drive the Test Runner (`TestRunnerApi` with `runSynchronously`, EditMode only; log `RunFinished`'s counts and each failed leaf test's `FullName` and `Message`). See the class-name rule below before using it.

Player Settings has `runInBackground` on. It must stay on: play mode does not advance while the Editor is unfocused, so an automated PlayMode run would otherwise hang forever (the menu item turns it on defensively).

If running tests headless, the Editor must **not** already have the project open:

```
"D:\UnityInstalls\6000.6.0f1\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml
```

Use `-testPlatform PlayMode` for play mode tests and `-testFilter <FullyQualifiedName>` to run a single test.

## Gameplay layer

`Assets/_Game/Scripts/Gameplay/` (namespace `YASS.Gameplay`): `GameRunner` is the composition root and the only caller of `GameSession`. It reads input, moves the ship, ticks the session and the `WaveDirector` in `FixedUpdate`, and spawns projectiles, hazards, the boss and pickups. **A level is data, not a scene.** `Assets/_Game/Scenes/Level.unity` is the only scene the game is played in: every campaign level and an Endless run all play there. A level is a `LevelDefinition` asset (`ScriptableObjects/Levels/`): waves of spawn groups, a backdrop and a boss prefab. The runner resolves which one to play in `Awake` (`ResolveLevel`): the run says how far the campaign has got, `Campaign` says what that step is, and the scene's own `level` field is the fallback so opening the scene in the Editor still plays. The backdrop comes from the definition too, which is why the scene can be one file. Every wave hazard carries its `WaveIndex`; pool release callbacks report it to the director (`NotifyHazardGone`), and meteor fragments join their parent's wave (`NotifyHazardAdded`), which is how the director knows a wave is cleared. Every boss is one `BossView` driven by a `BossDefinition` asset (`ScriptableObjects/Bosses/`): armour colliders on its root absorb shots, a `BossCoreView` child takes damage only while open, and `BossBrain` (Core) runs the fight from a table of attacks, each scoped to a phase (armoured or vulnerable) and to a band of remaining health. Bands are how a boss escalates and how the Overmind's three phases work; there is no per-boss class. A boss does nothing until it has flown in to its station. An attack that could not finish inside its own phase is rejected by `BossDefinition.Validate` rather than silently dropped. Entity views (`PlayerShipView`, `EnemyView`, `MeteorView`, `PickupView`, `Projectile`) report collisions to the runner rather than applying rules themselves. Projectiles, enemies, meteors and pickups are pooled; views reset all state in `Init`/`Launch` and return themselves to their pool in `Despawn`/`Release`. Players are handled by index (`players[]` and `inputs[]` on the runner). The player ship's sprite and engine sit on a `Visual` child that tilts towards the aim while firing (`ShipTilt` in Core); the collider stays level on the root. This is a side-scroller, so firing is limited to a forward arc: `FiringArc` (Core) clamps the aim, `PlayerShip.Tick` fires along the clamped direction and `PlayerShipView` tilts by the same angle. Both read `GameTuning.FiringArcDegrees`; never add a per-prefab copy of that value, or the nose and the shots can drift apart. Every ship prefab has an `Engine` object (under `Visual` on the player ship): a local-space `ParticleSystem` plus `EngineExhaust`, whose throttle the runner drives from the player ship's actual displacement each step (not the stick) (`EngineThrust` in Core holds the rule). Tuning data lives in `PlayerDefinition`, `EnemyDefinition` and `MeteorDefinition` assets under `Assets/_Game/ScriptableObjects/`; `PrototypeDataTests` checks them against the GDD. `GameRunner.SetCommandOverride`, `SpawningEnabled` (pauses only the waves phase), `StartBossFightNow`, `ActivePlayerProjectiles` and `BossView.ArmourHits` are internal test seams for the PlayMode tests; `WaveDirector.SkipToBoss` is public because the runner lives in another assembly. The boss is instantiated inactive in `Awake` so its entrance does not hitch. Sector Clear timing runs on the director's fixed-step clock (`LevelEvent.SectorClear`), not `Time.time`.
## Game feel layer

`Assets/_Game/Scripts/Feedback/` (namespace `YASS.Feedback`, in the `YASS.Game` assembly): gameplay asks for feedback through the `Cue` facade (`Cue.Play`, `Cue.Spawn`, `Cue.Shake`), which forwards to whichever of `AudioDirector`, `EffectSpawner` and `CameraShaker` the scene has, so a scene without them stays quiet instead of throwing. `SoundBank` maps each `Sfx` to a clip and its trim; `PooledEffect` returns itself to its pool when its particle system stops (the prefabs set Stop Action to Callback); `HitFlash` whitens a sprite briefly when it is damaged. The rules (`ScreenShake`, `AudioRules`, `VisualCues.HitFlash`) live in Core and are unit tested.

- The facade is called `Cue`, not `Feedback`: a class named `Feedback` inside namespace `YASS.Feedback` cannot be referenced as `Feedback.Play(...)`, because the name resolves to the namespace.
- **A MonoBehaviour must live in a file named after it.** A second MonoBehaviour sharing another script's file silently serialises with `m_Script: {fileID: 0}` in prefabs and scenes, so the component exists but nothing can reference it.
- **The first `AudioMixer.SetFloat` of a session is thrown away.** Unity's audio system finishes initialising after the first scene's `Awake`/`OnEnable`/`Start`, and restores its snapshot over anything written there, so the saved volumes silently did not apply until the player touched a slider. `MixerVolumes` therefore reads both groups back every frame and writes again whenever they disagree with the settings; it owns those parameters and nothing else may write them. A PlayMode test cannot reproduce the failure (its scene loads once play mode is running, by which point the write sticks), so the test knocks the groups off their level by hand instead.
- Audio goes through `Assets/_Game/Audio/YASS.mixer`: effect voices feed its SFX group and `MusicPlayer` (two sources, crossfading) feeds Music, while `MixerVolumes` writes both Settings sliders to the exposed `MusicVolume`/`SfxVolume` parameters in decibels. A voice carries only its clip's trim, so a volume is never applied twice.
- The sound bank, the particle prefabs and the mixer are **assets, edited directly**. Sound effects are AI-generated into `Assets/_Game/Audio/SFX/`, one file per `Sfx` entry, named after it; music into `Assets/_Game/Audio/Music/`. Adding a sound means generating the file and adding a row to `SoundBank`.
- **Unity has no public API for creating an audio mixer**, its groups or its exposed parameters. `YASS.mixer` already exists, so this only matters if one is ever needed again: it has to be made by hand in the Editor, or with a throwaway script driving the internal `UnityEditor.Audio.AudioMixerController` by reflection.
- A menu screen that is already visible when a scene loads has nothing to trigger a layout pass, so whatever positions the scene was saved with are what the player sees on the first frame. If a screen is ever rebuilt by code, lay its canvas out (`Canvas.ForceUpdateCanvases()`) before saving.
- `Tools/YASS/Capture Screenshot` saves the Game view to `Temp/YASS-Screenshot.png`, which is how an automated session can actually look at the game (Screen Space Overlay UI does not appear in a camera render). Spawn an effect and capture in the same command to catch a short-lived one.

## Touch controls layer

`TouchSticks` (`Scripts/Gameplay/`) reads the glass, `VirtualStick` and `TouchControls` (Core) hold the rules, and `TouchStickView` (`Scripts/UI/`) draws a ring and knob only while a thumb is down. Two floating sticks: the half a thumb lands in decides which one it drives (GDD "Controls", Touch).

- **Enhanced touch, reconciled against presence, never against phases.** Each frame it asks who is on the glass, drives them, and releases any stick whose touch is not among them. Reading `Touchscreen.touches` phases broke the game twice: the device overwrites a finished touch the moment another finger lands, and a thumb that presses and slides inside one input update arrives with phase `Moved`, so that finger never started.
- **A stick is owned by `touch.touchId`, never by `finger.index`.** Enhanced touch hands the freed finger slot straight to the next touch, so a thumb lifted and replanted arrives on the same finger and the stick silently keeps its old centre.
- **The side comes from where the thumb landed; the press point may not.** A touch seen from its first frame is centred where it landed (so a press-and-slide works); one adopted later, after a pause or a rebuild let go of it, is centred where it is now, or the stale gap reads as a push nobody made. A finger ignored because its half was busy stays judged by its landing side, so it cannot grab the other stick by wandering across the middle. A rebuild is the one exception and re-decides both from the current position.
- **The rebuild guard compares `Screen.width`/`Screen.height`, not the radius.** `RadiusFor` uses the shorter side, so a 90 degree rotation leaves the radius identical while every coordinate moves: checking the radius looked right and could never fire on a rotation.
- **Losing the touchscreen lets go.** Leaving the sticks as they were froze the ship at its last deflection and locked out the keyboard and pad behind `InUse`.
- `PlayerInputReader` merges **per channel**: the move half comes from touch only while the move stick is held, the aim half only while the aim stick is held. Taking the whole command whenever `InUse` was true meant a right thumb firing zeroed WASD and a resting palm killed every other device.
- `TouchStickView` writes a `RectTransform`'s position or size **only when it changes**: every write dirties the HUD canvas and re-meshes the graphic. It hides all four parts in `OnDisable`, or they freeze on screen at their last push.
- Test seams: `Touching`/`Begin`/`Drag`/`End` and `RebuildNow` (what a rotation does, which a test cannot ask the screen for). Device-level tests live in `TouchDeviceTests` and need `"testables": ["com.unity.inputsystem"]` in the manifest plus a `Unity.InputSystem.TestFramework` asmdef reference; without both, fed events silently produce no touches at all. See `%USERPROFILE%\.claude\knowledge\unity-input-system-gotchas.md`.

## Endless mode

Endless is a **mode**, played in the same `Level.unity` as the campaign: cycles of ten waves and a boss, each faster, fuller and worth more (GDD "Core Loop", Endless mode). It authors no gameplay content, drawing each cycle from the campaign's levels through `EndlessDefinition` (`ScriptableObjects/Levels/Endless.asset`), but it does have its own ring of skies.

- The rules are `EndlessRun` (Core): the escalation, which levels a cycle may draw from, the wave draw and the boss draw. `EndlessDirector` (Gameplay) turns those choices into the waves, boss and backdrop a cycle needs.
- **The run decides the mode, not the scene.** `RunContext.Mode` is what makes the level scene an Endless run; `modeWhenPlayedDirectly` on the runner covers opening the scene in the Editor without going through the menus. A campaign run that cannot get its level from the campaign **fails loudly** rather than falling back to the scene's own level, or it would quietly replay level 1 for a whole run.
- Beating a boss starts the next cycle in place: no Sector Clear, a new sky, a new boss and raised multipliers. The turnover is deferred to the next `FixedUpdate` (it is reached from a physics callback), and the run's cycle number moves when the new cycle actually begins, not when the old boss dies.
- `DifficultySettings.Escalated` produces the settings hazards are spawned with. **Everything that spawns must use `_spawnSettings`, not `_settings`**: the latter is the difficulty alone, and using it was how the whole speed escalation once went missing while the tests still passed.
- Hazards left over from a finished cycle are disowned (`WaveIndex = -1`) so their pool release cannot tell the new cycle's director that one of its waves is clear.
- The skies, the HUD's cycle label and the sky pair all live in the one scene; the cycle label hides itself when the cycle is 0, which is the campaign.

## Loading only what is being used

One scene plays every level and Endless, so a direct reference from the scene or from a ScriptableObject would put the whole game's asset graph behind it. The heavy assets are therefore held **by path, never by reference**, and given back the moment they stop being used.

- `AssetResidency` (Core) is the arithmetic: given what is wanted, it says what to load and what to free. `ContentCache` (Gameplay) does it against `Resources`, keyed by path, and `Resources.UnloadAsset` frees exactly one named asset.
- `LevelDefinition.BackdropPath` and `EndlessDefinition.SkyPaths` are strings, not `Sprite` references. **Do not turn them back into references**: that is what made the scene's dependency graph the entire game, and nothing would fail visibly if it happened.
- A boss holds its own sprite, taken from `BossDefinition.SpritePath` when it arrives and given back when the level ends or an Endless cycle turns. **The runner keeps a second `ContentCache` for it** (`BossContent`): a cache means "hold exactly this and nothing else", so the sky and the boss sharing one would free each other's art.
- A campaign level holds its own backdrop and nothing else. An Endless run holds **one** sky: the next is fetched in the background shortly before its dissolve (`prefetchSeconds`) and the departing one is freed as that dissolve ends, so two are resident only while both are on screen. `ContentCache.Require` frees before it loads, which keeps peak memory down; the sprite still being drawn is never in the set being freed.
- `GameRunner.OnDestroy` gives everything back, so leaving a level does not carry its sky into the next one.
- Two guards: `EndlessSceneTests.ARunHoldsOnlyTheSkyItIsShowing` drives a full lap and fails if anything accumulates, and `ContentCacheTests` (PlayMode) asserts the engine really unloaded the asset. **Do not test that with Unity's fake null.** After `Resources.UnloadAsset` the managed wrapper is still there (the editor rebuilds the asset from the AssetDatabase if anything touches it), so `texture == null` stays false however completely the memory went; `Resources.FindObjectsOfTypeAll` is what changes. Measured side by side: fake-null said "live", FindObjectsOfTypeAll said "gone".
- Enemy and boss **prefabs** are still ordinary references, and deliberately so: measured, all 73 prefab assets together are 27 KB and their pooled instances another 27 KB. Their sprites are in the shared atlas page, which one enemy on screen keeps loaded, so holding them by name would reclaim nothing. It was the boss **art** that was worth moving, because it was two thirds of that page.

## Flow and menus layer

`Assets/_Game/Scripts/UI/` (namespace `YASS.UI`, in the `YASS.Game` assembly) holds the game flow: `GameFlow` (static) carries the chosen difficulty and the shared `SettingsService` across scenes; `MenuRouter` shows whichever screen `MenuStack` (Core) says is current and owns `Time.timeScale` while a menu covers a level; `MenuScreenView` is one panel plus the button to select when it opens; `TitleMenu`, `DifficultyMenu`, `SettingsMenu`, `PauseMenu` and `ResultsMenu` answer their buttons; `LevelFlow` shows Game Over or Sector Clear once `GameRunner` reports the run has ended; `SettingsApplier` applies saved settings in every scene; `PlayerPrefsSettingsStore` is the `ISettingsStore` implementation.

**Domain reloading is off, so every static holding session state needs a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` reset** (`GameFlow`, `CameraShaker`, `MusicPlayer` and `SettingsApplier` all have one). The dangerous kind is a flag meaning "the saved preference has been applied once this session": without the reset the second play session takes the other branch on its first scene and writes the live state over the saved setting, so the setting is not just ignored but destroyed. `SettingsService.ShouldAdoptTheWindow` holds that rule and is unit tested; it never adopts the window in the editor, where the window is the Editor's rather than the game's.

A campaign run is `CampaignRun` (Core) held by `RunContext`: it banks each cleared level's score and what each player carries (lives, health, weapon), and `GameRunner` restores that when the next level loads. The levels themselves are listed in `Assets/_Game/Resources/Campaign.asset` (a `Campaign`, loaded by path so the flow layer needs no scene reference), which holds the `LevelDefinition` assets in play order: **add a level by making its definition and dropping it in that list**. No new scene, no build-settings entry, no builder run. `CampaignProgress` saves how far each difficulty has got through the same key-value store as the settings.

## Leaderboards

Six Unity Gaming Services boards, one per mode and difficulty (GDD "Scoring"). `Leaderboards` (Core) holds the rules that need no network: the board ids (`campaign_cadet` to `endless_ace`, **pinned by a test because renaming one orphans every score filed under it**), the name cleaning, and whether a run is worth sending. `ILeaderboardService` is the seam: `UgsLeaderboards` talks to the service, `LocalLeaderboards` keeps the player's own runs on this machine through the settings store, and that is also what the tests run against. **The interface is callback-based and may never throw**: a failure is a `LeaderboardResult`, because a leaderboard is not allowed to interrupt a game. `GameFlow.SubmitRun` sends a finished run and mirrors it locally; `LevelFlow` calls it at the two points a score is final (game over, and clearing the campaign's last level, never an intermediate Sector Clear).

- **Sign-in is lazy and happens through `GameFlow.WhenReady`.** Nothing called `Prepare` for a while, and the symptom was invisible: `IsReady` stayed false, so every submission failed with "the boards could not be reached" and every board fell back to the local one, which is exactly what having no network looks like. The live test missed it because it signs in itself.
- **`Prepare` queues its callers.** Answering "not ready" to whoever asks while a sign-in is in flight is wrong: opening the board screen starts the sign-in, and the player pressing a button during it must not be told the boards are unreachable. Everyone waiting is answered when it lands, and there is a deadline, because a black-holed connection never refuses, it just never answers.
- **Unity Authentication refuses a player name containing any whitespace**, and that refusal arrives in the middle of submitting a score. `CleanName` therefore strips every space, and the rename has its own `try` and is skipped when the name has not changed, so a refused rename costs the player their new name rather than their run.
- **A label that a network answer fills in must keep its row.** The placing line arrives after the results panel, and deactivating it takes it out of the column's layout, so Retry and Title jump up the screen while the player is reaching for them. Write empty text instead, and reserve the row with a `LayoutElement`.
- Every answer arrives later than the question, so `LeaderboardMenu` carries a request token: cycling the mode twice quickly asks three questions, and without it the first board back wins and the screen shows something other than what its heading says. `OnDisable` cancels.
- **A build plays against `production`; anything in the editor plays against `test`.** The rule is `UgsEnvironment` (Core, unit tested), the single place the services are started is `UgsSession`, and `UgsLeaderboards.Submit`/`Top` refuse and log an error unless the environment can be *shown* to be the right one. An unknown environment is refused, never assumed safe. A test run only ever happens in the editor (batchmode included), so reaching production from a test is no longer a matter of each fixture remembering its fakes, and an editor playtest cannot put a practice score on a real board. Consequence to know: **the boards you see in the editor are the test ones.**
- **`UnityServices.InitializeAsync(options)`, never `InitializeAsync()`.** Building an `InitializationOptions`, calling `SetEnvironmentName` on it and then not passing it is silent: the services take the default environment, which is production. That is exactly how the live boards ended up holding test scores while the fixture's own remarks said it never touched production. There is no public way to read the active environment back (it lives on an internal type), so `UgsSession` records what it started them with and everything else asks it.
- The UGS project is **`4279fc32-7e38-465f-b493-66a8aff2da78`** (`cloudProjectId` in `ProjectSettings`), with a `production` and a `test` environment holding the same six boards. **The UGS CLI's own default is a different project**, so every command has to name this one: `ugs leaderboards list -p 4279fc32-7e38-465f-b493-66a8aff2da78 -e test`. Use `-p` rather than `ugs config set project-id`, which would change the default for the other project too. The CLI cannot read scores at all (configs only, plus a `reset` that wipes a board), so looking at what is on a board means the dashboard.
- Note for anyone touching the boards: **UGS CLI 1.6.0 silently ignores `SortOrder`**, so the six were created ascending and had to be set to descending in the dashboard. Production is `KeepBest`; the `test` environment is `KeepLatest`, so a test run overwrites its own previous score instead of accumulating.
- `GameFlow.UseLeaderboards` and `UseLocalBoards` are the test seams. **Both are needed**: a finished run is mirrored locally whatever the network did, so without the second the suite files its own runs in the saved scores of whoever runs it.

**The two scenes are ordinary, hand-edited assets.** They were generated by builders once; those builders are gone, and the scenes are now the source of truth, edited in the Editor (or over MCP) like any Unity project. `SceneAudioTests` fails if either scene loses its audio, its mixer reference or its `SettingsApplier`, because a silent or unconfigured scene looks exactly like a working one.

- **Nothing is generated from tables any more: every asset is edited directly.** Enemy and boss prefabs, their definitions, the levels, the scenes, the sound bank and the mixer are all plain assets, changed in the Inspector (or over MCP) like any Unity project. `Prefabs/_BossTemplate.prefab` is kept as a starting point to duplicate by hand for a ninth boss, not as a builder input.
- **Do not add a builder.** A script that regenerates assets makes hand edits disappear on its next run, which cost this project real work more than once. If MCP cannot do something directly, write a throwaway script, run it, and delete it in the same change; the tests are what keep the assets honest (`PrototypeDataTests`, `BossDataTests`, `EnemyPrefabTests`, `CampaignLevelTests` all check them against the GDD).
- Known deviation, accepted for now: per-hazard state (enemy and meteor `Health`, enemy `FireTimer`s, the boss's `HiveCarrierBrain`) and the `WaveDirector` are owned by the Unity layer, so their mutators are public. Move them behind `GameSession` when online co-op is scheduled.

## Project configuration worth knowing

- Render pipeline: URP 2D (`Assets/Settings/UniversalRP.asset` with `Renderer2D.asset`). New materials and shaders must be URP/2D compatible, not Built-in.
- Input: the **new Input System only** (`activeInputHandler: 1`). Use `UnityEngine.InputSystem`, not `UnityEngine.Input`. Default action map is `Assets/Settings/InputSystem_Actions.inputactions`.
- Build scenes: `Assets/_Game/Scenes/Title.unity` (index 0) and `Assets/_Game/Scenes/Level.unity` (index 1). That is the whole game: the template's `SampleScene` is no longer in the build.
- Sprites: gameplay sprites live in `Sprites/Ships`, `Sprites/Hazards`, `Sprites/Pickups`, `Sprites/Bosses` (the shared core only) and `Sprites/Procedural`, all packed into `Sprites/Gameplay.spriteatlasv2`. **The eight boss bodies are deliberately not in it**: they were two thirds of its content, only one boss is ever in play, and an atlas page is all-or-nothing. They live in `Resources/Bosses/` and are loaded by name, which shrank the page from 2048x2048 (10.71 MB) to 1024x1024 (2.71 MB). Backdrops and skies are **not** in the atlas: they are in `Resources/Backgrounds/` and `Resources/Skies/`, loaded by path. Keep them out of the atlas, or freeing one would destroy the shared atlas page (`ContentCache` refuses and says so). Keep the **atlas** sources uncompressed with dimensions divisible by 4 (the atlas compresses them). The boss bodies are not atlassed, so they compress themselves: Compressed HQ (BC7), mipmaps off, and still divisible by 4 so the blocks are clean. Sorting orders are listed in the GDD's Art Direction page.
- `GeneratedAssets/` at the repository root is Unity AI's generation cache and is gitignored.
- Physics layers: 6 PlayerShip, 7 PlayerProjectile, 8 Hazard (enemies, meteors and the boss, including its core), 9 EnemyProjectile, 10 Pickup. The 2D collision matrix only allows PlayerShip with Hazard, EnemyProjectile and Pickup, and PlayerProjectile with Hazard. All gameplay bodies are kinematic, interpolated `Rigidbody2D`s with trigger colliders (kinematic-vs-kinematic triggers work without `useFullKinematicContacts`, verified by the PlayMode tests).
- The generated `*.csproj` / `*.sln` files at the root are regenerated by Unity; do not edit them.

## Repository layout notes

- `.claude/` and `.remember/` are gitignored; `.mcp.json` is intended to be tracked.
