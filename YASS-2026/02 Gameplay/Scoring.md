---
tags:
  - gdd
  - gameplay
status: approved
updated: 2026-09-24
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

**The pilot's name is the account.** One name to remember, and it is what the boards show. Asked once, on the way into the first run, with a password beside it: 3 to 12 characters, letters, numbers and `. - _ @`, and no spaces, because those are the service's rules for a name that identifies an account. The name is never quietly altered to fit them, as it was when it was only a label: a name the game changed is one the player cannot type again next time, so a name that will not do is refused and said so. The password is 8 to 30 characters with a capital, a small letter, a digit and a symbol; both are checked before the game calls the service, so a mistake is answered at once and in the player's words.

Because the account is the identity, **scores follow the player to any device**, and two people sharing a machine keep their own. The cost is that names are unique: the second player wanting "Ace" is told it is taken.

**Signing up and signing in are separate choices** on that screen, not one clever button. Guessing would mean either creating an account for somebody who mistyped a name they already have, or telling a new player their password is wrong.

**Nobody is made to have one.** The same screen offers playing without an account, which is remembered, so the question is asked once rather than at the start of every session. That player gets the whole game; their runs are kept on this machine and go on no online board. A signed-in player cannot edit their name in Settings, because that screen renames a label and not an account.

%% Not built yet: there is no way to sign out, and no way back for a player who chose to play without an account, so both choices are final on a machine and two people cannot share one. See [[Open Questions]], "Account management". %%

**Boards are browsable without playing**, from the Title: any of the six, by cycling mode and difficulty, showing the top 10 with the player's own run picked out in the highlight colour.

**Nothing about a leaderboard may interrupt a game.** A returning player is signed in again from what the machine remembers, while they are reading the title; nobody is asked to sign in to play. A player with no network, or no account, sees their own best runs kept on this machine instead, said plainly, and their results screen simply shows no placing. A failure is never an error the player has to dismiss.

Back to [[00 GDD Home]]
