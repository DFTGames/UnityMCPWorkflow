---
tags:
  - gdd
  - production
status: approved
updated: 2026-09-26
---

# itch.io Sign-in Setup

What has to exist outside the repository before the web build can sign anybody in. Nothing here is in the
game's code, and none of it can be done from the Editor.

The design is in [[Scoring]]; the code notes are in `CLAUDE.md` under "Signing in on the web".

## 1. Publish the callback page

itch.io's OAuth redirect has to land on a page we control, and it has to be one exact registered URL. That
page is `docs/itch-callback.html` in this repository.

- On GitHub: **Settings → Pages → Source: Deploy from a branch**, branch `main`, folder `/docs`.
- Confirm it loads: <https://dftgames.github.io/UnityMCPWorkflow/itch-callback.html>. Opened directly it
  should say it has nothing to sign in with, which is the correct answer.
- `docs/` holds **only** that one page, because Pages serves every file in the folder to the public web.
  These notes live here in the vault for that reason.
- The page's own `ALLOWED` list has to contain the origin the build is served from, or the token is posted
  nowhere. The page cannot detect that and will not say so, because `postMessage` drops silently across a
  mismatched origin. If sign-in gets as far as itch and then nothing happens, check that list first.

If the repository is renamed or the Pages URL differs, change `ItchAccounts.CallbackPage` to match. The
origin the plugin trusts is derived from that constant, so the two cannot drift apart.

## 2. Register the itch.io OAuth application

<https://itch.io/user/settings/oauth-apps> → **Create OAuth application**.

- **Redirect URI:** exactly the Pages URL above. A mismatch is rejected by itch as a bad request, which
  reads like a broken app rather than a wrong setting, so it is worth pasting rather than typing.
- Take the **client id** and put it in `ItchSettings.ClientId`. It is not a secret: it travels in the URL
  every player is sent to. While it is empty the sign-in button stays hidden, on purpose.

**Done on 2026-09-26**, so the web build now offers the button. The id and the redirect are registered with
itch as a pair and are checked as a pair, so changing either means changing the other and re-registering;
`ItchSettingsTests` pins both, which is the only warning available on this side of the flow.

Nothing asks for a client secret, because itch supports only the implicit flow. That is why the token has
to be verified server side before it means anything.

## 3. Create the service account

Unity Cloud dashboard → **Administration → Service Accounts** → create one for this project.

- Role: **Player Authentication Token Issuer**, on project `4279fc32-7e38-465f-b493-66a8aff2da78`.
- Keep the key id and secret key; the dashboard shows the secret once.

This key can mint a session for **any** player id. It never goes in the game, in this repository, or in a
log. It exists only in Secret Manager, read by Cloud Code.

## 4. Store the key as a secret

Unity Cloud dashboard → **Secret Manager**.

- Name: `ITCH_SIGNIN_SERVICE_ACCOUNT`
- Value: `<KEY_ID>:<SECRET_KEY>`, exactly that, one colon, no spaces. The script base64-encodes it itself.

**Done on 2026-09-26, at the organization level.** Cloud Code resolves a secret lowest-first (environment,
then project, then organization), so one organization entry serves both `production` and `test`. That is
the right choice here and it sidesteps the trap this step would otherwise have: a build plays against
`production` and the editor against `test`, so a secret set in only one of them leaves the other unable to
sign anybody in, with a symptom indistinguishable from itch being down.

The thing to know about organization level is its reach: every project in the organization can read it, and
this key can mint a session for any player. If another project is ever added to this organization, move the
secret down to the project or environment level, where only this game can see it.

## 5. Enable Custom ID sign-in

Unity Cloud dashboard → **Authentication → Identity Providers** → add **Custom ID**. Without it the
`custom-id` endpoint refuses every call.

## 6. Deploy the Cloud Code script

`CloudCode/SignInWithItch.js`, published under the name `SignInWithItch` (which is what
`ItchAccounts.SignInScript` calls).

**Done on 2026-09-26: version 1 is live in both `test` and `production`.** These steps are for the next
change to the script.

Publishing is two commands, not one: `publish` takes only a name, so the file is uploaded separately. The
first upload of a script is `create -t API -l JS`; every later one is `update`.

```
ugs cloud-code scripts update SignInWithItch CloudCode/SignInWithItch.js \
  -p 4279fc32-7e38-465f-b493-66a8aff2da78 -e production
ugs cloud-code scripts publish SignInWithItch \
  -p 4279fc32-7e38-465f-b493-66a8aff2da78 -e production
```

Repeat with `-e test`. As ever with this project's UGS CLI use, `-p` on every command: the CLI's own
default is a different project.

**Uploading is not deploying.** A script uploaded but not published leaves the previous version serving
players, which looks exactly like a change that did nothing.
`ugs cloud-code scripts get SignInWithItch -p <id> -e <env>` shows the active version and lists the others,
and `publish -v <n>` rolls back to one of them.

## 7. Check it before trusting it

In this order, because each step fails in a way the next one hides.

1. **Does the popup open at all?** This is the one thing that cannot be settled from here: itch does not
   document the sandbox attributes on its game iframe, so the window may be blocked outright. Upload a web
   build to itch and press the button. If it is blocked the game says so plainly rather than doing nothing,
   and the fallback is a plain link that opens a tab, which is not built yet.
2. **Does itch come back?** A wrong redirect URI fails here, at itch's page, before the game is involved.
3. **Does Cloud Code answer?** "Signing in is not set up correctly in this version" on screen means the
   secret is missing or wrong for that environment. Cloud Code's own logs say which.
4. **Is the player the same one next time?** Reload the page. A returning player should come back signed
   in, with the same pilot name, without the popup.
5. **Is it the same player in another browser?** Sign in there too. Same itch account must mean the same
   scores; if it does not, the external id is not doing its job.

## What is deliberately not set up

There is no itch sign-in on desktop or mobile, and no way to join a web identity to a desktop one, so one
person playing both is two players with two sets of scores. Both are recorded in [[Open Questions]].
