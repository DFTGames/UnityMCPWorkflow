using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UI;
using YASS.Core;
using YASS.UI;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// The leaderboard screens are wired by hand in the scenes, and a missing reference there does not fail:
    /// the panel appears, lays itself out, and does nothing at all (GDD "Scoring", Leaderboards). These tests
    /// are the only thing that can tell the difference without a person looking at the running game.
    /// </summary>
    public class LeaderboardScreenTests
    {
        [Test]
        public void TheTitle_HasABoardScreen_WithEveryPartConnected()
        {
            ScenePeek.In("Title", scene =>
            {
                var menu = ScenePeek.Find<LeaderboardMenu>(scene);
                Assert.That(menu, Is.Not.Null, "the title has no leaderboard screen, so the boards are unreachable");

                ScenePeek.RequireReference(menu, "router", "Back would do nothing");
                ScenePeek.RequireReference(menu, "headingText", "the player could not tell which board they are on");
                ScenePeek.RequireReference(menu, "messageText", "a board that cannot load would say nothing");
                ScenePeek.RequireReference(menu, "modeLabel", "the mode button would not say which mode it is on");
                ScenePeek.RequireReference(menu, "difficultyLabel", "the difficulty button would not say which it is on");
            });
        }

        /// <summary>
        /// A board with fewer rows than places shows a shorter board and never says so; one with an empty
        /// element throws the moment it is filled.
        /// </summary>
        [Test]
        public void TheBoardScreen_HasARowForEveryPlace()
        {
            ScenePeek.In("Title", scene =>
            {
                var menu = ScenePeek.Find<LeaderboardMenu>(scene);
                var rows = new SerializedObject(menu).FindProperty("rows");

                Assert.That(rows.arraySize, Is.EqualTo(LeaderboardMenu.Places),
                    "the screen shows " + LeaderboardMenu.Places + " places, so it needs that many rows");

                for (var i = 0; i < rows.arraySize; i++)
                    Assert.That(rows.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null,
                        "row " + (i + 1) + " is empty");
            });
        }

        /// <summary>A row missing one of its three labels shows a place with no name, or no score.</summary>
        [Test]
        public void EveryRow_CanShowAPlaceAName_AndAScore()
        {
            ScenePeek.In("Title", scene =>
            {
                var rows = ScenePeek.FindAll<LeaderboardRow>(scene);
                Assert.That(rows, Is.Not.Empty, "the board screen has no rows");

                foreach (var row in rows)
                {
                    ScenePeek.RequireReference(row, "rankText", "the place would be blank");
                    ScenePeek.RequireReference(row, "nameText", "the name would be blank");
                    ScenePeek.RequireReference(row, "scoreText", "the score would be blank");
                }
            });
        }

        [Test]
        public void TheTitle_AsksForAName_BeforeTheFirstRun()
        {
            ScenePeek.In("Title", scene =>
            {
                var menu = ScenePeek.Find<NameEntryMenu>(scene);
                Assert.That(menu, Is.Not.Null,
                    "nothing asks the player their name, so every board entry would read 'Pilot'");

                ScenePeek.RequireReference(menu, "router", "the screen could never be left");
                ScenePeek.RequireReference(menu, "field", "there would be nothing to type into");
                ScenePeek.RequireReference(menu, "passwordField",
                    "every attempt would be refused with 'Choose a password.'");
                ScenePeek.RequireReference(menu, "messageText",
                    "every failure would be silent, and the screen would look broken rather than refused");
            });
        }

        /// <summary>
        /// A panel the router does not know about can never be opened, however well it is built. This is the
        /// mistake that loses a whole screen, so it is checked for every screen in the scene, not just the
        /// new ones.
        /// </summary>
        [TestCase("Title")]
        [TestCase("Level")]
        public void TheRouter_KnowsEveryScreenInTheScene(string sceneName)
        {
            ScenePeek.In(sceneName, scene =>
            {
                var router = ScenePeek.Require<MenuRouter>(scene, sceneName, "no menu could be shown");
                var listed = new SerializedObject(router).FindProperty("screens");

                var known = new HashSet<MenuScreenView>();
                var screens = new HashSet<MenuScreen>();
                for (var i = 0; i < listed.arraySize; i++)
                {
                    var view = listed.GetArrayElementAtIndex(i).objectReferenceValue as MenuScreenView;
                    Assert.That(view, Is.Not.Null, sceneName + "'s router has an empty entry at " + i);
                    Assert.That(screens.Add(view.Screen), Is.True,
                        sceneName + " has two panels claiming to be " + view.Screen + ", so one can never show");
                    known.Add(view);
                }

                foreach (var view in ScenePeek.FindAll<MenuScreenView>(scene))
                    Assert.That(known.Contains(view), Is.True,
                        $"{sceneName}'s '{view.name}' ({view.Screen}) is not in the router's list, so nothing can open it");
            });
        }

        /// <summary>
        /// Every button on the new screens, checked through the call it is wired to rather than through a
        /// serialised reference to it. A button with the right label, in the right place, and an empty
        /// onClick is exactly what lost wiring looks like, and it is what a reference check cannot see.
        /// </summary>
        [TestCase(nameof(TitleMenu.OpenLeaderboards), "the boards would be unreachable")]
        [TestCase(nameof(LeaderboardMenu.NextMode), "the mode could not be changed")]
        [TestCase(nameof(LeaderboardMenu.NextDifficulty), "the difficulty could not be changed")]
        [TestCase(nameof(NameEntryMenu.CreateAccount), "nobody could ever make an account")]
        [TestCase(nameof(NameEntryMenu.SignIn), "a returning player could not sign in")]
        [TestCase(nameof(NameEntryMenu.PlayOffline), "the account screen would have no way past it")]
        public void TheTitle_HasAButton_ThatCalls(string method, string otherwise)
        {
            ScenePeek.In("Title", scene =>
            {
                var callers = 0;
                foreach (var button in ScenePeek.FindAll<Button>(scene))
                    for (var i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                        if (button.onClick.GetPersistentMethodName(i) == method)
                        {
                            Assert.That(button.onClick.GetPersistentTarget(i), Is.Not.Null,
                                "the button calling " + method + " points at nothing");
                            callers++;
                        }

                Assert.That(callers, Is.EqualTo(1), $"no button calls {method}, so {otherwise}");
            });
        }

        /// <summary>The name is edited in Settings once it has been chosen, in a level as well as the title.</summary>
        [TestCase("Title")]
        [TestCase("Level")]
        public void EveryPlayableScene_LetsThePilotChangeTheirName(string sceneName)
        {
            ScenePeek.In(sceneName, scene =>
            {
                var menu = ScenePeek.Find<SettingsMenu>(scene);
                Assert.That(menu, Is.Not.Null, sceneName + " has no settings screen");
                ScenePeek.RequireReference(menu, "playerName", "the name could never be changed");
            });
        }

        /// <summary>
        /// The two panels a run can end on are the two that report a placing (GDD "Core Loop": a score is
        /// submitted at victory or game over). Sector Clear is deliberately not one of them: the run carries on.
        /// </summary>
        [Test]
        public void ThePanelsThatEndARun_CanShowItsPlacing()
        {
            ScenePeek.In("Level", scene =>
            {
                var flow = ScenePeek.Require<LevelFlow>(scene, "Level", "no results would ever appear");
                var serialised = new SerializedObject(flow);

                foreach (var which in new[] { "gameOver", "victory" })
                {
                    var panel = serialised.FindProperty(which).objectReferenceValue as ResultsMenu;
                    Assert.That(panel, Is.Not.Null, "LevelFlow has no " + which + " panel");
                    ScenePeek.RequireReference(panel, "placingText",
                        "a submitted run would never show where it came");
                }
            });
        }
    }
}
