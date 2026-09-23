---
tags:
  - gdd
  - audio
status: approved
updated: 2026-09-23
---

# Audio Direction

Sound effects were a highlighted feature of the original ("a beautiful set of SFX"), so audio feedback is a priority. New audio is AI generated.

## Music
- Style: driving electronic (synthwave or darksynth).
- Tracks: one per level (8), a boss theme, a menu theme and a game-over sting.
- Adaptive: each level track has intensity layers that build as waves escalate and when the boss arrives.

## First music set (Google Lyria 3 Clip, looping clips)
Menu theme, Level 1 track, boss theme. Tracks crossfade on scene and boss transitions. Adaptive intensity layers come later.

**As built.** Three 30-second clips in `Assets/_Game/Audio/Music/`, looped. The title plays the menu theme; a level starts on its own track and crossfades to the boss theme when the boss warning sounds, over 1.5 seconds with both tracks briefly audible rather than one cutting to the other. The music runs on unscaled time, so pausing does not silence it. Lyria does not generate seamless loops, so the clips are asked for a steady piece with no fade at either end; a true seamless loop needs editing the clip, which is not done yet.

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

**As built.** `Assets/_Game/Audio/YASS.mixer` has the Music and SFX groups under Master, with their volumes exposed as `MusicVolume` and `SfxVolume`. The Settings sliders are linear but loudness is not, so each value is converted to decibels (half the slider is about -6 dB, and zero is silence rather than a quiet hum) and written to the group. Both sliders now do what they say.

The saved volumes are in force from the moment the game opens, not from the moment the player visits the Settings screen. That takes a little care: the write made as the first scene wakes is discarded by the audio system, so the game reads the groups back every frame and writes again whenever they do not hold what the settings asked for.

Effect voices feed the SFX group and carry only their clip's own trim from the sound bank, so the volume is applied once. Repeats are limited per sound (shots 60 ms, explosions 40 ms, player and shield hits 100 ms) with a small repeating pitch variation so a stream of shots is not one flat note. Twelve voices play at once; when they are all busy the one closest to finishing is reused, so a four-second boss explosion survives the shots fired over it.

Sound effects are generated into `Assets/_Game/Audio/SFX/`, one file per entry above, named after it, and listed in the `SoundBank` asset. Adding or replacing a sound means generating the file and pointing the bank's row at it.

Back to [[00 GDD Home]]
