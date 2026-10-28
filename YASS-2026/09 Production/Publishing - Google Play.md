---
tags:
  - gdd
  - production
status: draft
updated: 2026-10-28
---

# Publishing: Google Play

%% Every piece of text Google Play asks for, ready to paste, plus the answers to the questionnaires it makes you fill in. Keep this page in step with the store listing: when one changes, change the other. %%

The Play Console asks for text in a dozen places and rejects submissions for things that are nowhere near the text. This page holds **both**: the copy to paste, and the decisions behind the forms.

Character limits below are Google's, and every piece here has been measured against them.

## Published

**The game is live on Google Play**, so the copy below is the listing as submitted rather than a plan:

https://play.google.com/store/apps/details?id=com.DFTGamesStudios.YASS2026

The package name is **`com.DFTGamesStudios.YASS2026`**, set in `ProjectSettings` under `applicationIdentifier ▸ Android`. **It is now permanent**: it cannot be changed, and it cannot be reused even if the app is taken down. Any future Android release of this game uses that string or it is a different app.

Being live means the other three gates were cleared too: the upload was an Android App Bundle, the signing key was settled (`AndroidKeystoreName` points outside the repository, at `D:/git/Tutorials/user.keystore` with alias `android`, and must stay outside it), and a privacy policy is published at a public URL.

The privacy policy is published at **https://dftgames.com/privacy-statement/**, which is the address given to the Play Console.

Worth knowing: `AndroidTargetArchitectures` is ARM64 only, which Play accepts. `minSdkVersion` is 29. Check the **target API level** against Play's current requirement before each submission, since Google raises it annually and rejects anything below it.

## Store listing

### App name
Limit **30 characters**.

```
YASS 2026
```

Keep it the brand alone. Play's metadata policy discourages keywords, rankings and promotional text in the name, and the short description is where discovery copy belongs.

### Short description
Limit **80 characters**. Shown first, and the only copy many people read.

```
A fast, hard arcade space shooter. Eight sectors, eight bosses, endless runs.
```

### Full description
Limit **4000 characters**. Plain text: Play strips most formatting, so the capitalised headings below are deliberate and do the work that bold would.

```
Yet Another Space Shooter. Fast, hard, and unapologetically arcade.

A 2026 remake of YASS, released by DFT Games Studios in 2010. Your ship holds the left of the screen. Everything else comes at you from the right.

EIGHT SECTORS, EIGHT BOSSES
Fight through the Magenta Nebula, an Asteroid Belt, the Solar Corona, Ice Rings, a Derelict Fleet, an Ion Storm, the edge of a Black Hole, and finally the Enemy Homeworld. Each one ends with a boss that has armour you have to break and a core that opens for only a few seconds at a time. Learn the pattern, or die learning it.

CHASE THE SCORE
Chain your kills: every kill within 1.5 seconds of the last one raises your multiplier, up to x3. Take a hit and it is gone.

Three difficulties, Cadet, Pilot and Ace, each with its own score multiplier and its own leaderboard.

Endless mode: cycles of ten waves and a boss, drawn from across the whole campaign. Every cycle is faster, fuller and worth more. There is no ending. You stop when you run out of lives.

Six online leaderboards, one for every mode and difficulty.

BUILT FOR TOUCH
Two floating sticks that appear wherever your thumbs land: left to fly, right to aim and fire. Gamepads and Bluetooth keyboards work too.

FREE, AND FREE OF EVERYTHING ELSE
No ads. No in-app purchases. No subscriptions. Nothing to buy, ever.

Signing in is optional. The whole game is playable without an account. Signing in only puts your runs on the online boards instead of keeping them on your device.

AN OPEN SOURCE TUTORIAL
YASS 2026 was built to show what an agentic AI development workflow can do on a real project. The code, art and audio were produced by an AI agent driving the Unity Editor directly, with a human directing and reviewing every change. The entire history, including the mistakes, is public:

github.com/DFTGames/UnityMCPWorkflow
```

### What's new (release notes)
Limit **500 characters** per language. For the first release:

```
First release.

Eight sectors, eight bosses, three difficulties and an Endless mode. Six online leaderboards. Touch, gamepad and keyboard.
```

## Graphics

Play rejects the whole submission for one wrong dimension, so these are exact.

| Asset | Required | Status |
| ----- | -------- | ------ |
| App icon | 512 x 512 PNG, 32-bit with alpha, under 1 MB | **To make.** `Assets/_Game/Icons/Icon_1024.png` is the source; downscale it. Note Play wants the *square* icon, not the Android adaptive pair |
| Feature graphic | 1024 x 500 PNG or JPG, no alpha | **Done:** `Store/google-play-feature-1024x500.png` (RGB, no alpha, 722 KB). See below |
| Phone screenshots | 2 to 8, 16:9 or 9:16, each side 320 to 3840 px | **To capture.** Use `Tools/YASS/Capture Screenshot` |
| Tablet screenshots | Optional, but without them the listing is marked as not designed for tablets | Decide |

Suggested screenshots, in order: the title screen with the logo, a busy wave mid-fight, a boss with its core open, the Endless cycle counter, and a leaderboard.

### How the feature graphic was composed
Worth recording, because it will need remaking whenever the cover art changes.

It is a 1280 x 625 band taken from `Store/cover-source-1280x1024.png` starting at y = 290, scaled to 1024 x 500, with `Sprites/UI/Logo.png` laid over it. The band is 61% of the source's height, so it cannot hold everything: y = 290 was chosen because it keeps the boss's lit core at the top right **and** the player ship almost whole at the bottom left. Starting higher cuts the ship apart; starting lower loses the core, which is the only magenta focal point in the frame.

The logo sits in the bottom right because that is measurably the darkest quarter of the crop (mean luminance 4 of 255, against 121 for the ship's corner), so it needs no darkening wash behind it. A wash was tried and rejected: the only place wide enough to take one also covers the explosion, and dulling the explosion costs more than the legibility gains.

Margins are 7% right and 11% bottom. Play can crop this graphic in some placements, so nothing important sits against an edge.

**Do not put Xbox branding in any graphic.** The description's reference to the 2010 original is factual and nominative, which is fine; a logo or console imagery is a trademark problem.

## Categorisation and contact

| Field | Value |
| ----- | ----- |
| App or game | Game |
| Category | Arcade |
| Tags | Arcade, Action, Shooter, Retro, Single player (max 5) |
| Email | The published support address for DFT Games Studios |
| Website | `https://dftgames.com` |
| Privacy policy | https://dftgames.com/privacy-statement/ |

## Content rating (IARC questionnaire)

The rating comes from a questionnaire, not from a choice. Answer it honestly; a wrong answer is grounds for removal.

| Question | Answer |
| -------- | ------ |
| Violence | Yes, non-realistic. The player shoots spacecraft and asteroids. No humans or creatures are depicted, no blood, no gore, no dismemberment |
| Sexual content, nudity | None |
| Language | None |
| Controlled substances | None |
| Gambling, simulated or real | None |
| **Users interact** | **Yes.** See below |
| Shares user location | No |
| Digital purchases | No |

**The "users interact" answer is Yes, and it carries obligations.** Players choose their own pilot name and it is displayed to other players on the online leaderboards. That is user-generated content. `Leaderboards.CleanName` strips whitespace and caps the length but, deliberately, makes no judgement about what a name says. Play's User Generated Content policy expects a moderation approach and a way for users to report content.

Before release, pick one and record the decision here:
1. Rely on Unity Gaming Services' own name moderation and document that it is relied on, or
2. Add a filter and a report route in-game, or
3. Remove player-chosen names from public boards.

This is tracked in [[Open Questions]].

## Data safety

Every app must complete this form, and it must match what the app actually does. The game's data collection comes almost entirely from Unity Gaming Services rather than from our own code.

| Data type | Collected | Shared | Purpose | Optional? |
| --------- | --------- | ------ | ------- | --------- |
| Email address | Yes, via Unity Player Accounts at sign-in | With Unity, as the service provider | Account management | **Yes**: the game is fully playable signed out |
| User IDs | Yes, the Unity player ID | With Unity | Account management, leaderboards | Yes |
| In-app actions (scores) | Yes, submitted scores and the chosen pilot name | With Unity; the pilot name is public on leaderboards | App functionality | Yes |
| Device or other IDs | Assume yes | With Unity | App functionality, diagnostics | No |

Declare additionally:
- Data **is** encrypted in transit.
- Users **can** request deletion: the account can be deleted from the in-game account screen.
- The email address is **never** shown on a leaderboard; only the chosen pilot name is.
- Settings, progress and local scores stay on the device in `PlayerPrefs` and are not collected.

**Verify the bottom row against Unity's own disclosure before submitting.** Unity publishes what each UGS package collects, and the SDKs gather more than our code asks them to. The form is a declaration about the whole app, including its dependencies, and getting it wrong is an enforcement matter rather than a cosmetic one.

## Privacy policy (draft)

Published at https://dftgames.com/privacy-statement/. The text below is the draft that was written here; **the live page is the authoritative version**, so check against it before reusing this, and change both together if either changes.

```
Privacy Policy for YASS 2026

Last updated: 26 October 2026

YASS 2026 is a free, non-commercial game published by DFT Games Studios. This policy explains what the game collects and why.

PLAYING WITHOUT AN ACCOUNT
You can play all of YASS 2026 without signing in and without giving us anything. Your settings, your progress and your scores are stored on your own device and are not sent anywhere.

IF YOU CHOOSE TO SIGN IN
Signing in is optional, and exists only so your scores can appear on the online leaderboards. Accounts are handled by Unity Gaming Services, operated by Unity Technologies. When you sign in:

- Unity receives and stores your email address, in order to manage your account.
- The game receives an anonymous player identifier from Unity. We use it to attach your scores to you.
- Your email address is never displayed to other players and never appears on a leaderboard.

WHAT APPEARS PUBLICLY
Leaderboards show a pilot name that you choose, together with your score, mode and difficulty. Choose a name you are happy for other players to see. You can change it at any time in Settings.

WHAT WE DO NOT DO
We show no advertising. We sell nothing. We do not sell or rent your data. We do not track you across other apps or websites, and we do not collect your location, contacts, photos or files.

SERVICE PROVIDERS
The game uses Unity Gaming Services for accounts and leaderboards. Data you send to the leaderboards is processed by Unity under Unity's own privacy policy: https://unity.com/legal/privacy-policy

CHILDREN
The game is suitable for a general audience and is not directed at children under 13. We do not knowingly collect personal information from children under 13. If you believe a child has given us personal information, contact us and we will delete it.

DELETING YOUR DATA
You can delete your account, and the data held against it, from the account screen inside the game. You can also remove everything stored on your device by uninstalling the game.

CHANGES
If this policy changes, the new version will be published at this address with an updated date.

CONTACT
Questions about this policy: [SUPPORT EMAIL ADDRESS]
```

## Open risks

- **The package name is undecided and permanent.** Choose it before the first upload, not during it.
- **User-generated names are unmoderated** (see Content rating). The most likely cause of a policy problem with this listing.
- **Target API level rises every year.** A build that passed last year will be rejected this year without a line of code changing.
- **The game is free with no ads and no purchases**, so there is no monetisation to declare, which also means no revenue to offset a developer account fee.

Back to [[00 GDD Home]]
