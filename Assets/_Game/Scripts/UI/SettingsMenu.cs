using UnityEngine;
using UnityEngine.UI;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// Settings screen (GDD "UI Flow and Screens", Settings): the pilot name, music and effects volume,
    /// screen shake, fullscreen on desktop, and the way through to the account screen. Every change is saved
    /// at once, so leaving by any route keeps it.
    /// </summary>
    public sealed class SettingsMenu : MonoBehaviour
    {
        [SerializeField] Slider musicVolume;
        [SerializeField] Slider sfxVolume;
        [SerializeField] Toggle screenShake;
        [SerializeField] Toggle fullscreen;

        [SerializeField, Tooltip("The fullscreen row, hidden where the platform owns the window.")]
        GameObject fullscreenRow;

        [SerializeField, Tooltip("The name that goes on the leaderboards and the HUD.")]
        TMPro.TMP_InputField playerName;

        [SerializeField, Tooltip("Opens the account screen. Optional: a level has no account screen.")]
        MenuRouter router;

        [SerializeField, Tooltip("The account row, hidden in scenes with no account screen to open.")]
        GameObject accountRow;

        // While the widgets are being filled in from the saved settings, their callbacks must not write back.
        bool _loading;

        /// <summary>Read at the point of use: tests swap the service, so a cached reference would go stale.</summary>
        static SettingsService Settings => GameFlow.Settings;

        void Awake()
        {
            if (fullscreenRow != null) fullscreenRow.SetActive(GameFlow.SupportsFullscreen);
            if (accountRow != null) accountRow.SetActive(router != null && router.Has(MenuScreen.Account));

            if (musicVolume != null) musicVolume.onValueChanged.AddListener(OnMusicVolume);
            if (sfxVolume != null) sfxVolume.onValueChanged.AddListener(OnSfxVolume);
            if (screenShake != null) screenShake.onValueChanged.AddListener(OnScreenShake);
            if (fullscreen != null) fullscreen.onValueChanged.AddListener(OnFullscreen);

            if (playerName != null)
            {
                playerName.characterLimit = Credentials.MaxNameLength;
                playerName.onEndEdit.AddListener(OnPlayerName);
            }
        }

        /// <summary>
        /// The account screen, where signing in, signing out and changing who is signed in happen (GDD
        /// "UI Flow and Screens", Account). Managing the account itself is Unity's page, not ours.
        /// </summary>
        public void OpenAccount()
        {
            if (router != null) router.Open(MenuScreen.Account);
        }

        void OnEnable() => Load(Settings.Settings);

        /// <summary>Dragging a slider writes the value at once but not to disk; leaving the screen does that.</summary>
        /// <remarks>
        /// The name is committed here first. A panel's OnDisable runs before its children's, and it is the
        /// input field's own deactivation that fires the end-of-edit this screen listens for, so a name typed
        /// and then dismissed with Back was being stored just after the flush meant to save it.
        /// </remarks>
        void OnDisable()
        {
            // Only when the box is this machine's to edit. While signed in it shows the account's name
            // read-only, and writing that back here would recreate the stored copy that outlives the
            // account and turns up in the next person's settings.
            if (playerName != null && playerName.interactable) OnPlayerName(playerName.text);
            Settings.Flush();
        }

        void OnDestroy()
        {
            if (musicVolume != null) musicVolume.onValueChanged.RemoveListener(OnMusicVolume);
            if (sfxVolume != null) sfxVolume.onValueChanged.RemoveListener(OnSfxVolume);
            if (screenShake != null) screenShake.onValueChanged.RemoveListener(OnScreenShake);
            if (fullscreen != null) fullscreen.onValueChanged.RemoveListener(OnFullscreen);
            if (playerName != null) playerName.onEndEdit.RemoveListener(OnPlayerName);
        }

        void Load(GameSettings settings)
        {
            _loading = true;
            if (musicVolume != null) musicVolume.SetValueWithoutNotify(settings.MusicVolume);
            if (sfxVolume != null) sfxVolume.SetValueWithoutNotify(settings.SfxVolume);
            if (screenShake != null) screenShake.SetIsOnWithoutNotify(settings.ScreenShake);
            if (fullscreen != null) fullscreen.SetIsOnWithoutNotify(settings.Fullscreen);
            if (playerName != null)
            {
                var accounts = GameFlow.Accounts;

                if (accounts.IsSignedIn)
                {
                    // The account's name, never this machine's copy: the stored one belongs to whoever was
                    // signed in last, and showing it to the next person is how somebody sees a stranger's
                    // name in their own settings. Read-only here; the account screen changes it.
                    playerName.SetTextWithoutNotify(accounts.PilotName);
                    playerName.interactable = false;
                    Say("Your account name. Change it on the Account screen.");

                    // Empty until the service answers, so ask, and fill it in when it does.
                    accounts.FetchPilotName(fetched =>
                    {
                        if (this == null || !isActiveAndEnabled || playerName == null) return;

                        _loading = true;
                        playerName.SetTextWithoutNotify(fetched);
                        _loading = false;
                    });
                }
                else
                {
                    playerName.SetTextWithoutNotify(Settings.PlayerName);
                    playerName.interactable = true;
                }
            }

            _loading = false;
        }

        /// <summary>
        /// On finishing the edit rather than on every keystroke: the name is cleaned as it is stored, and
        /// rewriting the box mid-word would fight the player typing.
        /// </summary>
        [SerializeField, Tooltip("Why a name was refused. Optional: without it a refusal is silent.")]
        TMPro.TMP_Text nameProblem;

        void OnPlayerName(string value)
        {
            if (_loading) return;

            // Checked here as well as on the first-run screen. Without this the box accepted anything, the
            // service refused it when a score was submitted, and the only sign was a warning in the log
            // while the board quietly kept the previous name (GDD "Scoring": a name that will not do is
            // refused and said so).
            var check = Credentials.CheckName(value);
            if (!check.IsUsable)
            {
                Say(check.Problem);

                // The box keeps what they typed, so they can correct it rather than retype it from nothing.
                return;
            }

            Say(string.Empty);

            // Never while signed in: the account owns the name then, and this box is showing it read-only.
            if (GameFlow.Accounts.IsSignedIn) return;

            // This machine's copy, for a player with no account. Changing the name on an account is done on
            // the account screen, with a button, because it changes what every board prints and the service
            // rate-limits renames: it should not happen as a side effect of clicking out of a text box.
            Settings.SetPlayerName(value);
            if (playerName != null) playerName.SetTextWithoutNotify(Settings.PlayerName);

            if (GameFlow.Accounts.IsSignedIn)
                Say("Signed in: change the name the boards show on the Account screen.");
        }

        void Say(string problem)
        {
            if (nameProblem != null) nameProblem.text = problem ?? string.Empty;
        }

        void OnMusicVolume(float value)
        {
            if (!_loading) Settings.SetMusicVolume(value);
        }

        void OnSfxVolume(float value)
        {
            if (!_loading) Settings.SetSfxVolume(value);
        }

        void OnScreenShake(bool value)
        {
            if (!_loading) Settings.SetScreenShake(value);
        }

        void OnFullscreen(bool value)
        {
            if (!_loading) Settings.SetFullscreen(value);
        }
    }
}
