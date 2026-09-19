using System.Runtime.CompilerServices;

// Rules-state mutators are internal: presentation code must go through GameSession so that
// cross-cutting rules (chain reset, pity timer, bonuses) are always applied. Tests may call them directly.
[assembly: InternalsVisibleTo("YASS.Game.Tests.EditMode")]
[assembly: InternalsVisibleTo("YASS.Game.Tests.PlayMode")]
