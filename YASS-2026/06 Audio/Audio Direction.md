---
tags:
  - gdd
  - audio
status: approved
updated: 2026-09-20
---

# Audio Direction

Sound effects were a highlighted feature of the original ("a beautiful set of SFX"), so audio feedback is a priority. New audio is AI generated.

## Music
- Style: driving electronic (synthwave or darksynth).
- Tracks: one per level (8), a boss theme, a menu theme and a game-over sting.
- Adaptive: each level track has intensity layers that build as waves escalate and when the boss arrives.

## First music set (Google Lyria 3 Clip, looping clips)
Menu theme, Level 1 track, boss theme. Tracks crossfade on scene and boss transitions. Adaptive intensity layers come later.

## Sound effects (ElevenLabs Sound Effects V2)
| Event | Description | Status |
| ----- | ----------- | ------ |
| Player shot | Short punchy laser blip | Done |
| Enemy shot | Lower, softer energy pulse | Done |
| Small explosion | Enemy destroyed | Done |
| Large explosion | Big meteor or large enemy destroyed | Done |
| Meteor break | Rock cracking and splitting | Done |
| Player hit | Metallic impact with alarm edge | Done |
| Shield hit | Energy ripple | Done |
| Life lost | Heavy explosion with a descending tone | Done |
| Pickup | Bright chime | Done |
| Weapon upgrade | Rising power-up sweep | Done |
| Boss warning | Siren | Done |
| Boss explosion | Long multi-stage explosion | Done |
| UI move / UI confirm | Soft clicks | Done |
| Game over | Short sombre sting | Done |
| Sector clear | Short triumphant fanfare | Done |

## Mixing
An audio mixer with **Music** and **SFX** groups, whose volumes the Settings screen controls. Repeated sounds (shots) are rate-limited so they do not stack into noise.

**As built (effects pass):** the effects volume is applied directly to the pooled voices that play them, and repeats are limited per sound (shots 60 ms, explosions 40 ms, player and shield hits 100 ms) with a small repeating pitch variation so a stream of shots is not one flat note. Each clip also has its own trim in the sound bank, so a long explosion cannot drown a shot. Twelve voices play at once; when they are all busy the one closest to finishing is reused, so a four-second boss explosion survives the shots fired over it. Changing the volume reaches sounds already playing. The mixer groups themselves arrive with the music pass, which is what needs them; until then the **music slider changes nothing audible**.

Sound effects are generated into `Assets/_Game/Audio/SFX/`, one file per entry above, named after it. `Tools/YASS/Build Feedback Assets` matches those files to the game's sound list, so adding or replacing a sound only means generating a file with the right name and rebuilding.

Back to [[00 GDD Home]]
