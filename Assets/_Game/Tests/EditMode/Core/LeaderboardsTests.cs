using System;
using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    /// <summary>
    /// The part of the leaderboards that can be decided without a network (GDD "Scoring", Leaderboards): which
    /// board a run belongs to, what may be written on it, and what is not worth sending.
    /// </summary>
    public class LeaderboardsTests
    {
        [Test]
        public void EveryModeAndDifficulty_HasItsOwnBoard()
        {
            // Six boards, and no two runs sharing one: a Cadet campaign score on the Ace board would make
            // both meaningless.
            var ids = Leaderboards.AllIds();

            Assert.That(ids.Length, Is.EqualTo(Leaderboards.Count));
            Assert.That(ids, Is.Unique);
        }

        [Test]
        public void ABoardId_NamesItsModeAndDifficulty()
        {
            // Typed into the service's dashboard by hand, so it has to be readable and unambiguous.
            Assert.That(Leaderboards.IdFor(GameMode.Campaign, Difficulty.Pilot), Is.EqualTo("campaign_pilot"));
            Assert.That(Leaderboards.IdFor(GameMode.Endless, Difficulty.Ace), Is.EqualTo("endless_ace"));
        }

        [Test]
        public void BoardIds_AreTheSameEveryTime()
        {
            // These are the keys the scores already on the service are filed under: if this test ever has to
            // change, every score on the old id is orphaned.
            Assert.That(Leaderboards.AllIds(), Is.EqualTo(new[]
            {
                "campaign_cadet", "campaign_pilot", "campaign_ace",
                "endless_cadet", "endless_pilot", "endless_ace"
            }));
        }

        [Test]
        public void AName_IsTrimmedAndCutToLength()
        {
            Assert.That(Leaderboards.CleanName("  Ace  "), Is.EqualTo("Ace"));
            Assert.That(Leaderboards.CleanName(new string('x', Leaderboards.MaxNameLength + 20)),
                Has.Length.EqualTo(Leaderboards.MaxNameLength));
        }

        /// <summary>
        /// The limit is the service's, not the width of a board row. It was 12 for a while, measured off the
        /// row, which meant the game refused and truncated names the service had itself generated:
        /// SeriousForgottenSnowflake is 25 characters. The row shortens nothing; its label is 420 px of 28 pt
        /// with overflow Ellipsis, which holds about 29 characters and cuts the rest at the right pixel.
        /// </summary>
        [Test]
        public void TheLimit_IsTheServiceOwn_NotTheWidthOfABoardRow()
        {
            Assert.That(Leaderboards.MaxNameLength, Is.EqualTo(50), "Unity Authentication's documented limit");

            const string generated = "SeriousForgottenSnowflake";
            Assert.That(generated.Length, Is.LessThanOrEqualTo(Leaderboards.MaxNameLength));
            Assert.That(Leaderboards.CleanName(generated), Is.EqualTo(generated),
                "a name the service generated must survive the game's own cleaning unchanged");
        }

        [Test]
        public void AnEmptyName_BecomesTheDefault()
        {
            // Nobody is nameless on a public board, and a blank row looks like a bug in the game.
            Assert.That(Leaderboards.CleanName(null), Is.EqualTo(Leaderboards.DefaultName));
            Assert.That(Leaderboards.CleanName("   "), Is.EqualTo(Leaderboards.DefaultName));
            Assert.That(Leaderboards.CleanName("\t\n"), Is.EqualTo(Leaderboards.DefaultName));
        }

        [Test]
        public void AName_ThatIsOnlySpacesOnceCut_BecomesTheDefault()
        {
            // A run of spaces ahead of the name: cutting from the front would leave blanks, not a name.
            var name = new string(' ', Leaderboards.MaxNameLength) + "Ace";

            Assert.That(Leaderboards.CleanName(name), Is.EqualTo("Ace"), "the leading spaces are trimmed first");
        }

        /// <summary>
        /// Not a nicety: Unity Authentication refuses a player name containing any whitespace, and that
        /// refusal used to arrive in the middle of submitting a score and take the score with it. "Red
        /// Baron" meant a run that never reached a board, looking exactly like having no network.
        /// </summary>
        [Test]
        public void AName_KeepsNoSpacesAtAll()
        {
            Assert.That(Leaderboards.CleanName("Red Baron"), Is.EqualTo("RedBaron"));
            Assert.That(Leaderboards.CleanName("a b\tc\nd"), Is.EqualTo("abcd"));
            Assert.That(Leaderboards.CleanName(" The  Ace "), Is.EqualTo("TheAce"));
        }

        [Test]
        public void AName_IsCutByWhatIsLeftAfterTheSpacesGo()
        {
            // The limit is on the name that goes up, so spaces must not eat into the player's allowance.
            Assert.That(Leaderboards.CleanName("A B C D E F G H"), Is.EqualTo("ABCDEFGH"));
            Assert.That(Leaderboards.CleanName(new string(' ', 30) + new string('x', Leaderboards.MaxNameLength + 20)),
                Has.Length.EqualTo(Leaderboards.MaxNameLength));
        }

        [Test]
        public void ARunWorthNothing_IsNotSent()
        {
            // A board full of zeroes buries the runs that meant something.
            Assert.That(Leaderboards.WorthSubmitting(0), Is.False);
            Assert.That(Leaderboards.WorthSubmitting(-10), Is.False);
            Assert.That(Leaderboards.WorthSubmitting(1), Is.True);
        }

        [Test]
        public void AnEntry_KnowsWhereItCame()
        {
            var entry = new LeaderboardEntry(3, "Ace", 1200, isYou: true);

            Assert.That(entry.Rank, Is.EqualTo(3));
            Assert.That(entry.Score, Is.EqualTo(1200));
            Assert.That(entry.IsYou, Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => new LeaderboardEntry(0, "Ace", 1));
        }

        [Test]
        public void AFailedRequest_CarriesNoEntriesRatherThanNull()
        {
            // The UI walks the entries without checking: a failure shows an empty board, not an exception.
            var result = LeaderboardResult.Failure("no network");

            Assert.That(result.Status, Is.EqualTo(LeaderboardStatus.Failed));
            Assert.That(result.Entries, Is.Not.Null.And.Empty);
            Assert.That(result.Problem, Is.EqualTo("no network"));
        }
    }
}
