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
        /// Puts the selection on the first button, or on the first usable one if that button is not there.
        /// </summary>
        /// <remarks>
        /// **Falling back matters more than it sounds.** Screens hide the buttons that do not apply: the
        /// account screen hides Sign in once somebody is signed in, and hid it always on the web build
        /// before itch.io sign-in existed. Its chosen button was that one, so the selection was left null,
        /// and <c>MenuRouter.RestoreLostSelection</c> then retried the same impossible call every frame.
        /// The screen was usable with a mouse and dead to a keyboard or a gamepad, which is exactly how it
        /// was reported: the gamepad works, except sometimes.
        ///
        /// Note that <c>IsInteractable</c> is not enough on its own. It reports whether a Selectable is
        /// switched on, not whether it is *there*, so a deactivated button passes it and then cannot be
        /// selected. Active is checked separately for that reason.
        ///
        /// Interactable is still checked, because a locked entry (Endless) must not swallow the selection
        /// and leave the menu unusable either.
        /// </remarks>
        public void SelectFirst()
        {
            var events = EventSystem.current;
            if (events == null) return;

            var wanted = Usable(firstSelected) ? firstSelected : FirstUsable();
            if (wanted == null) return;

            events.SetSelectedGameObject(null); // clear first, or a stale selection can keep the highlight
            events.SetSelectedGameObject(wanted.gameObject);
        }

        static bool Usable(Selectable candidate) =>
            candidate != null && candidate.gameObject.activeInHierarchy && candidate.IsInteractable();

        /// <summary>
        /// Anything on this screen that can take the selection, in the order it is laid out. Only reached
        /// when the screen's chosen button is unavailable, so the search is not on the usual path.
        /// </summary>
        Selectable FirstUsable()
        {
            // Inactive ones are not even returned, which is most of what is being guarded against here.
            foreach (var candidate in Root.GetComponentsInChildren<Selectable>(false))
                if (Usable(candidate)) return candidate;

            return null;
        }
    }
}
