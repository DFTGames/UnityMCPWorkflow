using UnityEngine;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// Settings storage backed by PlayerPrefs, which works on every target platform including WebGL.
    /// </summary>
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);

        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);

        public bool GetBool(string key, bool fallback) => PlayerPrefs.GetInt(key, fallback ? 1 : 0) != 0;

        public void SetBool(string key, bool value) => PlayerPrefs.SetInt(key, value ? 1 : 0);

        public void Save() => PlayerPrefs.Save();
    }
}
