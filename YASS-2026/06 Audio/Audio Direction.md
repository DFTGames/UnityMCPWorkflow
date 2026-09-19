---
tags:
  - gdd
  - audio
status: approved
updated: 2026-09-19
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
| Player shot | Short punchy laser blip | Planned |
| Enemy shot | Lower, softer energy pulse | Planned |
| Small explosion | Enemy destroyed | Planned |
| Large explosion | Big meteor or large enemy destroyed | Planned |
| Meteor break | Rock cracking and splitting | Planned |
| Player hit | Metallic impact with alarm edge | Planned |
| Shield hit | Energy ripple | Planned |
| Life lost | Heavy explosion with a descending tone | Planned |
| Pickup | Bright chime | Planned |
| Weapon upgrade | Rising power-up sweep | Planned |
| Boss warning | Siren | Planned |
| Boss explosion | Long multi-stage explosion | Planned |
| UI move / UI confirm | Soft clicks | Planned |
| Game over | Short sombre sting | Planned |
| Sector clear | Short triumphant fanfare | Planned |

## Mixing
An audio mixer with **Music** and **SFX** groups, whose volumes the Settings screen controls. Repeated sounds (shots) are rate-limited so they do not stack into noise.

Back to [[00 GDD Home]]
