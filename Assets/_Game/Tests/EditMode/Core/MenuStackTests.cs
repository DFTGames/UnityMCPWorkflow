using System;
using System.Collections.Generic;
using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    public class MenuStackTests
    {
        [Test]
        public void NewStack_ShowsItsRoot()
        {
            Assert.That(new MenuStack(MenuScreen.Title).Current, Is.EqualTo(MenuScreen.Title));
            Assert.That(new MenuStack().Current, Is.EqualTo(MenuScreen.None), "in a level, no menu is showing");
        }

        [Test]
        public void Open_ShowsTheScreen()
        {
            var stack = new MenuStack(MenuScreen.Title);

            stack.Open(MenuScreen.Difficulty);

            Assert.That(stack.Current, Is.EqualTo(MenuScreen.Difficulty));
            Assert.That(stack.Depth, Is.EqualTo(1));
        }

        [Test]
        public void Back_ReturnsToWhereTheScreenWasOpenedFrom()
        {
            // The point of a stack: Settings is reachable from the Title and from Pause (GDD "UI Flow").
            var fromTitle = new MenuStack(MenuScreen.Title);
            fromTitle.Open(MenuScreen.Settings);
            Assert.That(fromTitle.Back(), Is.True);
            Assert.That(fromTitle.Current, Is.EqualTo(MenuScreen.Title));

            var inLevel = new MenuStack();
            inLevel.Open(MenuScreen.Pause);
            inLevel.Open(MenuScreen.Settings);
            Assert.That(inLevel.Back(), Is.True);
            Assert.That(inLevel.Current, Is.EqualTo(MenuScreen.Pause), "back from Settings returns to Pause, not to the game");
        }

        [Test]
        public void Back_AtTheRoot_DoesNothing()
        {
            var stack = new MenuStack(MenuScreen.Title);

            Assert.That(stack.Back(), Is.False);
            Assert.That(stack.Current, Is.EqualTo(MenuScreen.Title));
        }

        [Test]
        public void Back_FromPause_ResumesTheGame()
        {
            var stack = new MenuStack();
            stack.Open(MenuScreen.Pause);

            Assert.That(stack.Back(), Is.True);
            Assert.That(stack.Current, Is.EqualTo(MenuScreen.None));
            Assert.That(stack.PausesGame, Is.False);
        }

        [Test]
        public void PausesGame_WhileAnyMenuCoversTheLevel()
        {
            var stack = new MenuStack();
            Assert.That(stack.PausesGame, Is.False);

            stack.Open(MenuScreen.Pause);
            Assert.That(stack.PausesGame, Is.True);

            stack.Open(MenuScreen.Settings);
            Assert.That(stack.PausesGame, Is.True, "settings opened from pause keeps the game stopped");
        }

        [Test]
        public void PausesGame_IsFalseInTheTitleScene()
        {
            var stack = new MenuStack(MenuScreen.Title);
            stack.Open(MenuScreen.Settings);

            Assert.That(stack.PausesGame, Is.False, "there is no game to pause in the menus");
        }

        [Test]
        public void ResultScreens_DoNotPauseTheGame()
        {
            // The run is over; ships level off and engines idle while the panel is up.
            var stack = new MenuStack();
            stack.ShowResult(MenuScreen.GameOver);

            Assert.That(stack.PausesGame, Is.False);
        }

        [Test]
        public void ShowResult_ReplacesWhateverWasOpen()
        {
            var stack = new MenuStack();
            stack.Open(MenuScreen.Pause);
            stack.Open(MenuScreen.Settings);

            stack.ShowResult(MenuScreen.SectorClear);

            Assert.That(stack.Current, Is.EqualTo(MenuScreen.SectorClear));
            Assert.That(stack.Depth, Is.EqualTo(1));
        }

        [Test]
        public void ResultScreen_CannotBeLeftByGoingBack()
        {
            var stack = new MenuStack();
            stack.ShowResult(MenuScreen.GameOver);

            Assert.That(stack.Back(), Is.False);
            Assert.That(stack.Current, Is.EqualTo(MenuScreen.GameOver), "you leave Game Over by choosing Retry or Title");
        }

        [Test]
        public void ResultScreen_CannotBeCoveredByAnotherScreen()
        {
            var stack = new MenuStack();
            stack.ShowResult(MenuScreen.GameOver);

            stack.Open(MenuScreen.Pause);

            Assert.That(stack.Current, Is.EqualTo(MenuScreen.GameOver), "pausing a finished run does nothing");
        }

        [Test]
        public void LockForResult_StopsThePauseMenuOpeningOverAFinishedRun()
        {
            // The run ends a moment before its results panel appears; pausing in that gap would offer to resume
            // a run that is over, and would stop the delay that shows the panel.
            var stack = new MenuStack();
            stack.LockForResult();

            stack.Open(MenuScreen.Pause);

            Assert.That(stack.Current, Is.EqualTo(MenuScreen.None));
            Assert.That(stack.PausesGame, Is.False);
            Assert.That(stack.IsLockedForResult, Is.True);
        }

        [Test]
        public void LockForResult_ClosesWhateverWasAlreadyOpen()
        {
            var stack = new MenuStack();
            stack.Open(MenuScreen.Pause);

            stack.LockForResult();

            Assert.That(stack.Current, Is.EqualTo(MenuScreen.None), "the pause menu belongs to a run that is over");
            Assert.That(stack.Back(), Is.False);
        }

        [Test]
        public void LockForResult_StillAllowsTheResultItself()
        {
            var stack = new MenuStack();
            stack.LockForResult();

            stack.ShowResult(MenuScreen.GameOver);

            Assert.That(stack.Current, Is.EqualTo(MenuScreen.GameOver));
        }

        [Test]
        public void LockForResult_IsIdempotent()
        {
            var stack = new MenuStack();
            var events = 0;
            stack.CurrentChanged += _ => events++;

            stack.LockForResult();
            stack.LockForResult();

            Assert.That(events, Is.Zero, "nothing was open, so nothing changed");
        }

        [Test]
        public void CloseAll_ReturnsToTheRoot()
        {
            var stack = new MenuStack();
            stack.Open(MenuScreen.Pause);
            stack.Open(MenuScreen.Settings);

            stack.CloseAll();

            Assert.That(stack.Current, Is.EqualTo(MenuScreen.None));
            Assert.That(stack.Depth, Is.Zero);
        }

        [Test]
        public void Open_TheSameScreenTwice_DoesNotStackIt()
        {
            var stack = new MenuStack(MenuScreen.Title);
            stack.Open(MenuScreen.Settings);

            stack.Open(MenuScreen.Settings);

            Assert.That(stack.Depth, Is.EqualTo(1));
            Assert.That(stack.Back(), Is.True);
            Assert.That(stack.Current, Is.EqualTo(MenuScreen.Title), "one back is enough to leave it");
        }

        [Test]
        public void CurrentChanged_ReportsEveryTransition()
        {
            var stack = new MenuStack(MenuScreen.Title);
            var seen = new List<MenuScreen>();
            stack.CurrentChanged += s => seen.Add(s);

            stack.Open(MenuScreen.Settings);
            stack.Back();
            stack.Open(MenuScreen.Credits);
            stack.CloseAll();

            Assert.That(seen, Is.EqualTo(new[]
            {
                MenuScreen.Settings, MenuScreen.Title, MenuScreen.Credits, MenuScreen.Title
            }));
        }

        [Test]
        public void CurrentChanged_IsSilentWhenNothingChanges()
        {
            var stack = new MenuStack(MenuScreen.Title);
            var events = 0;
            stack.CurrentChanged += _ => events++;

            stack.Back();    // already at the root
            stack.CloseAll(); // nothing stacked

            Assert.That(events, Is.Zero);
        }

        [Test]
        public void Open_RejectsNone()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MenuStack(MenuScreen.Title).Open(MenuScreen.None));
        }

        [Test]
        public void ShowResult_RejectsANonResultScreen()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MenuStack().ShowResult(MenuScreen.Pause));
        }

        /// <summary>
        /// The name screen is answered, not visited: after it opens the difficulty screen, back belongs to
        /// the title. Opening rather than replacing put the player back in front of the question they had
        /// just answered, with the box taking their keyboard again.
        /// </summary>
        [Test]
        public void AReplacedScreen_IsNotWhatBackReturnsTo()
        {
            var stack = new MenuStack(MenuScreen.Title);
            stack.Open(MenuScreen.NameEntry);
            stack.Replace(MenuScreen.Difficulty);

            Assert.That(stack.Current, Is.EqualTo(MenuScreen.Difficulty));
            Assert.That(stack.Depth, Is.EqualTo(1), "the screen it replaced is gone, not buried");

            Assert.That(stack.Back(), Is.True);
            Assert.That(stack.Current, Is.EqualTo(MenuScreen.Title), "back skips the screen already answered");
        }

        [Test]
        public void ReplacingFromTheRoot_JustOpens()
        {
            var stack = new MenuStack(MenuScreen.Title);
            stack.Replace(MenuScreen.Difficulty);

            Assert.That(stack.Current, Is.EqualTo(MenuScreen.Difficulty));
            Assert.That(stack.Depth, Is.EqualTo(1));
        }

        [Test]
        public void ReplacingAfterTheRunHasEnded_DoesNothing()
        {
            var stack = new MenuStack();
            stack.ShowResult(MenuScreen.GameOver);
            stack.Replace(MenuScreen.Difficulty);

            Assert.That(stack.Current, Is.EqualTo(MenuScreen.GameOver), "nothing opens over a result");
        }

        /// <summary>
        /// A player who alt-tabs, takes a call or switches app has stopped playing, and a ship that dies
        /// while they are away is not a fair death. The same rule on every platform.
        /// </summary>
        [Test]
        public void LosingFocus_PausesTheGame()
        {
            Assert.That(MenuStack.ShouldPauseOnFocusLoss(hasFocus: false, inEditor: false), Is.True);
        }

        [Test]
        public void KeepingFocus_DoesNot()
        {
            Assert.That(MenuStack.ShouldPauseOnFocusLoss(hasFocus: true, inEditor: false), Is.False);
        }

        /// <summary>
        /// Never in the editor. Focus there belongs to the Console and the Inspector in turn, and an
        /// automated PlayMode run never has it at all: without this carve-out every test in the suite would
        /// pause the game and then time out waiting for a game that is not running.
        /// </summary>
        [Test]
        public void TheEditor_NeverPausesOnFocus()
        {
            Assert.That(MenuStack.ShouldPauseOnFocusLoss(hasFocus: false, inEditor: true), Is.False);
            Assert.That(MenuStack.ShouldPauseOnFocusLoss(hasFocus: true, inEditor: true), Is.False);
        }
    }
}
