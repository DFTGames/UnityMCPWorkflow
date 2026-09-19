using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    /// <summary>Returns a scripted sequence of values, then repeats the last one.</summary>
    sealed class TestRandom : IRandomSource
    {
        readonly Queue<float> _values;
        readonly float _last;

        public TestRandom(params float[] values)
        {
            _values = new Queue<float>(values);
            _last = values.Length > 0 ? values[values.Length - 1] : 0f;
        }

        public int Calls { get; private set; }

        public float NextFloat()
        {
            Calls++;
            return _values.Count > 0 ? _values.Dequeue() : _last;
        }
    }

    static class TestUtil
    {
        public const float Tolerance = 1e-5f;

        public static void AssertVector(Vector2 expected, Vector2 actual, float tolerance = Tolerance)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(tolerance), "X");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(tolerance), "Y");
        }

        public static float AngleDegrees(Vector2 v) => (float)(System.Math.Atan2(v.Y, v.X) * 180.0 / System.Math.PI);

        /// <summary>Advances a session with no player input.</summary>
        public static void Idle(GameSession session, float deltaTime)
        {
            var commands = new PlayerCommand[session.PlayerCount];
            session.Tick(deltaTime, commands, new List<ShotSpec>());
        }

        /// <summary>Takes the player's lives down to game over, waiting out invulnerability between hits.</summary>
        public static void KillPlayer(GameSession session, int playerIndex)
        {
            while (!session.GetPlayer(playerIndex).IsGameOver)
            {
                session.ReportPlayerHit(playerIndex, 1000f);
                Idle(session, GameTuning.RespawnInvulnerabilitySeconds + 0.01f);
            }
        }
    }
}
