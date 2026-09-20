using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YASS.Core;
using YASS.UI;

namespace YASS.Editor
{
    /// <summary>
    /// Builds <c>Assets/_Game/Scenes/Title.unity</c>: the title, difficulty, settings and credits screens
    /// (GDD "UI Flow and Screens"). Re-runnable, so the menus are authored by code rather than by hand and can be
    /// rebuilt after a layout or palette change.
    /// </summary>
    public static class TitleSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/Title.unity";

        [MenuItem("Tools/YASS/Build Title Scene")]
        public static void Build()
        {
            // Unity refuses to save over a scene that is already open, so close that one first, asking about
            // unsaved work rather than discarding it.
            var open = SceneManager.GetSceneByPath(ScenePath);
            if (open.IsValid() && open.isLoaded)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                if (SceneManager.sceneCount == 1)
                {
                    Debug.LogError($"{ScenePath} is the only open scene; open another scene before rebuilding it.");
                    return;
                }

                EditorSceneManager.CloseScene(open, true);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            CreateCamera(scene);
            MenuSceneParts.EnsureEventSystem(scene, null);

            var canvas = MenuSceneParts.CreateCanvas("Canvas", scene);
            var menus = new GameObject("Menus");
            SceneManager.MoveGameObjectToScene(menus, scene);
            var router = menus.AddComponent<MenuRouter>();
            menus.AddComponent<SettingsApplier>();

            var title = BuildTitle(canvas, router, out var titleFirst);
            var difficulty = BuildDifficulty(canvas, router, out var difficultyFirst);
            var settings = MenuSceneParts.BuildSettings(canvas, router, out var settingsFirst);
            var credits = BuildCredits(canvas, router, out var creditsFirst);

            MenuSceneParts.AddScreen(title, MenuScreen.Title, titleFirst);
            MenuSceneParts.AddScreen(difficulty, MenuScreen.Difficulty, difficultyFirst);
            MenuSceneParts.AddScreen(settings, MenuScreen.Settings, settingsFirst);
            MenuSceneParts.AddScreen(credits, MenuScreen.Credits, creditsFirst);

            MenuSceneParts.SerializeRouter(router, MenuScreen.Title, new[]
            {
                title.GetComponent<MenuScreenView>(),
                difficulty.GetComponent<MenuScreenView>(),
                settings.GetComponent<MenuScreenView>(),
                credits.GetComponent<MenuScreenView>()
            });

            MenuSceneParts.LayoutNow(title, difficulty, settings, credits);

            // Only the root screen starts visible; the router shows the rest on demand.
            difficulty.SetActive(false);
            settings.SetActive(false);
            credits.SetActive(false);

            MenuSceneParts.SaveAndRelease(scene, ScenePath, true);
            AddToBuildSettings();
            Debug.Log($"Built {ScenePath}");
        }

        static void CreateCamera(Scene scene)
        {
            // The AudioListener lives here: without one the volume settings have nothing to act on.
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            SceneManager.MoveGameObjectToScene(go, scene);
            go.tag = "MainCamera";

            var camera = go.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = MenuUiFactory.Background;
            go.transform.position = new Vector3(0f, 0f, -10f);
        }

        static GameObject BuildTitle(Transform canvas, MenuRouter router, out Selectable first)
        {
            var panel = MenuUiFactory.CreatePanel("TitleScreen", canvas, Color.clear, false);
            var column = MenuUiFactory.CreateColumn("Column", panel);

            MenuUiFactory.CreateText("Logo", column, "YASS 2026", MenuUiFactory.TitleFontSize, MenuUiFactory.Accent);
            MenuUiFactory.CreateText("Tagline", column, "Yet Another Space Shooter", MenuUiFactory.BodyFontSize,
                MenuUiFactory.Text);

            var menu = panel.gameObject.AddComponent<TitleMenu>();

            var play = MenuUiFactory.CreateButton("Play", column, "Play");
            var settings = MenuUiFactory.CreateButton("Settings", column, "Settings");
            var credits = MenuUiFactory.CreateButton("Credits", column, "Credits");
            var quit = MenuUiFactory.CreateButton("Quit", column, "Quit");

            UnityEventTools.AddPersistentListener(play.onClick, menu.Play);
            UnityEventTools.AddPersistentListener(settings.onClick, menu.OpenSettings);
            UnityEventTools.AddPersistentListener(credits.onClick, menu.OpenCredits);
            UnityEventTools.AddPersistentListener(quit.onClick, menu.Quit);

            MenuSceneParts.Serialize(menu, o =>
            {
                MenuSceneParts.Find(o, "router").objectReferenceValue = router;
                MenuSceneParts.Find(o, "quitButton").objectReferenceValue = quit.gameObject;
            });

            first = play;
            return panel.gameObject;
        }

        static GameObject BuildDifficulty(Transform canvas, MenuRouter router, out Selectable first)
        {
            var panel = MenuUiFactory.CreatePanel("DifficultyScreen", canvas, MenuUiFactory.Panel);
            var column = MenuUiFactory.CreateColumn("Column", panel);

            MenuUiFactory.CreateText("Heading", column, "Choose your wings", MenuUiFactory.HeadingFontSize,
                MenuUiFactory.Accent);

            var menu = panel.gameObject.AddComponent<DifficultyMenu>();

            var cadet = CreateChoice(column, "Cadet", "Cadet", "Five lives, gentler waves. Learn the sector.");
            var pilot = CreateChoice(column, "Pilot", "Pilot", "Three lives. The intended fight.");
            var ace = CreateChoice(column, "Ace", "Ace", "Two lives, heavier waves, double score.");
            var endless = CreateChoice(column, "Endless", "Endless (locked)", "Finish the campaign to unlock.");

            UnityEventTools.AddPersistentListener(cadet.onClick, menu.ChooseCadet);
            UnityEventTools.AddPersistentListener(pilot.onClick, menu.ChoosePilot);
            UnityEventTools.AddPersistentListener(ace.onClick, menu.ChooseAce);

            var back = MenuUiFactory.CreateButton("Back", column, "Back");
            UnityEventTools.AddPersistentListener(back.onClick, router.Back);

            endless.interactable = false; // locked in the saved scene, not only once the game runs
            MenuSceneParts.Serialize(menu, o =>
                MenuSceneParts.Find(o, "endlessButton").objectReferenceValue = endless);

            first = pilot; // the intended difficulty is the default choice
            return panel.gameObject;
        }

        static Button CreateChoice(Transform column, string name, string label, string description)
        {
            var button = MenuUiFactory.CreateButton(name, column, label);
            MenuUiFactory.CreateText(name + "Description", column, description, 24, MenuUiFactory.Text);
            return button;
        }

        static GameObject BuildCredits(Transform canvas, MenuRouter router, out Selectable first)
        {
            var panel = MenuUiFactory.CreatePanel("CreditsScreen", canvas, MenuUiFactory.Panel);
            var column = MenuUiFactory.CreateColumn("Column", panel);

            MenuUiFactory.CreateText("Heading", column, "Credits", MenuUiFactory.HeadingFontSize, MenuUiFactory.Accent);
            MenuUiFactory.CreateText("Studio", column, "DFT Games Studios", MenuUiFactory.BodyFontSize, MenuUiFactory.Text);
            MenuUiFactory.CreateText("Original", column,
                "Based on YASS (Xbox 360, Xbox Live Indie Games, 2010)", 24, MenuUiFactory.Text);
            MenuUiFactory.CreateText("Tools", column,
                "Art generated with Unity AI (Gemini 3.1 Flash) and Game UI Essentials", 24, MenuUiFactory.Text);
            MenuUiFactory.CreateText("Engine", column,
                "Built with Unity 6, Universal Render Pipeline and TextMeshPro", 24, MenuUiFactory.Text);

            var back = MenuUiFactory.CreateButton("Back", column, "Back");
            UnityEventTools.AddPersistentListener(back.onClick, router.Back);

            first = back;
            return panel.gameObject;
        }

        /// <summary>Puts the title scene first in the build, ahead of the levels.</summary>
        static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
