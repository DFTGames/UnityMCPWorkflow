using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    /// <summary>
    /// Which Unity Gaming Services environment a session may touch (GDD "Scoring").
    /// </summary>
    /// <remarks>
    /// Worth testing for its own sake rather than through anything that talks to the network: the whole
    /// point is that the wrong answer is invisible. A run against the live boards looks exactly like a run
    /// against the test ones, right up until somebody reads the dashboard and finds scores nobody earned.
    /// </remarks>
    public class UgsEnvironmentTests
    {
        [Test]
        public void ABuild_PlaysAgainstProduction() =>
            Assert.That(UgsEnvironment.For(false), Is.EqualTo(UgsEnvironment.Production));

        /// <summary>A test run only ever happens in the editor, batchmode included.</summary>
        [Test]
        public void TheEditor_PlaysAgainstTest() =>
            Assert.That(UgsEnvironment.For(true), Is.EqualTo(UgsEnvironment.Test));

        [Test]
        public void AnUnrecordedEnvironment_IsRefused()
        {
            Assert.That(UgsEnvironment.MayTalkToTheService(null, true, out var problem), Is.False,
                "not knowing which environment this is must never read as the right one");
            Assert.That(problem, Is.Not.Null.And.Not.Empty, "and it has to say why");

            Assert.That(UgsEnvironment.MayTalkToTheService(string.Empty, false, out _), Is.False);
        }

        [Test]
        public void TheEditorOnProduction_IsRefused()
        {
            Assert.That(UgsEnvironment.MayTalkToTheService(UgsEnvironment.Production, true, out var problem),
                Is.False, "a practice run in the editor must not reach the live boards");
            Assert.That(problem, Does.Contain(UgsEnvironment.Production).And.Contain(UgsEnvironment.Test),
                "the message should name both what it is on and what it may touch");
        }

        /// <summary>The other way round too: a shipped game's scores belong on the live boards.</summary>
        [Test]
        public void ABuildOnTest_IsRefused() =>
            Assert.That(UgsEnvironment.MayTalkToTheService(UgsEnvironment.Test, false, out _), Is.False);

        [Test]
        public void EachOnItsOwnEnvironment_IsAllowed()
        {
            Assert.That(UgsEnvironment.MayTalkToTheService(UgsEnvironment.Test, true, out var inEditor), Is.True);
            Assert.That(inEditor, Is.Null);

            Assert.That(UgsEnvironment.MayTalkToTheService(UgsEnvironment.Production, false, out _), Is.True);
        }
    }
}
