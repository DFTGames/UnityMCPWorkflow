using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
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

        /// <summary>
        /// A row must be able to draw a name the service generated. Nothing in code shortens a name any more:
        /// the row hands the whole thing to the label, and the label's own Ellipsis overflow cuts it at the
        /// pixel where it stops fitting. That puts the entire rule in the scene, where a narrowed label or an
        /// overflow mode changed to Overflow or Truncate would spill over the score, or lose the ellipsis that
        /// says a name was shortened, with nothing to fail. This is that something.
        /// </summary>
        [Test]
        public void EveryRow_CanDrawAServiceGeneratedName()
        {
            // 25 characters, and real: the name Unity Authentication handed this project's test account.
            // A row that cannot hold this cannot hold what most players will never have changed.
            const string generated = "SeriousForgottenSnowflake";

            ScenePeek.In("Title", scene =>
            {
                foreach (var row in ScenePeek.FindAll<LeaderboardRow>(scene))
                {
                    var label = (TMP_Text)new SerializedObject(row).FindProperty("nameText").objectReferenceValue;
                    var width = ((RectTransform)label.transform).rect.width;

                    Assert.That(label.GetPreferredValues(generated, 0f, 0f).x, Is.LessThanOrEqualTo(width),
                        row.name + "'s name label is too narrow for a name the service generated itself");

                    Assert.That(label.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis),
                        row.name + "'s name label must shorten a longer name rather than spill or cut it dead");
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
                ScenePeek.RequireReference(menu, "messageText",
                    "every failure would be silent, and the screen would look broken rather than refused");
            });
        }

        /// <summary>
        /// The account screen's serialised references. A missing one is silent by design here: every setter
        /// is null-guarded, so a lost button reference looks exactly like a button the screen has correctly
        /// hidden, and a lost router makes Back do nothing at all.
        /// </summary>
        [Test]
        public void TheTitle_HasAFullyWiredAccountScreen()
        {
            ScenePeek.In("Title", scene =>
            {
                var menu = ScenePeek.Find<AccountMenu>(scene);
                Assert.That(menu, Is.Not.Null, "there is no account screen, so nobody could ever sign out");

                ScenePeek.RequireReference(menu, "router", "Back would do nothing");
                ScenePeek.RequireReference(menu, "summaryText", "nobody would be told who is signed in");
                ScenePeek.RequireReference(menu, "messageText", "every refusal would be silent");
                ScenePeek.RequireReference(menu, "signInButton", "a signed-out player could not sign in");
                ScenePeek.RequireReference(menu, "signOutButton", "nobody could sign out");
                ScenePeek.RequireReference(menu, "switchButton", "nobody could hand the game to someone else");
                ScenePeek.RequireReference(menu, "manageButton", "a forgotten password could not be reset");
            });
        }

        /// <summary>The way through to it, and the line that explains a refused name.</summary>
        [Test]
        public void TheTitle_SettingsCanReachTheAccountScreen()
        {
            ScenePeek.In("Title", scene =>
            {
                var settings = ScenePeek.Find<SettingsMenu>(scene);
                ScenePeek.RequireReference(settings, "router", "the account screen would be unreachable");
                ScenePeek.RequireReference(settings, "accountRow", "the row could not hide itself in a level");
                ScenePeek.RequireReference(settings, "nameProblem",
                    "a refused pilot name would be rejected in silence");
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
        [TestCase(typeof(TitleMenu), nameof(TitleMenu.OpenLeaderboards), "the boards would be unreachable")]
        [TestCase(typeof(LeaderboardMenu), nameof(LeaderboardMenu.NextMode), "the mode could not be changed")]
        [TestCase(typeof(LeaderboardMenu), nameof(LeaderboardMenu.NextDifficulty), "the difficulty could not be changed")]
        [TestCase(typeof(NameEntryMenu), nameof(NameEntryMenu.SignIn), "nobody could sign in before their first run")]
        [TestCase(typeof(NameEntryMenu), nameof(NameEntryMenu.PlayOffline), "the first-run screen would have no way past it")]
        [TestCase(typeof(SettingsMenu), nameof(SettingsMenu.OpenAccount), "the account screen would be unreachable")]
        [TestCase(typeof(AccountMenu), nameof(AccountMenu.SignIn), "a signed-out player could never sign in")]
        [TestCase(typeof(AccountMenu), nameof(AccountMenu.SignOut), "nobody could sign out, so a shared machine keeps the first player")]
        [TestCase(typeof(AccountMenu), nameof(AccountMenu.SwitchAccount), "nobody could hand the game to someone else")]
        [TestCase(typeof(AccountMenu), nameof(AccountMenu.ManageAccount), "a forgotten password could not be reset")]
        public void TheTitle_HasAButton_ThatCalls(System.Type target, string method, string otherwise)
        {
            ScenePeek.In("Title", scene =>
            {
                var callers = 0;
                foreach (var button in ScenePeek.FindAll<Button>(scene))
                    for (var i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    {
                        if (button.onClick.GetPersistentMethodName(i) != method) continue;

                        // The target as well as the name: two screens can both have a SignIn, and a button
                        // wired to the wrong one of them is exactly the mistake this is here to catch.
                        var called = button.onClick.GetPersistentTarget(i);
                        Assert.That(called, Is.Not.Null,
                            $"the button calling {method} points at nothing");

                        if (called.GetType() == target) callers++;
                    }

                Assert.That(callers, Is.EqualTo(1),
                    $"expected exactly one button calling {target.Name}.{method}, or {otherwise}");
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
