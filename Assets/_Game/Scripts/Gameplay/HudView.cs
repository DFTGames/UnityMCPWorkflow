using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YASS.Core;

namespace YASS.Gameplay
{
    /// <summary>
    /// In-level HUD for one player: score, lives, health bar, weapon level, chain and kills, plus the boss
    /// warning and boss health bar. The results panels belong to the flow layer (YASS.UI). Text is only
    /// rewritten when a value changes, through a reusable char buffer, so it does not allocate.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        const string LivesPrefix = "Lives ";
        const string WeaponPrefix = "Weapon Lv ";
        const string KillsPrefix = "Kills ";
        const string CyclePrefix = "Cycle ";
        const float WarningBlinksPerSecond = 3f;

        [SerializeField, Min(0)] int playerIndex;
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text livesText;
        [SerializeField] TMP_Text weaponText;
        [SerializeField] TMP_Text chainText;
        [SerializeField] TMP_Text killsText;
        [SerializeField] Image healthFill;

        [SerializeField, Tooltip("Endless only: which cycle the run is on. Hidden in the campaign.")]
        TMP_Text cycleText;

        [Header("Boss")]
        [SerializeField] TMP_Text bossWarning;
        [SerializeField] GameObject bossBar;
        [SerializeField] Image bossHealthFill;

        readonly char[] _buffer = new char[48];

        long? _score;
        int? _lives;
        int? _weapon;
        int? _chainSteps;
        int? _kills;
        int? _cycle;
        float? _health;
        float? _bossHealth;
        bool? _bossBarShown;
        bool? _warningShown;

        public void Refresh(GameSession session, LevelHudState level)
        {
            var player = session.GetPlayer(playerIndex);
            var score = session.Score;

            if (score.Score != _score) SetNumber(scoreText, "", (_score = score.Score).Value);
            if (player.Vitals.Lives != _lives) SetNumber(livesText, LivesPrefix, (_lives = player.Vitals.Lives).Value);
            if (player.Weapon.Level != _weapon) SetNumber(weaponText, WeaponPrefix, (_weapon = player.Weapon.Level).Value);
            if (score.Kills != _kills) SetNumber(killsText, KillsPrefix, (_kills = score.Kills).Value);

            if (score.ChainSteps != _chainSteps)
            {
                _chainSteps = score.ChainSteps;
                chainText.SetCharArray(_buffer, 0, NumberFormatter.WriteMultiplier(score.ChainMultiplier, _buffer));
            }

            // Endless counts its cycles where the campaign has nothing to say; the label is simply absent there.
            if (cycleText != null && level.Cycle != _cycle)
            {
                _cycle = level.Cycle;
                cycleText.gameObject.SetActive(level.Cycle > 0);
                if (level.Cycle > 0) SetNumber(cycleText, CyclePrefix, level.Cycle);
            }

            var health = player.Vitals.HealthFraction;
            if (_health == null || !Mathf.Approximately(health, _health.Value)) healthFill.fillAmount = (_health = health).Value;

            if (level.BossWarning != _warningShown) bossWarning.gameObject.SetActive((_warningShown = level.BossWarning).Value);
            if (level.BossWarning) bossWarning.enabled = Mathf.Repeat(Time.time * WarningBlinksPerSecond, 1f) < 0.6f;

            if (level.BossActive != _bossBarShown) bossBar.SetActive((_bossBarShown = level.BossActive).Value);
            if (level.BossActive && (_bossHealth == null || !Mathf.Approximately(level.BossHealthFraction, _bossHealth.Value)))
                bossHealthFill.fillAmount = (_bossHealth = level.BossHealthFraction).Value;
        }

        void SetNumber(TMP_Text text, string prefix, long value)
        {
            prefix.CopyTo(0, _buffer, 0, prefix.Length);
            var length = prefix.Length + NumberFormatter.Write(value, _buffer, prefix.Length);
            text.SetCharArray(_buffer, 0, length);
        }
    }
}
