using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YASS.Core;
using YASS.Gameplay;
using YASS.UI;
using Object = UnityEngine.Object;

namespace YASS.Editor
{
    /// <summary>
    /// Adds the in-level menus to a level scene: pause, settings, Game Over and Sector Clear
    /// (GDD "UI Flow and Screens"). Re-runnable: it replaces whatever it built last time and leaves the rest of
    /// the scene alone. A scene the user already had open is left open afterwards.
    /// </summary>
    public static class LevelMenuBuilder
    {
        const string LevelScenePath = "Assets/_Game/Scenes/Level01.unity";
        const string MenusObjectName = "Menus";
        const string CanvasName = "MenuCanvas";

        [MenuItem("Tools/YASS/Build Level Menus")]
        public static void Build()
        {
            var scene = MenuSceneParts.OpenForBuilding(LevelScenePath, out var openedByBuilder);

            DestroyExisting(scene, MenusObjectName);
            DestroyExisting(scene, CanvasName);

            // Re-read the roots: the ones destroyed above would throw when their components are asked for.
            var roots = scene.GetRootGameObjects().Where(r => r != null).ToList();

            var canvas = MenuSceneParts.CreateCanvas(CanvasName, scene, sortingOrder: 10); // above the HUD
            MenuSceneParts.EnsureEventSystem(scene, FindInRoots<EventSystem>(roots));

            var menus = new GameObject(MenusObjectName);
            SceneManager.MoveGameObjectToScene(menus, scene);
            var router = menus.AddComponent<MenuRouter>();
            menus.AddComponent<SettingsApplier>();
            var levelFlow = menus.AddComponent<LevelFlow>();

            var pause = BuildPause(canvas, router, out var pauseFirst);
            var settings = MenuSceneParts.BuildSettings(canvas, router, out var settingsFirst);
            var gameOver = BuildGameOver(canvas, out var gameOverFirst, out var gameOverMenu);
            var sectorClear = BuildSectorClear(canvas, out var sectorClearFirst, out var sectorClearMenu);

            MenuSceneParts.AddScreen(pause, MenuScreen.Pause, pauseFirst);
            MenuSceneParts.AddScreen(settings, MenuScreen.Settings, settingsFirst);
            MenuSceneParts.AddScreen(gameOver, MenuScreen.GameOver, gameOverFirst);
            MenuSceneParts.AddScreen(sectorClear, MenuScreen.SectorClear, sectorClearFirst);

            MenuSceneParts.SerializeRouter(router, MenuScreen.None, new[]
            {
                pause.GetComponent<MenuScreenView>(),
                settings.GetComponent<MenuScreenView>(),
                gameOver.GetComponent<MenuScreenView>(),
                sectorClear.GetComponent<MenuScreenView>()
            });

            var runner = FindInRoots<GameRunner>(roots);
            if (runner == null) Debug.LogError($"No {nameof(GameRunner)} in {LevelScenePath}: the results screens will never appear.");

            MenuSceneParts.Serialize(levelFlow, o =>
            {
                MenuSceneParts.Find(o, "runner").objectReferenceValue = runner;
                MenuSceneParts.Find(o, "router").objectReferenceValue = router;
                MenuSceneParts.Find(o, "gameOver").objectReferenceValue = gameOverMenu;
                MenuSceneParts.Find(o, "sectorClear").objectReferenceValue = sectorClearMenu;
            });

            MenuSceneParts.LayoutNow(pause, settings, gameOver, sectorClear);

            // Nothing is open while the level is being played.
            pause.SetActive(false);
            settings.SetActive(false);
            gameOver.SetActive(false);
            sectorClear.SetActive(false);

            MenuSceneParts.SaveAndRelease(scene, LevelScenePath, openedByBuilder);
            Debug.Log($"Built the menus in {LevelScenePath}");
        }

        static T FindInRoots<T>(IEnumerable<GameObject> roots) where T : Component =>
            roots.Select(r => r.GetComponentInChildren<T>(true)).FirstOrDefault(c => c != null);

        static void DestroyExisting(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects().Where(r => r != null && r.name == name).ToList())
                Object.DestroyImmediate(root);
        }

        static GameObject BuildPause(Transform canvas, MenuRouter router, out Selectable first)
        {
            var panel = MenuUiFactory.CreatePanel("PauseScreen", canvas, MenuUiFactory.Panel);
            var column = MenuUiFactory.CreateColumn("Column", panel);

            MenuUiFactory.CreateText("Heading", column, "Paused", MenuUiFactory.HeadingFontSize, MenuUiFactory.Accent);

            var menu = panel.gameObject.AddComponent<PauseMenu>();

            var resume = MenuUiFactory.CreateButton("Resume", column, "Resume");
            var restart = MenuUiFactory.CreateButton("Restart", column, "Restart");
            var settings = MenuUiFactory.CreateButton("Settings", column, "Settings");
            var quit = MenuUiFactory.CreateButton("QuitToTitle", column, "Quit to Title");

            UnityEventTools.AddPersistentListener(resume.onClick, menu.Resume);
            UnityEventTools.AddPersistentListener(restart.onClick, menu.Restart);
            UnityEventTools.AddPersistentListener(settings.onClick, menu.OpenSettings);
            UnityEventTools.AddPersistentListener(quit.onClick, menu.QuitToTitle);

            MenuSceneParts.Serialize(menu, o => MenuSceneParts.Find(o, "router").objectReferenceValue = router);

            first = resume;
            return panel.gameObject;
        }

        static GameObject BuildGameOver(Transform canvas, out Selectable first, out ResultsMenu menu)
        {
            var panel = MenuUiFactory.CreatePanel("GameOverScreen", canvas, MenuUiFactory.Panel);
            var column = MenuUiFactory.CreateColumn("Column", panel);

            MenuUiFactory.CreateText("Heading", column, "Game Over", MenuUiFactory.HeadingFontSize, MenuUiFactory.Accent);
            var score = MenuUiFactory.CreateText("Score", column, "Score 0", MenuUiFactory.BodyFontSize, MenuUiFactory.Text);
            var kills = MenuUiFactory.CreateText("Kills", column, "Kills 0", MenuUiFactory.BodyFontSize, MenuUiFactory.Text);
            var chain = MenuUiFactory.CreateText("Chain", column, "Best chain x1.0", MenuUiFactory.BodyFontSize,
                MenuUiFactory.Secondary);

            menu = panel.gameObject.AddComponent<ResultsMenu>();

            var retry = MenuUiFactory.CreateButton("Retry", column, "Retry");
            var title = MenuUiFactory.CreateButton("Title", column, "Title");
            UnityEventTools.AddPersistentListener(retry.onClick, menu.Retry);
            UnityEventTools.AddPersistentListener(title.onClick, menu.QuitToTitle);

            MenuSceneParts.Serialize(menu, o =>
            {
                MenuSceneParts.Find(o, "scoreText").objectReferenceValue = score;
                MenuSceneParts.Find(o, "killsText").objectReferenceValue = kills;
                MenuSceneParts.Find(o, "chainText").objectReferenceValue = chain;
            });

            first = retry;
            return panel.gameObject;
        }

        static GameObject BuildSectorClear(Transform canvas, out Selectable first, out ResultsMenu menu)
        {
            var panel = MenuUiFactory.CreatePanel("SectorClearScreen", canvas, MenuUiFactory.Panel);
            var column = MenuUiFactory.CreateColumn("Column", panel);

            MenuUiFactory.CreateText("Heading", column, "Sector Clear", MenuUiFactory.HeadingFontSize,
                MenuUiFactory.Secondary);
            var score = MenuUiFactory.CreateText("Score", column, "Score 0", MenuUiFactory.BodyFontSize, MenuUiFactory.Text);
            var bonus = MenuUiFactory.CreateText("Bonus", column, "Bonuses 0", MenuUiFactory.BodyFontSize,
                MenuUiFactory.Text);
            var kills = MenuUiFactory.CreateText("Kills", column, "Kills 0", MenuUiFactory.BodyFontSize, MenuUiFactory.Text);

            menu = panel.gameObject.AddComponent<ResultsMenu>();

            var continueButton = MenuUiFactory.CreateButton("Continue", column, "Continue");
            UnityEventTools.AddPersistentListener(continueButton.onClick, menu.QuitToTitle);

            MenuSceneParts.Serialize(menu, o =>
            {
                MenuSceneParts.Find(o, "scoreText").objectReferenceValue = score;
                MenuSceneParts.Find(o, "killsText").objectReferenceValue = kills;
                MenuSceneParts.Find(o, "clearBonusText").objectReferenceValue = bonus;
                MenuSceneParts.Find(o, "clearBonusPrefix").stringValue = "Bonuses ";
            });

            first = continueButton;
            return panel.gameObject;
        }
    }
}
