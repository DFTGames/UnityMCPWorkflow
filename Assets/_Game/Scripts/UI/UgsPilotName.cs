using System;
using Unity.Services.Authentication;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// The pilot name the service holds for whoever is signed in, which is the name the boards print.
    /// </summary>
    /// <remarks>
    /// Not the name this machine remembers. A name kept only on the client is what the player is shown while
    /// their runs appear under something else entirely, which is exactly what happened before this existed.
    /// </remarks>
    sealed class UgsPilotName
    {
        readonly string _owner;

        /// <summary>
        /// The last name the service gave us. The package caches the name in <c>PlayerName</c> too, but whole
        /// screens hang off this value, so it is worth holding rather than assuming.
        /// </summary>
        string _known = string.Empty;

        public UgsPilotName(string owner) => _owner = owner;

        /// <summary>The name without the number the service appends to keep names unique.</summary>
        public string Current(bool signedIn)
        {
            if (!signedIn) return string.Empty;

            try
            {
                if (!UgsCalls.ServicesReady) return _known;

                var live = WithoutTheNumber(AuthenticationService.Instance.PlayerName);
                return string.IsNullOrEmpty(live) ? _known : live;
            }
            catch (Exception problem)
            {
                UgsCalls.Log(_owner, "read the pilot name", problem);
                return _known;
            }
        }

        public async void Fetch(bool signedIn, Action<string> done)
        {
            if (!signedIn)
            {
                done?.Invoke(string.Empty);
                return;
            }

            try
            {
                // Auto-generating, which is the default and the right thing: a player who has never chosen a
                // name still has one on the service, and that generated name is what every board prints.
                // Asking with autoGenerate off was a mistake: it returned nothing, so the game had no name to
                // show and displayed something of its own, guaranteeing screen and boards disagreed.
                var name = await UgsCalls.WithDeadline(
                    AuthenticationService.Instance.GetPlayerNameAsync(), UgsCalls.TimeoutSeconds);

                _known = WithoutTheNumber(name);
                done?.Invoke(_known);
            }
            catch (Exception problem)
            {
                UgsCalls.Log(_owner, "fetch the pilot name", problem);
                done?.Invoke(Current(true));
            }
        }

        public async void Set(bool signedIn, string name, Action<AccountResult> done)
        {
            if (!signedIn)
            {
                done?.Invoke(new AccountResult(AccountStatus.Refused,
                    "Sign in first: the name on the boards belongs to your account."));
                return;
            }

            var wanted = Leaderboards.CleanName(name);

            try
            {
                await UgsCalls.WithDeadline(
                    AuthenticationService.Instance.UpdatePlayerNameAsync(wanted), UgsCalls.TimeoutSeconds);

                _known = wanted;
                done?.Invoke(AccountResult.Ok);
            }
            catch (Exception problem)
            {
                UgsCalls.Log(_owner, "set the pilot name", problem);

                // The service rate-limits renames and has its own rules, so a refusal here is ordinary and
                // must be said rather than swallowed: the player has just watched their name not change.
                done?.Invoke(new AccountResult(AccountStatus.Refused,
                    "The service would not take that name. Try another, or try again shortly."));
            }
        }

        public void Forget() => _known = string.Empty;

        public bool Known => !string.IsNullOrEmpty(_known);

        /// <summary>Drops the "#1234" the service appends to keep player names unique.</summary>
        public static string WithoutTheNumber(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;

            var hash = name.IndexOf('#');
            return hash > 0 ? name.Substring(0, hash) : name;
        }
    }
}
