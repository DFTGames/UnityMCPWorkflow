---
tags:
  - gdd
  - gameplay
status: approved
updated: 2026-09-26
---

# Scoring

## Point values
| Source | Points |
| ------ | ------ |
| Small enemy | 100 |
| Medium enemy | 250 |
| Large enemy | 500 |
| Splitting meteor: large / medium / small | 50 / 30 / 20 |
| Solid meteor | 150 |
| Proximity mine shot down | 50 |
| Boss | 10,000 × level number |
| Weapon upgrade collected at level 5 | 500 |
| Level clear | remaining health % × 20 (maximum 2,000) |
| Boss defeated without taking damage | 5,000 |

Enemy size classes are listed in [[Enemies and Hazards]].

## Chain
- A "kill" is any destroyed enemy or meteor: both extend the chain and count towards the HUD kill count. _Provisional: chosen during implementation, awaiting approval._
- Each kill within **1.5 seconds** of the previous kill adds **×0.1** to the chain multiplier, up to **×3.0**. The chain starts at ×1.0.
- The chain resets to ×1.0 when the 1.5 second window runs out or when the player takes damage (a hit absorbed by the shield does not count as damage).

- A boss kill also extends the chain. A shield-absorbed hit does not forfeit the no-damage boss bonus (consistent with the chain rule). The no-damage window runs from the boss's appearance to its defeat. _Provisional._
- Level clear uses the exact remaining health percentage (not rounded to a whole percent).

## Final score
Points awarded = base points × chain multiplier × difficulty score multiplier ([[Difficulty and Balancing]]) × Endless cycle multiplier ([[Core Loop#Endless mode]], ×1.0 in Campaign). Bonuses (level clear, no-damage boss, level 5 upgrade) are multiplied by difficulty and Endless multipliers but not by the chain.

## Leaderboards
Separate Unity Gaming Services leaderboards per mode (Campaign, Endless) and difficulty: 6 boards, ordered highest first. Their ids are `<mode>_<difficulty>` (`campaign_cadet` through `endless_ace`) and are fixed: renaming one orphans every score already filed under it.

**A run is submitted when it is over**, which is at game over in either mode, and at clearing the campaign's last level. A cleared level in the middle of a campaign is not submitted: the run carries on and a campaign is one score ([[Core Loop]]). A run worth nothing is never sent, so a board of zeroes cannot bury the runs that meant something.

**The account is a Unity account; the pilot name is separate.** Signing in opens Unity's own page in the system browser, and the game never sees a password: changing one, recovering a forgotten one and closing the account all happen on Unity's account portal, reached from the account screen. What the boards show is a pilot name the player chooses, 3 to 50 characters of letters, numbers and `. - _`, asked once on the way into the first run and editable in Settings afterwards. The 50 is Unity Authentication's own documented limit, not a number this game picked: a signed-in player who has never chosen anything is given a generated name such as `SeriousForgottenSnowflake`, and a game that cannot hold the name its own service assigned cannot show it or let anybody type it back. A board row is narrower than that (about 29 characters at its width), so a longer name is shortened with an ellipsis **where it is drawn only**; it is never shortened where it is stored. The two are deliberately apart, because a Unity account is identified by an email address and an email address must never appear on a public board. The name is never quietly altered to fit the rules: a name the game changed is one the player cannot type again next time, so a name that will not do is refused and said so.

Because the account is the identity, **scores follow the player to any device**, and two people sharing a machine keep their own. Pilot names are not unique, so two players may both call themselves Ace; what tells their runs apart is the account behind them.

**One button, not two.** Unity's page signs a player in or signs them up as it finds appropriate, so the game no longer has to ask which they meant, and cannot create an account for somebody who mistyped a name they already have.

**Nobody is made to have one.** The first-run screen offers playing without an account, which is remembered, so the question is asked once rather than at the start of every session. That player gets the whole game; their runs are kept on this machine and go on no online board. The choice is not final: the account screen signs them in later. The pilot name stays editable in Settings whether or not anybody is signed in, because it is a label rather than an account.

**Signing out and changing who is signed in** are on the account screen, behind Settings. Signing out clears the remembered session as well, so a machine handed to somebody else does not sign the first player back in; a switch abandoned halfway leaves nobody signed in rather than the previous player, which is the safe end to stop at.

**Boards are browsable without playing**, from the Title: any of the six, by cycling mode and difficulty, showing the top 10 with the player's own run picked out in the highlight colour.

**Nothing about a leaderboard may interrupt a game.** A returning player is signed in again from what the machine remembers, while they are reading the title; nobody is asked to sign in to play. A player with no network, or no account, sees their own best runs kept on this machine instead, said plainly, and their results screen simply shows no placing. A failure is never an error the player has to dismiss.

Back to [[00 GDD Home]]
