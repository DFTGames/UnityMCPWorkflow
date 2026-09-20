using System;
using UnityEngine;

namespace YASS.Feedback
{
    /// <summary>
    /// A pooled particle effect. Returns itself to its pool when the system finishes, so nothing has to time it
    /// (the prefabs are authored with Stop Action set to Callback).
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class PooledEffect : MonoBehaviour
    {
        ParticleSystem _particles;
        Action<PooledEffect> _release;

        void Awake() => _particles = GetComponent<ParticleSystem>();

        public void Play(Vector2 position, Action<PooledEffect> release)
        {
            _release = release;
            transform.position = position;
            _particles.Clear(true);
            _particles.Play(true);
        }

        void OnParticleSystemStopped()
        {
            var release = _release;
            _release = null;
            release?.Invoke(this);
        }
    }
}
