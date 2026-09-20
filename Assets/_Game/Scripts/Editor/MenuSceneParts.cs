using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YASS.Core;
using YASS.UI;
using Object = UnityEngine.Object;

namespace YASS.Editor
{
    /// <summary>
    /// The pieces both scene builders need: the canvas, the event system, the settings screen (which is reachable
    /// from the Title and from Pause, so it exists in both scenes) and the serialisation helpers.
    /// </summary>
    public static class MenuSceneParts
    {
        public const string GameActionsPath = "Assets/_Game/Input/YASS.inputactions";
        public const string UiActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";

        public static Transform CreateCanvas(string name, Scene scene, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(go, scene);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Menus are columns of buttons, so height is what must fit: matching height keeps every entry on
            // screen at wide aspect ratios instead of pushing the first and last off it.
            scaler.matchWidthOrHeight = 1f;
            return go.transform;
        }

        /// <summary>Creates the event system, or fixes up one an earlier run left behind.</summary>
        public static void EnsureEventSystem(Scene scene, EventSystem existing)
        {
            var go = existing != null
                ? existing.gameObject
                : new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            if (existing == null) SceneManager.MoveGameObjectToScene(go, scene);

            var module = go.GetComponent<InputSystemUIInputModule>();
            if (module == null) module = go.AddComponent<InputSystemUIInputModule>();

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(UiActionsPath);
            if (actions != null) module.actionsAsset = actions;

            // Clicking a panel's background must not clear the selection, or keyboard and gamepad navigation
            // goes dead until the mouse finds a button again (GDD "UI Flow and Screens", Navigation).
            module.deselectOnBackgroundClick = false;
        }

        /// <summary>
        /// The settings screen: music and effects volume, screen shake and, on desktop, fullscreen
        /// (GDD "UI Flow and Screens", Settings). Built once here so the Title and the levels cannot drift apart.
        /// </summary>
        public static GameObject BuildSettings(Transform canvas, MenuRouter router, out Selectable first)
        {
            var panel = MenuUiFactory.CreatePanel("SettingsScreen", canvas, MenuUiFactory.Panel);
            var column = MenuUiFactory.CreateColumn("Column", panel);

            MenuUiFactory.CreateText("Heading", column, "Settings", MenuUiFactory.HeadingFontSize, MenuUiFactory.Accent);

            var menu = panel.gameObject.AddComponent<SettingsMenu>();

            MenuUiFactory.CreateRow("MusicRow", column, "Music volume", out var musicSlot);
            var music = MenuUiFactory.CreateSlider("MusicVolume", musicSlot);

            MenuUiFactory.CreateRow("SfxRow", column, "Sound effects volume", out var sfxSlot);
            var sfx = MenuUiFactory.CreateSlider("SfxVolume", sfxSlot);

            MenuUiFactory.CreateRow("ShakeRow", column, "Screen shake", out var shakeSlot);
            var shake = MenuUiFactory.CreateToggle("ScreenShake", shakeSlot);

            var fullscreenRow = MenuUiFactory.CreateRow("FullscreenRow", column, "Fullscreen", out var fullscreenSlot);
            var fullscreen = MenuUiFactory.CreateToggle("Fullscreen", fullscreenSlot);

            var back = MenuUiFactory.CreateButton("Back", column, "Back");
            UnityEventTools.AddPersistentListener(back.onClick, router.Back);

            Serialize(menu, o =>
            {
                Find(o, "musicVolume").objectReferenceValue = music;
                Find(o, "sfxVolume").objectReferenceValue = sfx;
                Find(o, "screenShake").objectReferenceValue = shake;
                Find(o, "fullscreen").objectReferenceValue = fullscreen;
                Find(o, "fullscreenRow").objectReferenceValue = fullscreenRow.gameObject;
            });

            first = music;
            return panel.gameObject;
        }

        public static void AddScreen(GameObject panel, MenuScreen screen, Selectable first)
        {
            var view = panel.AddComponent<MenuScreenView>();
            Serialize(view, o =>
            {
                Find(o, "screen").enumValueIndex = (int)screen;
                Find(o, "root").objectReferenceValue = panel;
                Find(o, "firstSelected").objectReferenceValue = first;
            });
        }

        public static void SerializeRouter(MenuRouter router, MenuScreen root, IReadOnlyList<MenuScreenView> screens)
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(GameActionsPath);
            if (actions == null) Debug.LogError($"Missing {GameActionsPath}: pause and back will not work.");

            Serialize(router, o =>
            {
                Find(o, "rootScreen").enumValueIndex = (int)root;
                Find(o, "actions").objectReferenceValue = actions;

                var array = Find(o, "screens");
                array.arraySize = screens.Count;
                for (var i = 0; i < screens.Count; i++)
                    array.GetArrayElementAtIndex(i).objectReferenceValue = screens[i];
            });
        }

        /// <summary>
        /// Lays the screens out before the scene is saved. Without this the scene file keeps the positions the
        /// objects had as they were created (everything stacked at the centre), and a screen that is already
        /// active when the scene loads has nothing to trigger a rebuild: the title drew over its tagline until
        /// another screen was opened and came back.
        /// </summary>
        public static void LayoutNow(params GameObject[] panels)
        {
            Canvas.ForceUpdateCanvases();

            foreach (var panel in panels)
            {
                if (panel == null) continue;

                // Innermost first: a column's own height depends on the children it has already placed.
                foreach (var child in panel.GetComponentsInChildren<RectTransform>(true).Reverse())
                    LayoutRebuilder.ForceRebuildLayoutImmediate(child);
            }

            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// Lays out every canvas in a scene before it is saved. Any builder that touches a scene with menus in
        /// it has to do this, not only the one that built them: the saved positions are what a screen shows on
        /// its first frame.
        /// </summary>
        static void LayoutCanvases(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                    LayoutNow(canvas.gameObject);
            }
        }

        public static void Serialize(Object target, Action<SerializedObject> edit)
        {
            var serialized = new SerializedObject(target);
            edit(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Finds a serialised field, failing loudly rather than with a bare null reference on a rename.</summary>
        public static SerializedProperty Find(SerializedObject serialized, string field)
        {
            var property = serialized.FindProperty(field);
            if (property == null)
                throw new InvalidOperationException(
                    $"{serialized.targetObject.GetType().Name} has no serialised field '{field}'.");

            return property;
        }

        /// <summary>
        /// Opens a scene for building. Reports whether the builder opened it, so a scene the user already had
        /// open is left open rather than being saved and closed underneath them.
        /// </summary>
        public static Scene OpenForBuilding(string path, out bool openedByBuilder)
        {
            var existing = SceneManager.GetSceneByPath(path);
            if (existing.IsValid() && existing.isLoaded)
            {
                openedByBuilder = false;
                return existing;
            }

            openedByBuilder = true;
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        /// <summary>Saves the built scene, and closes it only if the builder was the one that opened it.</summary>
        public static void SaveAndRelease(Scene scene, string path, bool openedByBuilder)
        {
            LayoutCanvases(scene);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene, path))
            {
                Debug.LogError($"Failed to save {path}; the built menus are still in the open scene.");
                return;
            }

            if (openedByBuilder) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
