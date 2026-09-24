using System;
using System.Collections.Generic;

namespace YASS.Core
{
    /// <summary>The screens the game can show (GDD "UI Flow and Screens").</summary>
    public enum MenuScreen
    {
        /// <summary>No menu: the level is being played.</summary>
        None = 0,
        Title,
        Difficulty,
        Settings,
        Credits,
        Pause,
        GameOver,
        SectorClear,
        Victory,

        /// <summary>The six boards, browsable from the title (GDD "Scoring", Leaderboards).</summary>
        Leaderboards,

        /// <summary>Asked for once, before the first run: a board entry needs a name.</summary>
        NameEntry
    }

    /// <summary>
    /// Which screen is showing and how "back" behaves (GDD "UI Flow and Screens"). A stack, because Settings is
    /// reachable from both the Title and the Pause menu and has to return where it came from.
    /// </summary>
    public sealed class MenuStack
    {
        readonly List<MenuScreen> _stack = new List<MenuScreen>(4);

        /// <param name="root">
        /// The screen shown when nothing is pushed: <see cref="MenuScreen.Title"/> in the title scene,
        /// <see cref="MenuScreen.None"/> in a level.
        /// </param>
        public MenuStack(MenuScreen root = MenuScreen.None)
        {
            Root = root;
        }

        public MenuScreen Root { get; }

        /// <summary>
        /// True once the run is over. Nothing new opens after that, not even in the beat before the results
        /// panel appears: pausing a finished run would show a Resume button for a run that cannot resume
        /// (GDD "UI Flow and Screens": results "cannot be paused or dismissed with Back").
        /// </summary>
        public bool IsLockedForResult { get; private set; }

        public MenuScreen Current => _stack.Count > 0 ? _stack[_stack.Count - 1] : Root;

        /// <summary>How many screens are stacked above the root.</summary>
        public int Depth => _stack.Count;

        /// <summary>True while a menu covers the level, which is when the game should stop advancing.</summary>
        public bool PausesGame => Depth > 0 && Root == MenuScreen.None && !IsResult(Current);

        /// <summary>Raised whenever <see cref="Current"/> changes.</summary>
        public event Action<MenuScreen> CurrentChanged;

        /// <summary>
        /// Whether losing focus should pause the game. It should: a player who alt-tabs, takes a call or
        /// switches app has stopped playing, and coming back to a ship that died while they were away is the
        /// worst way to find that out. The same on every platform, because the reason is the same.
        /// </summary>
        /// <remarks>
        /// Never in the editor. Focus there belongs to the Console, the Inspector and the Game view in turn,
        /// so the game would pause every time somebody clicked away from it; and an automated PlayMode run
        /// never has focus at all, so this would pause every test in the suite and each one would then time
        /// out waiting for a game that is not running.
        /// </remarks>
        public static bool ShouldPauseOnFocusLoss(bool hasFocus, bool inEditor) => !hasFocus && !inEditor;

        /// <summary>
        /// Game Over and Sector Clear end the run: they are left by choosing a button, never by pressing back,
        /// and nothing can be stacked on top of them.
        /// </summary>
        public static bool IsResult(MenuScreen screen) =>
            screen == MenuScreen.GameOver || screen == MenuScreen.SectorClear || screen == MenuScreen.Victory;

        /// <summary>
        /// Called the moment the run ends, before the results panel is shown, so the short delay before it
        /// appears cannot be spent in the pause menu.
        /// </summary>
        public void LockForResult()
        {
            if (IsLockedForResult) return;

            IsLockedForResult = true;
            CloseAll(); // anything already open belongs to a run that is over
        }

        public void Open(MenuScreen screen)
        {
            if (screen == MenuScreen.None) throw new ArgumentOutOfRangeException(nameof(screen));
            if (screen == Current) return;
            if (IsLockedForResult || IsResult(Current)) return; // the run is over: nothing opens over the result

            Push(screen);
        }

        /// <summary>
        /// Swaps the open screen for another, so back skips the one being left. For a screen the player has
        /// answered rather than merely visited: the name screen opens the difficulty screen this way, and
        /// back from there goes to the title rather than asking their name again.
        /// </summary>
        public void Replace(MenuScreen screen)
        {
            if (screen == MenuScreen.None) throw new ArgumentOutOfRangeException(nameof(screen));
            if (IsLockedForResult || IsResult(Current)) return;

            if (Depth > 0) _stack.RemoveAt(_stack.Count - 1);
            if (screen == Current)
            {
                CurrentChanged?.Invoke(Current);
                return;
            }

            Push(screen);
        }

        /// <summary>Goes back one screen. Returns false when there is nothing to go back to.</summary>
        public bool Back()
        {
            if (Depth == 0 || IsLockedForResult || IsResult(Current)) return false;

            _stack.RemoveAt(_stack.Count - 1);
            CurrentChanged?.Invoke(Current);
            return true;
        }

        /// <summary>Closes every stacked screen, leaving the root.</summary>
        public void CloseAll()
        {
            if (Depth == 0) return;

            _stack.Clear();
            CurrentChanged?.Invoke(Current);
        }

        /// <summary>
        /// Shows a result screen, replacing whatever was open (a run can end while the pause menu is up only in
        /// theory, but the result must still win).
        /// </summary>
        public void ShowResult(MenuScreen screen)
        {
            if (!IsResult(screen)) throw new ArgumentOutOfRangeException(nameof(screen));
            if (Current == screen) return;

            _stack.Clear();
            Push(screen);
        }

        void Push(MenuScreen screen)
        {
            _stack.Add(screen);
            CurrentChanged?.Invoke(screen);
        }
    }
}
