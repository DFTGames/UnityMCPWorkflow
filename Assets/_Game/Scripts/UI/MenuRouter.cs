using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using YASS.Core;
using YASS.Feedback;

namespace YASS.UI
{
    /// <summary>
    /// Shows whichever screen <see cref="MenuStack"/> says is current, and stops game time while a menu covers a
    /// level (GDD "UI Flow and Screens"). One router per scene: the title scene roots at
    /// <see cref="MenuScreen.Title"/>, a level roots at <see cref="MenuScreen.None"/>.
    /// </summary>
    public sealed class MenuRouter : MonoBehaviour
    {
        const string MapName = "Menu";

        [SerializeField, Tooltip("The screen shown when nothing is open: Title in the menus, None in a level.")]
        MenuScreen rootScreen = MenuScreen.None;

        [SerializeField] MenuScreenView[] screens = Array.Empty<MenuScreenView>();
        [SerializeField] InputActionAsset actions;

        InputActionMap _map;
        InputAction _pause;
        InputAction _cancel;
        MenuScreenView _currentView;
        GameObject _lastSelected;
        bool _selectionIsOurs;
        bool _timeScaleHeld;
        bool _holdsInput;

        /// <summary>
        /// How many routers are currently using the menu action map. The actions asset is a shared project
        /// asset, not a copy per scene, so a router that switched it off on its way out would take the input with
        /// it: loading the next level runs the new router's OnEnable before the old one's OnDisable, which left
        /// the player with no way to pause.
        /// </summary>
        static int _inputHolders;

        public MenuStack Stack { get; private set; }

        public MenuScreen Current => Stack.Current;

        /// <summary>Test seam: whether pause and back are actually listening.</summary>
        internal bool InputEnabled => _map != null && _map.enabled;

        /// <summary>Statics survive a domain reload, which this project disables; start each run from zero.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _inputHolders = 0;


        void Awake()
        {
            Stack = new MenuStack(rootScreen);
            Stack.CurrentChanged += _ => Refresh();

            // A missing binding would leave the game with no way to pause, which no test can catch through the
            // router's own methods: say so loudly rather than failing silently.
            if (actions == null)
            {
                Debug.LogError($"{nameof(MenuRouter)}: no input actions asset assigned; pause and back will not work.", this);
            }
            else
            {
                _map = actions.FindActionMap(MapName, true);
                _pause = _map.FindAction("Pause", true);
                _cancel = _map.FindAction("Cancel", true);
            }

            Refresh();
        }

        void OnEnable()
        {
            if (_map == null || _holdsInput) return;

            _holdsInput = true;
            _inputHolders++;
            _map.Enable();
        }

        void OnDisable()
        {
            if (_map == null || !_holdsInput) return;

            _holdsInput = false;
            // Only the last one out turns the lights off.
            if (--_inputHolders <= 0)
            {
                _inputHolders = 0;
                _map.Disable();
            }
        }

        // Loading another scene while paused would carry timeScale 0 into it, freezing the game for good.
        void OnDestroy() => ReleaseTimeScale();

        void Update()
        {
            RestoreLostSelection();
            ReportSelectionMoved();

            if (_cancel != null && _cancel.WasPressedThisFrame() && !IsTyping) OnCancel();
            else if (_pause != null && _pause.WasPressedThisFrame()) OnPause();
        }

        /// <summary>
        /// Puts the selection back on the open screen when it has been lost: clicking the background clears it,
        /// and the EventSystem may not exist yet when the first screen is shown during scene load. Without this
        /// the keyboard and gamepad go dead until the mouse finds a button (GDD "UI Flow", Navigation).
        /// </summary>
        /// <summary>A soft tick as the selection moves, so keyboard and gamepad navigation has a voice too.</summary>
        void ReportSelectionMoved()
        {
            var events = EventSystem.current;
            if (events == null) return;

            var selected = events.currentSelectedGameObject;
            if (selected == _lastSelected) return;

            var hadSelection = _lastSelected != null;
            _lastSelected = selected;

            // Selections the game makes for itself (opening a screen, restoring one lost to a stray click) are
            // recorded silently: only navigation the player performed is heard.
            if (hadSelection && selected != null && !_selectionIsOurs) Cue.Play(Sfx.UiMove);
            _selectionIsOurs = false;
        }

        /// <summary>
        /// Puts the selection back on the open screen when it has been lost: clicking the background clears it,
        /// and the EventSystem may not exist yet when the first screen is shown during scene load. Without this
        /// the keyboard and gamepad go dead until the mouse finds a button (GDD "UI Flow", Navigation).
        /// </summary>
        void RestoreLostSelection()
        {
            if (_currentView == null) return;

            var events = EventSystem.current;
            if (events == null || events.currentSelectedGameObject != null) return;

            _selectionIsOurs = true;
            _currentView.SelectFirst();
        }

        /// <summary>
        /// Whether this scene has that screen at all. Settings is reached from the title and from a pause in
        /// a level, and the account screen only exists in the first: a button that opens nothing is worse
        /// than no button, so the row is hidden where there is nothing behind it.
        /// </summary>
        public bool Has(MenuScreen screen)
        {
            foreach (var view in screens)
                if (view != null && view.Screen == screen) return true;

            return false;
        }

        public void Open(MenuScreen screen)
        {
            Cue.Play(Sfx.UiConfirm);
            Stack.Open(screen);
        }

        /// <summary>
        /// Opens a screen in place of the open one, so back skips it. For a screen the player has answered.
        /// </summary>
        public void Replace(MenuScreen screen)
        {
            Cue.Play(Sfx.UiConfirm);
            Stack.Replace(screen);
        }

        /// <summary>For Back buttons; also what Escape and the gamepad's east button do.</summary>
        public void Back()
        {
            Cue.Play(Sfx.UiMove);
            Stack.Back();
        }

        public void ShowResult(MenuScreen screen) => Stack.ShowResult(screen);

        /// <summary>The run is over: stop anything else opening, including during the wait for the results panel.</summary>
        public void LockForResult() => Stack.LockForResult();

        /// <summary>Escape: leave the open screen, or open the pause menu when playing.</summary>
        void OnCancel()
        {
            if (Stack.Back()) return;
            if (rootScreen == MenuScreen.None) Stack.Open(MenuScreen.Pause);
        }

        /// <summary>The gamepad's Start button toggles pause, and does nothing in the menus.</summary>
        void OnPause()
        {
            if (rootScreen != MenuScreen.None) return;

            if (Stack.Current == MenuScreen.Pause) Stack.Back();
            else if (Stack.Depth == 0) Stack.Open(MenuScreen.Pause);
        }

        /// <summary>
        /// Losing focus pauses a level: the player has gone, and a ship that dies while they are in another
        /// window is not a fair death. Regaining it does not unpause, because the pause menu is now theirs to
        /// leave when they are ready to play again.
        /// </summary>
        void OnApplicationFocus(bool hasFocus) => PauseIfTheyHaveGone(hasFocus);

        /// <summary>
        /// The same thing on mobile, where switching app raises this rather than the focus callback. Both are
        /// wired, because which one arrives is the platform's business, and opening the pause menu twice is
        /// harmless: the stack refuses a screen that is already current.
        /// </summary>
        void OnApplicationPause(bool paused) => PauseIfTheyHaveGone(!paused);

        void PauseIfTheyHaveGone(bool hasFocus)
        {
            if (!MenuStack.ShouldPauseOnFocusLoss(hasFocus, Application.isEditor)) return;
            if (rootScreen != MenuScreen.None || Stack.Depth != 0) return;

            // Open rather than OnPause: this is never a toggle. Anything already open, including a result
            // panel, is left alone, and the stack refuses to open over a run that has ended.
            Stack.Open(MenuScreen.Pause);
        }

        /// <summary>
        /// Whether the player is in a text field. Escape belongs to the field then: it restores what was
        /// there before the edit, and without this one press would both undo their typing and close the
        /// screen out from under them.
        /// </summary>
        bool IsTyping
        {
            get
            {
                var events = EventSystem.current;
                if (events == null) return false;

                var selected = events.currentSelectedGameObject;
                if (selected == null) return false;

                var field = selected.GetComponent<TMPro.TMP_InputField>();
                return field != null && field.isFocused;
            }
        }

        void Refresh()
        {
            var current = Stack.Current;
            _currentView = null;
            foreach (var screen in screens)
            {
                if (screen == null) continue;
                if (screen.Screen == current)
                {
                    _currentView = screen;
                    _selectionIsOurs = true; // the screen selects its own first button; that is not navigation
                    screen.Show();
                }
                else if (screen.IsShown)
                {
                    screen.Hide();
                }
            }

            if (Stack.PausesGame) HoldTimeScale();
            else ReleaseTimeScale();
        }

        void HoldTimeScale()
        {
            if (_timeScaleHeld) return;

            _timeScaleHeld = true;
            Time.timeScale = 0f;
        }

        void ReleaseTimeScale()
        {
            if (!_timeScaleHeld) return;

            _timeScaleHeld = false;
            Time.timeScale = 1f;
        }
    }
}
