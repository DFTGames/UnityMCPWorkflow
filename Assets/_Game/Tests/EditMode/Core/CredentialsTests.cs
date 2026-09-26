using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    /// <summary>
    /// The pilot name is the account now (GDD "Scoring", Leaderboards), so it has to satisfy Unity
    /// Authentication's rules for a username. These are checked before the game calls the service, so a
    /// mistyped name is answered at once and in the player's words rather than after a round trip, in the
    /// service's.
    /// </summary>
    public class CredentialsTests
    {
        [TestCase("Ace")]
        [TestCase("Red_Baron")]
        [TestCase("a.b-c_d")]
        [TestCase("Pilot99")]
        public void AnOrdinaryName_IsAccepted(string name)
        {
            Assert.That(Credentials.CheckName(name).IsUsable, Is.True, name + " should be usable");
        }

        [Test]
        public void AShortName_IsRefused()
        {
            // The service's minimum, not ours: it would refuse this after the round trip anyway.
            Assert.That(Credentials.CheckName("Al").IsUsable, Is.False);
            Assert.That(Credentials.CheckName("Al").Problem, Does.Contain("3"));
        }

        [Test]
        public void ALongName_IsRefused()
        {
            var name = new string('x', Credentials.MaxNameLength + 1);

            Assert.That(Credentials.CheckName(name).IsUsable, Is.False);
        }

        [Test]
        public void ANameWithNothingInIt_IsRefused()
        {
            // There is no default any more: a name that identifies an account has to be chosen.
            Assert.That(Credentials.CheckName(null).IsUsable, Is.False);
            Assert.That(Credentials.CheckName("   ").IsUsable, Is.False);
        }

        /// <summary>
        /// A space used to be quietly removed, which was right when the name was only a label. Now it is the
        /// account, so silently changing it would hand the player a name they cannot type again.
        /// </summary>
        [TestCase("Red Baron")]
        [TestCase("Ace!")]
        [TestCase("na/me")]
        [TestCase("smile\U0001F600")]
        public void ANameWithCharactersTheServiceRefuses_IsRefusedHere(string name)
        {
            var check = Credentials.CheckName(name);

            Assert.That(check.IsUsable, Is.False, name + " should be refused");
            Assert.That(check.Problem, Is.Not.Empty, "and the player has to be told why");
        }

        /// <summary>
        /// The name is the display name on the boards as well as the account's username. '@' is allowed in a
        /// username but not confirmed for a display name, and a name the boards will not take fails silently:
        /// the run simply never appears under it.
        /// </summary>
        [Test]
        public void AName_DoesNotUseCharactersTheBoardsMayRefuse()
        {
            Assert.That(Credentials.IsAllowedInName('@'), Is.False);
            Assert.That(Credentials.CheckName("a@b").IsUsable, Is.False);
        }

        /// <summary>
        /// A name that passes here still has to fit on a board row, which is the tighter of the two limits.
        /// </summary>
        [Test]
        public void TheNameLimit_IsTheOneTheBoardsAreLaidOutFor()
        {
            Assert.That(Credentials.MaxNameLength, Is.EqualTo(Leaderboards.MaxNameLength));
            Assert.That(Credentials.MinNameLength, Is.LessThan(Credentials.MaxNameLength));
        }
    }
}
