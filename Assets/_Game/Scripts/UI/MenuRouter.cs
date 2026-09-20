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

        public MenuStack Stack { get; private set; }

        public MenuScreen Current => Stack.Current;

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

        void OnEnable() => _map?.Enable();

        void OnDisable() => _map?.Disable();

        // Loading another scene while paused would carry timeScale 0 into it, freezing the game for good.
        void OnDestroy() => ReleaseTimeScale();

        void Update()
        {
            RestoreLostSelection();
            ReportSelectionMoved();

            if (_cancel != null && _cancel.WasPressedThisFrame()) OnCancel();
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

        public void Open(MenuScreen screen)
        {
            Cue.Play(Sfx.UiConfirm);
            Stack.Open(screen);
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
