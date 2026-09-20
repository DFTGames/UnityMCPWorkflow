using UnityEngine;

namespace YASS.Gameplay
{
    /// <summary>
    /// The boss's weak core: a child collider that forwards player shots to <see cref="BossView.TakeCoreHit"/>,
    /// and shows whether it is open (bright, damageable) or closed (dim, armoured).
    /// </summary>
    public sealed class BossCoreView : MonoBehaviour, IDamageable
    {
        [SerializeField] SpriteRenderer coreRenderer;
        [SerializeField] Color openColour = Color.white;
        [SerializeField] Color closedColour = new Color(0.25f, 0.25f, 0.3f, 1f);

        BossView _boss;
        bool? _shownOpen;

        public bool BlocksPiercing => false;
        public bool IsAlive => _boss != null && _boss.IsAlive;
        public BossView Boss => _boss;

        public void Init(BossView boss)
        {
            _boss = boss;
            _shownOpen = null;
        }

        public void TakeHit(float damage, int playerIndex)
        {
            if (_boss != null) _boss.TakeCoreHit(damage, playerIndex);
        }

        public void ShowOpen(bool open)
        {
            // Written every step rather than only on a change: the core's colour is the player's one tell, and
            // anything else that touches the renderer (a hit flash, say) must not be able to leave it wrong.
            _shownOpen = open;
            coreRenderer.color = open ? openColour : closedColour;
        }
    }
}
