using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YASS.Core;
using YASS.Feedback;

namespace YASS.UI
{
    /// <summary>
    /// One screen's panel. Showing it also selects its first button, so keyboard and gamepad can drive every menu
    /// without touching the mouse (GDD "UI Flow and Screens", Navigation).
    /// </summary>
    public sealed class MenuScreenView : MonoBehaviour
    {
        [SerializeField, Tooltip("Which screen this panel is.")]
        MenuScreen screen = MenuScreen.Title;

        [SerializeField, Tooltip("Shown and hidden with the screen. Defaults to this object.")]
        GameObject root;

        [SerializeField, Tooltip("Selected when the screen opens, for keyboard and gamepad navigation.")]
        Selectable firstSelected;

        public MenuScreen Screen => screen;

        public bool IsShown => Root.activeSelf;

        GameObject Root => root != null ? root : gameObject;

        void Reset() => root = gameObject;

        public void Show()
        {
            Root.SetActive(true);
            RebuildLayout();
            SelectFirst();
        }

        public void Hide() => Root.SetActive(false);

        /// <summary>
        /// Lays the screen out now rather than next frame. Text elements size themselves to their content, and
        /// that size arrives one frame after the column has already placed them: without this the screen's first
        /// frame shows overlapping text (the title over its tagline), which then fixes itself invisibly on the
        /// next layout pass. Re-opening a screen hid the problem, so it only ever showed on the first one.
        /// </summary>
        void RebuildLayout()
        {
            var rect = Root.transform as RectTransform;
            if (rect == null) return;

            // Marked rather than forced: the rebuild then happens in the canvas update, when the canvas has a
            // real size. Forcing it here (during Awake, for the screen that starts open) measures against a
            // canvas that does not exist yet.
            LayoutRebuilder.MarkLayoutForRebuild(rect);
        }

        /// <summary>
        /// Puts the selection on the first button. Interactable is checked because a locked entry (Endless) must
        /// not swallow the selection and leave the menu unusable from a gamepad.
        /// </summary>
        public void SelectFirst()
        {
            if (firstSelected == null || !firstSelected.IsInteractable()) return;

            var events = EventSystem.current;
            if (events == null) return;

            events.SetSelectedGameObject(null); // clear first, or a stale selection can keep the highlight
            events.SetSelectedGameObject(firstSelected.gameObject);
        }
    }
}
