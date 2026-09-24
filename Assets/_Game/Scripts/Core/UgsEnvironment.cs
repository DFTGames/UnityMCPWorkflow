namespace YASS.Core
{
    /// <summary>
    /// Which Unity Gaming Services environment a session is allowed to touch, and whether it may talk to the
    /// service at all (GDD "Scoring": the six boards exist in both a production and a test environment).
    /// </summary>
    /// <remarks>
    /// The rule exists because a test run once wrote its own scores onto the live boards, and nothing about
    /// that looked wrong until somebody read the dashboard. Every score a player never earned has to be
    /// deleted by hand, so this is not a tidiness question.
    ///
    /// **A build plays against production; anything in the editor plays against test.** A test run only ever
    /// happens in the editor, including a headless batchmode one, so the environment a test can reach is not
    /// a matter of each fixture remembering to install fakes. It also means playing the game in the editor
    /// cannot put a practice score on a real board.
    ///
    /// The services have no public way to read back the environment they were started with (it lives on an
    /// internal type), so certainty has to come from owning the start: whoever begins a session records what
    /// they began it with, and work is refused when that record is missing or wrong. Refused, not assumed:
    /// "I do not know which environment this is" and "this is the right one" must never be the same answer.
    /// </remarks>
    public static class UgsEnvironment
    {
        /// <summary>The live boards, which real players' scores belong to.</summary>
        public const string Production = "production";

        /// <summary>The same six boards, set to keep the latest score so a test overwrites its own.</summary>
        public const string Test = "test";

        /// <summary>The only environment this session is allowed to touch.</summary>
        public static string For(bool inEditor) => inEditor ? Test : Production;

        /// <summary>
        /// Whether a session started against <paramref name="startedWith"/> may talk to the service. An empty
        /// record means somebody else started the services and the environment is unknown, which is a refusal.
        /// </summary>
        public static bool MayTalkToTheService(string startedWith, bool inEditor, out string problem)
        {
            var wanted = For(inEditor);

            if (string.IsNullOrEmpty(startedWith))
            {
                problem = "the services were started by something that did not record the environment, so " +
                          $"this session cannot show it is on '{wanted}'";
                return false;
            }

            if (startedWith != wanted)
            {
                problem = $"the services are on '{startedWith}' and this session may only touch '{wanted}'";
                return false;
            }

            problem = null;
            return true;
        }
    }
}
