using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YASS.Core;
using YASS.Gameplay;
using YASS.UI;
using Object = UnityEngine.Object;

namespace YASS.Tests.UI
{
    /// <summary>
    /// The game flow as the player meets it (GDD "UI Flow and Screens"): the title menus, difficulty select,
    /// pause and the results screens, in the real scenes.
    /// </summary>
    public class MenuFlowTests
    {
        const string TitleScene = "Title";
        const string LevelScene = "Level";

        bool _previousRunInBackground;
        InputSettings.BackgroundBehavior _previousBackgroundBehavior;
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode _previousEditorInput;
#endif

        /// <summary>An in-memory store, so tests never touch the player's own saved settings.</summary>
        sealed class MemoryStore : ISettingsStore
        {
            readonly Dictionary<string, float> _floats = new Dictionary<string, float>();
            readonly Dictionary<string, bool> _bools = new Dictionary<string, bool>();
            readonly Dictionary<string, string> _strings = new Dictionary<string, string>();

            public float GetFloat(string key, float fallback) => _floats.TryGetValue(key, out var v) ? v : fallback;
            public void SetFloat(string key, float value) => _floats[key] = value;
            public bool GetBool(string key, bool fallback) => _bools.TryGetValue(key, out var v) ? v : fallback;
            public void SetBool(string key, bool value) => _bools[key] = value;
            public string GetString(string key, string fallback) => _strings.TryGetValue(key, out var v) ? v : fallback;
            public void SetString(string key, string value) => _strings[key] = value;
            public void Save() { }
        }

        static MenuRouter Router => Object.FindAnyObjectByType<MenuRouter>();

        [SetUp]
        public void UseTestSettings()
        {
            _previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;

            // Queued input is discarded while the Editor is unfocused, which is how an automated run always
            // works: without this the input-driven tests pass or fail depending on where the mouse happens to be.
            _previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;

#if UNITY_EDITOR
            // Keeping the devices alive is not enough: while the Game view does not have focus the Editor still
            // decides where their input goes, and by default it goes to the Editor rather than to the game. A
            // run started from a menu item never has that focus.
            _previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif

            GameFlow.UseSettings(new SettingsService(new MemoryStore()));
            GameFlow.UseProgress(new CampaignProgress(new MemoryStore()));
            GameFlow.ResetForTests();
        }

        [TearDown]
        public void Restore()
        {
            Time.timeScale = 1f;
            Application.runInBackground = _previousRunInBackground;
            InputSystem.settings.backgroundBehavior = _previousBackgroundBehavior;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = _previousEditorInput;
#endif
            GameFlow.UseSettings(null);
            GameFlow.UseProgress(null);
            GameFlow.ResetForTests();
        }

        static IEnumerator Load(string sceneName)
        {
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            yield return null; // let Awake and the first Update run
        }

        static MenuScreenView FindScreen(MenuScreen screen)
        {
            foreach (var view in Object.FindObjectsByType<MenuScreenView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (view.Screen == screen) return view;

            Assert.Fail($"No {screen} screen in the scene.");
            return null;
        }

        static Button FindButton(string path)
        {
            foreach (var button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (button.name == path) return button;

            Assert.Fail($"No button named {path}.");
            return null;
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string waitingFor)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timed out waiting for {waitingFor}.");
                yield return null;
            }
        }

        // ---- Title scene ----

        [UnityTest]
        public IEnumerator TitleScene_StartsOnTheTitleScreen()
        {
            yield return Load(TitleScene);

            Assert.That(Router, Is.Not.Null, "the title scene has no MenuRouter");
            Assert.That(Router.Current, Is.EqualTo(MenuScreen.Title));
            Assert.That(FindScreen(MenuScreen.Title).IsShown, Is.True);
            Assert.That(FindScreen(MenuScreen.Difficulty).IsShown, Is.False);
            Assert.That(FindScreen(MenuScreen.Settings).IsShown, Is.False);
            Assert.That(FindScreen(MenuScreen.Credits).IsShown, Is.False);
        }

        [UnityTest]
        public IEnumerator TitleScene_SelectsItsFirstButton()
        {
            // Keyboard and gamepad must be able to drive the menu without a click first (GDD Navigation).
            yield return Load(TitleScene);

            Assert.That(EventSystem.current, Is.Not.Null, "no EventSystem in the title scene");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(FindButton("Play").gameObject));
        }

        [UnityTest]
        public IEnumerator TitleScene_LaysOutItsTextOnTheVeryFirstFrame()
        {
            // Text sizes itself to its content a frame after the column places it, so the first frame showed the
            // logo drawn over its tagline; opening another screen and coming back hid it. No extra frame is
            // waited for here on purpose: one frame later the layout has caught up and the bug is invisible.
            yield return SceneManager.LoadSceneAsync(TitleScene, LoadSceneMode.Single);

            AssertNoOverlap("Logo", "Tagline");
        }

        [UnityTest]
        public IEnumerator EveryScreen_LaysOutItsTextOnTheFrameItOpens()
        {
            yield return Load(TitleScene);

            FindButton("Play").onClick.Invoke(); // asserted in the same frame, before any layout pass
            AssertNoOverlap("Heading", "Cadet");
            AssertNoOverlap("CadetDescription", "Pilot");
        }

        /// <summary>
        /// Fails if two named elements overlap vertically, or if a text element is still at its stale one-line
        /// size (which is what makes the text draw over its neighbour even when the rectangles do not touch).
        /// </summary>
        static void AssertNoOverlap(string above, string below)
        {
            var top = FindRect(above);
            var bottom = FindRect(below);

            AssertSizedToItsText(top);
            AssertSizedToItsText(bottom);

            var topCorners = new Vector3[4];
            var bottomCorners = new Vector3[4];
            top.GetWorldCorners(topCorners);
            bottom.GetWorldCorners(bottomCorners);

            // Corner 0 is the bottom-left, corner 1 the top-left.
            Assert.That(topCorners[0].y, Is.GreaterThanOrEqualTo(bottomCorners[1].y - 1f),
                $"'{above}' overlaps '{below}': its bottom is at {topCorners[0].y:F1} " +
                $"while the top of '{below}' is at {bottomCorners[1].y:F1}");
        }

        /// <summary>A text element whose box is shorter than its content spills over whatever follows it.</summary>
        static void AssertSizedToItsText(RectTransform rect)
        {
            var text = rect.GetComponent<TMPro.TMP_Text>();
            if (text == null) return;

            Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(text.preferredHeight - 1f),
                $"'{rect.name}' is {rect.rect.height:F1} high but its text needs {text.preferredHeight:F1}: " +
                "the layout has not caught up with the content");
        }

        static RectTransform FindRect(string name)
        {
            foreach (var rect in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (rect.name == name && rect.gameObject.activeInHierarchy) return rect;

            Assert.Fail($"No active element named {name}.");
            return null;
        }

        [UnityTest]
        public IEnumerator Play_OpensDifficultySelect()
        {
            yield return Load(TitleScene);

            FindButton("Play").onClick.Invoke();
            yield return null;

            Assert.That(Router.Current, Is.EqualTo(MenuScreen.Difficulty));
            Assert.That(FindScreen(MenuScreen.Difficulty).IsShown, Is.True);
            Assert.That(FindScreen(MenuScreen.Title).IsShown, Is.False);
        }

        [UnityTest]
        public IEnumerator Settings_OpenFromTheTitleAndComeBack()
        {
            yield return Load(TitleScene);

            FindButton("Settings").onClick.Invoke();
            yield return null;
            Assert.That(Router.Current, Is.EqualTo(MenuScreen.Settings));

            Router.Back();
            yield return null;
            Assert.That(Router.Current, Is.EqualTo(MenuScreen.Title));
            Assert.That(FindScreen(MenuScreen.Title).IsShown, Is.True);
        }

        [UnityTest]
        public IEnumerator Credits_NameTheStudioAndTheOriginal()
        {
            yield return Load(TitleScene);

            FindButton("Credits").onClick.Invoke();
            yield return null;

            var text = FindScreen(MenuScreen.Credits).GetComponentsInChildren<TMPro.TMP_Text>(true);
            var joined = string.Join(" ", Array.ConvertAll(text, t => t.text));
            Assert.That(joined, Does.Contain("DFT Games"));
            Assert.That(joined, Does.Contain("2010"), "the credits name the original");
        }

        [UnityTest]
        public IEnumerator Endless_IsShownButLocked()
        {
            yield return Load(TitleScene);
            FindButton("Play").onClick.Invoke();
            yield return null;

            var endless = FindButton("Endless");
            Assert.That(endless.gameObject.activeInHierarchy, Is.True, "Endless is shown");
            Assert.That(endless.interactable, Is.False, "Endless is locked until the campaign is finished");
        }

        [UnityTest]
        public IEnumerator ChoosingADifficulty_StartsTheLevelOnIt()
        {
            yield return Load(TitleScene);
            FindButton("Play").onClick.Invoke();
            yield return null;

            FindButton("Ace").onClick.Invoke();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == LevelScene, 10f, "the level to load");
            yield return null;

            var runner = Object.FindAnyObjectByType<GameRunner>();
            Assert.That(runner, Is.Not.Null);
            Assert.That(runner.Difficulty, Is.EqualTo(Difficulty.Ace));
            Assert.That(runner.Session.Settings.StartingLives, Is.EqualTo(DifficultySettings.Ace.StartingLives));
        }

        /// <summary>Presses and releases a key through the Input System, as a player would.</summary>
        static IEnumerator PressKey(Key key)
        {
            var keyboard = Keyboard.current;
            Assert.That(keyboard, Is.Not.Null, "no keyboard device in the test run");

            // A device the Input System reset on losing focus swallows queued events silently.
            if (!keyboard.enabled) InputSystem.EnableDevice(keyboard);

            // Queue the press and let the player loop process it: the Input System updates early in the frame,
            // so the router's Update sees WasPressedThisFrame. Calling InputSystem.Update() by hand here would
            // consume the press in a frame the router has already run.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            yield return null; // the Input System updates early in a frame; give it one to be seen

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        /// <summary>
        /// Presses a key until it has the effect asked for. A queued device event is occasionally dropped when
        /// the Editor is busy, which is not the game's fault and not worth a red run: pressing again still
        /// proves that this key is what causes the change, and a key that does nothing still fails.
        /// </summary>
        static IEnumerator PressUntil(Key key, Func<bool> done, float timeoutSeconds, string what)
        {
            var deadline = Time.unscaledTime + timeoutSeconds;
            while (!done())
            {
                if (Time.unscaledTime > deadline)
                {
                    var keyboard = Keyboard.current;
                    Assert.Fail($"Timed out waiting for {what}. keyboard={(keyboard == null ? "none" : keyboard.name)}, " +
                                $"enabled={keyboard?.enabled}, router listening={Router.InputEnabled}, " +
                                $"screen={Router.Current}, focus={Application.isFocused}");
                }

                yield return PressKey(key);
                var settle = Time.unscaledTime + 0.3f;
                while (!done() && Time.unscaledTime < settle) yield return null;
            }
        }

        /// <summary>Takes the player's lives down until the run is over.</summary>
        static IEnumerator EndTheRun(GameRunner runner)
        {
            while (!runner.Session.IsGameOver)
            {
                runner.Session.ReportPlayerHit(0, 1000f);
                yield return new WaitForSeconds(GameTuning.RespawnInvulnerabilitySeconds + 0.05f);
            }
        }

        // ---- In a level ----

        [UnityTest]
        public IEnumerator Escape_OpensAndClosesThePauseMenu()
        {
            // The whole input path: the Menu action map, the router's Update and the pause screen.
            yield return Load(LevelScene);
            Assert.That(Router.Current, Is.EqualTo(MenuScreen.None));

            Assert.That(Router.InputEnabled, Is.True, "the router is not listening for pause at all");
            yield return PressUntil(Key.Escape, () => Router.Current == MenuScreen.Pause, 4f,
                "Escape to open the pause menu");
            Assert.That(Time.timeScale, Is.Zero);

            yield return PressUntil(Key.Escape, () => Router.Current == MenuScreen.None, 4f,
                "Escape again to resume");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator Escape_InTheMenus_GoesBackRatherThanPausing()
        {
            yield return Load(TitleScene);
            FindButton("Settings").onClick.Invoke();
            yield return null;

            Assert.That(Router.InputEnabled, Is.True, "the router is not listening for back at all");
            yield return PressUntil(Key.Escape, () => Router.Current == MenuScreen.Title, 4f, "Escape to go back");

            Assert.That(Time.timeScale, Is.EqualTo(1f), "there is no game to pause in the menus");
        }

        [UnityTest]
        public IEnumerator PauseStillWorksAfterMovingBetweenScenes()
        {
            // The menu actions are one shared project asset, not a copy per scene. A router that switched it off
            // on its way out took pause with it: the next scene's router had already enabled it, so the level
            // after a scene change had no way to pause at all.
            yield return Load(TitleScene);
            Assert.That(Router.InputEnabled, Is.True, "the title's own input");

            yield return Load(LevelScene);
            Assert.That(Router.InputEnabled, Is.True, "pause is still listening after a scene change");

            yield return Load(TitleScene);
            yield return Load(LevelScene);
            Assert.That(Router.InputEnabled, Is.True, "and after another one");
        }

        [UnityTest]
        public IEnumerator Escape_AfterTheRunEnds_DoesNotPause()
        {
            // The run is over for a beat before its results appear; pausing there would strand the player.
            yield return Load(LevelScene);
            var runner = Object.FindAnyObjectByType<GameRunner>();
            yield return EndTheRun(runner);

            yield return PressKey(Key.Escape);

            Assert.That(Router.Current, Is.Not.EqualTo(MenuScreen.Pause));
            yield return WaitUntil(() => Router.Current == MenuScreen.GameOver, 6f,
                "the Game Over screen to appear anyway");
        }

        [UnityTest]
        public IEnumerator Pause_StopsGameTimeAndResumeStartsItAgain()
        {
            yield return Load(LevelScene);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            Router.Open(MenuScreen.Pause);
            yield return null;
            Assert.That(FindScreen(MenuScreen.Pause).IsShown, Is.True);
            Assert.That(Time.timeScale, Is.Zero, "game time stops while paused (GDD Pause)");

            Router.Back();
            yield return null;
            Assert.That(FindScreen(MenuScreen.Pause).IsShown, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator Pause_FreezesTheSimulation()
        {
            yield return Load(LevelScene);
            var runner = Object.FindAnyObjectByType<GameRunner>();
            var ship = runner.GetPlayerView(0);
            runner.SetCommandOverride(0, new PlayerCommand(new System.Numerics.Vector2(1f, 0f), false,
                System.Numerics.Vector2.UnitX));
            yield return new WaitForSeconds(0.2f);

            Router.Open(MenuScreen.Pause);
            yield return null;
            var position = ship.Position;
            yield return new WaitForSecondsRealtime(0.3f);

            Assert.That(ship.Position, Is.EqualTo(position), "the ship must not drift while paused");
        }

        [UnityTest]
        public IEnumerator SettingsFromPause_KeepsTheGameStoppedAndReturnsToPause()
        {
            yield return Load(LevelScene);
            Router.Open(MenuScreen.Pause);
            yield return null;

            Router.Open(MenuScreen.Settings);
            yield return null;
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(FindScreen(MenuScreen.Settings).IsShown, Is.True);

            Router.Back();
            yield return null;
            Assert.That(Router.Current, Is.EqualTo(MenuScreen.Pause), "back returns to Pause, not to the game");
            Assert.That(Time.timeScale, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LeavingAPausedLevel_DoesNotCarryTheFrozenTimeAway()
        {
            // Loading the title while paused used to leave timeScale at 0, freezing the next run.
            yield return Load(LevelScene);
            Router.Open(MenuScreen.Pause);
            yield return null;
            Assert.That(Time.timeScale, Is.Zero);

            yield return Load(TitleScene);

            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator Settings_ChangedInALevel_SurviveTheSceneChange()
        {
            yield return Load(LevelScene);
            GameFlow.Settings.SetMusicVolume(0.15f);
            GameFlow.Settings.SetScreenShake(false);

            yield return Load(TitleScene);

            Assert.That(GameFlow.Settings.Settings.MusicVolume, Is.EqualTo(0.15f));
            Assert.That(GameFlow.Settings.Settings.ScreenShake, Is.False);
        }

        [UnityTest]
        public IEnumerator SettingsScreen_ShowsTheSavedValues()
        {
            GameFlow.Settings.SetMusicVolume(0.25f);
            yield return Load(TitleScene);

            FindButton("Settings").onClick.Invoke();
            yield return null;

            var slider = FindScreen(MenuScreen.Settings).GetComponentInChildren<Slider>(true);
            Assert.That(slider.value, Is.EqualTo(0.25f).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator MovingASlider_SavesTheSetting()
        {
            yield return Load(TitleScene);
            FindButton("Settings").onClick.Invoke();
            yield return null;

            var slider = FindScreen(MenuScreen.Settings).GetComponentInChildren<Slider>(true);
            slider.value = 0.4f;
            yield return null;

            Assert.That(GameFlow.Settings.Settings.MusicVolume, Is.EqualTo(0.4f).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator GameOver_ShowsTheResultsWithItsButtons()
        {
            yield return Load(LevelScene);
            var runner = Object.FindAnyObjectByType<GameRunner>();
            yield return EndTheRun(runner);

            yield return WaitUntil(() => Router.Current == MenuScreen.GameOver, 6f, "the Game Over screen");

            Assert.That(FindScreen(MenuScreen.GameOver).IsShown, Is.True);
            Assert.That(FindButton("Retry").gameObject.activeInHierarchy, Is.True);
            Assert.That(FindButton("Title").gameObject.activeInHierarchy, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(FindButton("Retry").gameObject),
                "Retry is selected, so Enter or the south button retries");
        }

        [UnityTest]
        public IEnumerator GameOver_CannotBePausedAway()
        {
            yield return Load(LevelScene);
            var runner = Object.FindAnyObjectByType<GameRunner>();
            yield return EndTheRun(runner);

            yield return WaitUntil(() => Router.Current == MenuScreen.GameOver, 6f, "the Game Over screen");

            Router.Open(MenuScreen.Pause);
            Router.Back();
            yield return null;

            Assert.That(Router.Current, Is.EqualTo(MenuScreen.GameOver));
        }

        [UnityTest]
        public IEnumerator GameOver_ShowsTheRunsFigures()
        {
            yield return Load(LevelScene);
            var runner = Object.FindAnyObjectByType<GameRunner>();

            // A chain of kills, so the figures are not all zero.
            runner.Session.Score.RegisterKill(100);
            runner.Session.Score.RegisterKill(100);
            yield return EndTheRun(runner);
            yield return WaitUntil(() => Router.Current == MenuScreen.GameOver, 6f, "the Game Over screen");

            var score = runner.Session.Score;
            var texts = FindScreen(MenuScreen.GameOver).GetComponentsInChildren<TMPro.TMP_Text>(true);
            var joined = string.Join(" | ", Array.ConvertAll(texts, t => t.text));

            Assert.That(joined, Does.Contain("Score " + score.Score));
            Assert.That(joined, Does.Contain("Kills " + score.Kills));
            Assert.That(joined, Does.Contain("Best chain x" + score.BestChainMultiplier.ToString("0.0")));
        }

        [UnityTest]
        public IEnumerator Retry_RestartsTheLevelOnTheSameDifficulty()
        {
            yield return Load(TitleScene);
            FindButton("Play").onClick.Invoke();
            yield return null;
            FindButton("Cadet").onClick.Invoke();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == LevelScene, 10f, "the level to load");
            yield return null;

            var runner = Object.FindAnyObjectByType<GameRunner>();
            yield return EndTheRun(runner);
            yield return WaitUntil(() => Router.Current == MenuScreen.GameOver, 6f, "the Game Over screen");

            FindButton("Retry").onClick.Invoke();
            yield return WaitUntil(() => Object.FindAnyObjectByType<GameRunner>() != runner, 10f, "the level to reload");
            yield return null;

            Assert.That(Time.timeScale, Is.EqualTo(1f));
            var restarted = Object.FindAnyObjectByType<GameRunner>();
            Assert.That(restarted.Difficulty, Is.EqualTo(Difficulty.Cadet), "Retry keeps the chosen difficulty");
            Assert.That(restarted.Session.IsGameOver, Is.False, "a fresh run");
        }

        [UnityTest]
        public IEnumerator QuitToTitle_LeavesTheGameRunnableAgain()
        {
            yield return Load(LevelScene);
            Router.Open(MenuScreen.Pause);
            yield return null;

            Object.FindAnyObjectByType<PauseMenu>().QuitToTitle();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == TitleScene, 10f, "the title to load");
            yield return null;

            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(GameFlow.HasChosenDifficulty, Is.False, "no run is configured at the menus");
            Assert.That(Router.Current, Is.EqualTo(MenuScreen.Title));
        }

        // ---- The campaign ----

        [UnityTest]
        public IEnumerator StartingACampaign_BeginsARunOnTheFirstLevel()
        {
            yield return Load(TitleScene);
            FindButton("Play").onClick.Invoke();
            yield return null;
            FindButton("Pilot").onClick.Invoke();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == LevelScene, 10f, "the level to load");
            yield return null;

            var run = GameFlow.Run;
            Assert.That(run, Is.Not.Null, "no campaign run was started");
            Assert.That(run.LevelNumber, Is.EqualTo(1));
            Assert.That(run.Difficulty, Is.EqualTo(Difficulty.Pilot));
            Assert.That(run.LevelCount, Is.EqualTo(GameFlow.Campaign.LevelCount));
        }

        [UnityTest]
        public IEnumerator ClearingTheLastLevel_WinsTheCampaign()
        {
            // Eight levels is too many to play in a test, so the run is put on its last one.
            yield return StartCampaignAndClearTheLevel(fromTheFinalLevel: true);

            yield return WaitUntil(() => Router.Current == MenuScreen.Victory, 8f, "the campaign ending");

            Assert.That(GameFlow.Run.IsComplete, Is.True);
            Assert.That(GameFlow.Progress.IsCampaignComplete(Difficulty.Pilot), Is.True, "and it is remembered");
            Assert.That(GameFlow.Progress.IsEndlessUnlocked, Is.True, "which is what unlocks Endless");
        }

        [UnityTest]
        public IEnumerator TheEndingShowsTheRunsTotals()
        {
            yield return StartCampaignAndClearTheLevel(fromTheFinalLevel: true);
            yield return WaitUntil(() => Router.Current == MenuScreen.Victory, 8f, "the campaign ending");

            var texts = FindScreen(MenuScreen.Victory).GetComponentsInChildren<TMPro.TMP_Text>(true);
            var joined = string.Join(" | ", Array.ConvertAll(texts, t => t.text));

            Assert.That(joined, Does.Contain("Final score " + GameFlow.Run.CarriedScore));
            Assert.That(joined, Does.Contain("Kills " + GameFlow.Run.CarriedKills));
        }

        [UnityTest]
        public IEnumerator ALevelPlayedOnItsOwn_StillShowsSectorClear()
        {
            // Opening the scene directly is not a campaign; it must not try to advance one.
            yield return Load(LevelScene);
            var runner = Object.FindAnyObjectByType<GameRunner>();
            yield return ClearTheLevel(runner);

            yield return WaitUntil(() => Router.Current == MenuScreen.SectorClear, 8f, "the Sector Clear screen");
            Assert.That(GameFlow.Run, Is.Null);
        }

        /// <summary>
        /// Starts a campaign from the menus and beats the level's boss. With <paramref name="fromTheFinalLevel"/>
        /// the run is moved to the last level first, which is how a test reaches the campaign's ending without
        /// playing every level of it.
        /// </summary>
        IEnumerator StartCampaignAndClearTheLevel(bool fromTheFinalLevel = false)
        {
            yield return Load(TitleScene);
            FindButton("Play").onClick.Invoke();
            yield return null;
            FindButton("Pilot").onClick.Invoke();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == LevelScene, 10f, "the level to load");
            yield return null;

            if (fromTheFinalLevel)
            {
                Assert.That(GameFlow.Run, Is.Not.Null, "the campaign did not start");
                GameFlow.Run.SkipToFinalLevel();
            }

            yield return ClearTheLevel(Object.FindAnyObjectByType<GameRunner>());
        }

        static IEnumerator ClearTheLevel(GameRunner runner)
        {
            runner.SpawningEnabled = false;
            runner.StartBossFightNow();

            var boss = runner.Boss;
            yield return WaitUntil(() => boss.Brain.IsCoreOpen, 12f, "the boss core to open");
            boss.TakeCoreHit(boss.Brain.Health.Max, 0);
        }

        [UnityTest]
        public IEnumerator SectorClear_ShowsTheScoreBreakdown()
        {
            yield return Load(LevelScene);
            var runner = Object.FindAnyObjectByType<GameRunner>();
            runner.SpawningEnabled = false;
            runner.StartBossFightNow();

            var boss = runner.Boss;
            yield return WaitUntil(() => boss.Brain.IsCoreOpen, 12f, "the boss core to open");
            boss.TakeCoreHit(boss.Brain.Health.Max, 0);

            yield return WaitUntil(() => Router.Current == MenuScreen.SectorClear, 8f, "the Sector Clear screen");

            var texts = FindScreen(MenuScreen.SectorClear).GetComponentsInChildren<TMPro.TMP_Text>(true);
            var joined = string.Join(" ", Array.ConvertAll(texts, t => t.text));
            Assert.That(joined, Does.Contain("Bonuses"), "the breakdown shows the bonus points");
            Assert.That(runner.Session.Score.BonusPoints, Is.GreaterThan(0), "clearing the sector awards a bonus");
        }
    }
}
