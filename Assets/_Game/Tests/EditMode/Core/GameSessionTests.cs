using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using YASS.Core;
using static YASS.Tests.Core.TestUtil;

namespace YASS.Tests.Core
{
    public class GameSessionTests
    {
        static GameSession Create(params float[] randomValues) =>
            new GameSession(DifficultySettings.Pilot, new TestRandom(randomValues));

        static void BuildChain(GameSession session, int kills)
        {
            for (var i = 0; i < kills; i++) session.ReportKill(0, 0);
        }

        [Test]
        public void NewSession_CreatesPlayersFromDifficulty()
        {
            var session = new GameSession(DifficultySettings.Cadet, new TestRandom(), 2);

            Assert.That(session.PlayerCount, Is.EqualTo(2));
            Assert.That(session.GetPlayer(1).PlayerIndex, Is.EqualTo(1));
            Assert.That(session.GetPlayer(0).Vitals.Lives, Is.EqualTo(5));
            Assert.That(session.IsGameOver, Is.False);
        }

        [Test]
        public void ReportKill_UsesDifficultyScoreMultiplier()
        {
            var session = Create();

            Assert.That(session.ReportKill(0, 100), Is.EqualTo(150));
            Assert.That(session.Score.Score, Is.EqualTo(150));
        }

        [Test]
        public void ReportPlayerHit_Damage_ResetsChain()
        {
            var session = Create();
            BuildChain(session, 3);

            Assert.That(session.ReportPlayerHit(0, 10f), Is.EqualTo(HitOutcome.Damaged));
            Assert.That(session.Score.ChainSteps, Is.EqualTo(0));
        }

        [Test]
        public void ReportPlayerHit_GameOver_ResetsChain()
        {
            var session = new GameSession(DifficultySettings.Ace, new TestRandom());
            session.ReportPlayerHit(0, 1000f);
            Idle(session, GameTuning.RespawnInvulnerabilitySeconds + 0.01f);
            BuildChain(session, 3);

            Assert.That(session.ReportPlayerHit(0, 1000f), Is.EqualTo(HitOutcome.GameOver));
            Assert.That(session.Score.ChainSteps, Is.EqualTo(0));
            Assert.That(session.IsGameOver, Is.True);
        }

        [Test]
        public void ReportPlayerHit_AbsorbedByShield_KeepsChainExactly()
        {
            var session = Create();
            session.ReportPickupCollected(0, PickupType.Shield);
            BuildChain(session, 2);

            Assert.That(session.ReportPlayerHit(0, 10f), Is.EqualTo(HitOutcome.Absorbed));
            Assert.That(session.Score.ChainSteps, Is.EqualTo(1));
        }

        [Test]
        public void ReportPlayerRam_DestroyedEnemy_ScoresAsKill()
        {
            var session = Create();
            session.ReportPickupCollected(0, PickupType.Shield);
            BuildChain(session, 1);

            var result = session.ReportPlayerRam(0, false, 20f, 100);

            Assert.That(result.EnemyDestroyed, Is.True);
            Assert.That(session.Score.Kills, Is.EqualTo(2));
            Assert.That(session.Score.ChainSteps, Is.EqualTo(1));
            Assert.That(session.Score.Score, Is.EqualTo(165)); // 100 x 1.1 x 1.5
        }

        [Test]
        public void ReportPlayerRam_Unshielded_ResetsChainThenScoresKill()
        {
            var session = Create();
            BuildChain(session, 3);

            var result = session.ReportPlayerRam(0, false, 20f, 100);

            Assert.That(result.Outcome, Is.EqualTo(HitOutcome.Damaged));
            Assert.That(session.Score.ChainSteps, Is.EqualTo(0));
            Assert.That(session.Score.Score, Is.EqualTo(150));
        }

        [Test]
        public void ReportPlayerRam_Boss_DoesNotScore()
        {
            var session = Create();

            session.ReportPlayerRam(0, true, 20f, 10000);

            Assert.That(session.Score.Kills, Is.EqualTo(0));
            Assert.That(session.Score.Score, Is.EqualTo(0));
        }

        [Test]
        public void WeaponUpgradePickup_ResetsPityTimer()
        {
            var session = Create();
            Idle(session, GameTuning.WeaponPityDelaySeconds + 5f);

            session.ReportPickupCollected(0, PickupType.WeaponUpgrade);

            Assert.That(session.Drops.TimeSinceWeaponUpgrade, Is.EqualTo(0f));
            Assert.That(session.GetPlayer(0).Weapon.Level, Is.EqualTo(2));
        }

        [Test]
        public void WeaponUpgradeAtMaxLevel_AwardsBonusWithDifficultyMultiplier()
        {
            var session = Create();
            for (var i = 0; i < 4; i++) session.ReportPickupCollected(0, PickupType.WeaponUpgrade);
            Assert.That(session.Score.Score, Is.EqualTo(0));

            session.ReportPickupCollected(0, PickupType.WeaponUpgrade);

            Assert.That(session.Score.Score, Is.EqualTo(750));
        }

        [Test]
        public void TryRollDrop_UsesPlayersWeaponLevelForPity()
        {
            var session = Create(0f, 0f);
            Idle(session, GameTuning.WeaponPityDelaySeconds);

            Assert.That(session.TryRollDrop(DropSource.Enemy, 0, out var pickup), Is.True);
            Assert.That(pickup, Is.EqualTo(PickupType.WeaponUpgrade));
        }

        [Test]
        public void TryRollDrop_ForGameOverPlayer_DropsNothing()
        {
            var session = Create(0f, 0f);
            KillPlayer(session, 0);

            Assert.That(session.TryRollDrop(DropSource.Enemy, 0, out var pickup), Is.False);
            Assert.That(pickup, Is.EqualTo(PickupType.None));
        }

        [Test]
        public void Tick_AdvancesChainTimerAndPlayers()
        {
            var session = Create();
            BuildChain(session, 2);
            session.ReportPickupCollected(0, PickupType.Shield);

            Idle(session, GameTuning.ShieldDurationSeconds);

            Assert.That(session.Score.ChainSteps, Is.EqualTo(0));
            Assert.That(session.GetPlayer(0).Shield.IsActive, Is.False);
        }

        [Test]
        public void Tick_AppliesCommandsPerPlayerAndTagsShots()
        {
            var session = new GameSession(DifficultySettings.Pilot, new TestRandom(), 2);
            var commands = new[]
            {
                new PlayerCommand(Vector2.Zero, false, Vector2.UnitX),
                new PlayerCommand(Vector2.Zero, true, Vector2.UnitY)
            };
            var shots = new List<ShotSpec>();

            session.Tick(0.02f, commands, shots);

            Assert.That(shots.Count, Is.EqualTo(1));
            Assert.That(shots[0].PlayerIndex, Is.EqualTo(1));
            // Aiming straight up fires at the edge of the forward arc, 35 degrees (GDD "Controls", Firing arc).
            Assert.That(AngleDegrees(shots[0].Direction), Is.EqualTo(35f).Within(1e-3f));
        }

        [Test]
        public void Tick_GameOverPlayerDoesNotFire()
        {
            var session = new GameSession(DifficultySettings.Ace, new TestRandom());
            KillPlayer(session, 0);
            var shots = new List<ShotSpec>();

            session.Tick(0.02f, new[] { new PlayerCommand(Vector2.Zero, true, Vector2.UnitX) }, shots);

            Assert.That(shots, Is.Empty);
        }

        [Test]
        public void BossDefeatedWithoutDamage_AwardsBossKillAndBonus()
        {
            var session = Create();
            session.BeginBossFight();
            session.ReportPickupCollected(0, PickupType.Shield);
            session.ReportPlayerHit(0, 10f); // absorbed: not damage

            var points = session.ReportBossDefeated(0, 2);

            Assert.That(points, Is.EqualTo(30000 + 7500)); // (20,000 + 5,000) x 1.5
            Assert.That(session.Score.Kills, Is.EqualTo(1));
        }

        [Test]
        public void BossDefeatedAfterDamage_AwardsOnlyBossKill()
        {
            var session = Create();
            session.BeginBossFight();
            session.ReportPlayerHit(0, 10f);

            Assert.That(session.ReportBossDefeated(0, 1), Is.EqualTo(15000));
        }

        [Test]
        public void DamageBeforeBossFight_DoesNotForfeitBonus()
        {
            var session = Create();
            session.ReportPlayerHit(0, 10f);
            session.BeginBossFight();

            Assert.That(session.ReportBossDefeated(0, 1), Is.EqualTo(15000 + 7500));
        }

        [Test]
        public void BossDefeatedWithoutFightStarted_AwardsNoBonus()
        {
            var session = Create();

            Assert.That(session.ReportBossDefeated(0, 1), Is.EqualTo(15000));
        }

        [Test]
        public void ReportLevelClear_AwardsHealthBonus()
        {
            var session = Create();
            session.ReportPlayerHit(0, 50f);

            Assert.That(session.ReportLevelClear(0), Is.EqualTo(1500)); // 1,000 x 1.5
        }

        [Test]
        public void ReportLevelClear_ForGameOverPlayer_AwardsNothing()
        {
            var session = Create();
            KillPlayer(session, 0);

            Assert.That(session.ReportLevelClear(0), Is.EqualTo(0));
        }

        [Test]
        public void SetEndlessMultiplier_AppliesToKills()
        {
            var session = Create();
            session.SetEndlessMultiplier(1.25f);

            Assert.That(session.ReportKill(0, 100), Is.EqualTo(188)); // 100 x 1.5 x 1.25 = 187.5
        }

        [Test]
        public void IsGameOver_OnlyWhenAllPlayersOut()
        {
            var session = new GameSession(DifficultySettings.Ace, new TestRandom(), 2);

            KillPlayer(session, 0);
            Assert.That(session.IsGameOver, Is.False);

            KillPlayer(session, 1);
            Assert.That(session.IsGameOver, Is.True);
        }

        [Test]
        public void Pickup_AfterGameOver_IsIgnored()
        {
            var session = new GameSession(DifficultySettings.Ace, new TestRandom());
            KillPlayer(session, 0);
            var before = session.Drops.TimeSinceWeaponUpgrade;

            session.ReportPickupCollected(0, PickupType.WeaponUpgrade);

            Assert.That(session.Drops.TimeSinceWeaponUpgrade, Is.EqualTo(before));
            Assert.That(session.GetPlayer(0).Weapon.Level, Is.EqualTo(1));
        }

        [Test]
        public void KillsAfterGameOver_ScoreNothing()
        {
            var session = new GameSession(DifficultySettings.Ace, new TestRandom());
            KillPlayer(session, 0);

            Assert.That(session.ReportKill(0, 100), Is.EqualTo(0));
            Assert.That(session.ReportBossDefeated(0, 1), Is.EqualTo(0));
            Assert.That(session.Score.Score, Is.EqualTo(0));
            Assert.That(session.Score.Kills, Is.EqualTo(0));
        }

        [Test]
        public void LethalRam_DestroysEnemyButDoesNotScoreIt()
        {
            var session = new GameSession(DifficultySettings.Ace, new TestRandom());
            session.ReportPlayerHit(0, 1000f);
            Idle(session, GameTuning.RespawnInvulnerabilitySeconds + 0.01f);

            var result = session.ReportPlayerRam(0, false, 1000f, 100);

            Assert.That(result.Outcome, Is.EqualTo(HitOutcome.GameOver));
            Assert.That(result.EnemyDestroyed, Is.True);
            Assert.That(session.Score.Kills, Is.EqualTo(0));
        }

        [Test]
        public void KillByOneCoopPlayer_StillScoresAfterTheOtherIsOut()
        {
            var session = new GameSession(DifficultySettings.Ace, new TestRandom(), 2);
            KillPlayer(session, 0);

            Assert.That(session.ReportKill(0, 100), Is.EqualTo(0));
            Assert.That(session.ReportKill(1, 100), Is.EqualTo(250));
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void InvalidPlayerIndex_Throws(int index)
        {
            var session = Create();

            Assert.Throws<ArgumentOutOfRangeException>(() => session.GetPlayer(index));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.ReportKill(index, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.ReportPlayerHit(index, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.ReportPlayerRam(index, false, 10f, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.ReportPickupCollected(index, PickupType.Health));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.TryRollDrop(DropSource.Enemy, index, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.ReportBossDefeated(index, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.ReportLevelClear(index));
        }

        [Test]
        public void Tick_RejectsInvalidArguments()
        {
            var session = Create();
            var shots = new List<ShotSpec>();
            var one = new PlayerCommand[1];

            Assert.Throws<ArgumentOutOfRangeException>(() => session.Tick(-1f, one, shots));
            Assert.Throws<ArgumentNullException>(() => session.Tick(0.02f, null, shots));
            Assert.Throws<ArgumentException>(() => session.Tick(0.02f, new PlayerCommand[2], shots));
            Assert.Throws<ArgumentNullException>(() => session.Tick(0.02f, one, null));
        }

        [Test]
        public void Constructor_RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new GameSession(null, new TestRandom()));
            Assert.Throws<ArgumentNullException>(() => new GameSession(DifficultySettings.Pilot, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameSession(DifficultySettings.Pilot, new TestRandom(), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameSession(DifficultySettings.Pilot, new TestRandom(), 5));
        }
    }
}
