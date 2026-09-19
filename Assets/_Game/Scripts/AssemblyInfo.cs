using System.Runtime.CompilerServices;

// Test seams on the gameplay layer (for example GameRunner.SetCommandOverride, EngineExhaust.SetThrottle) are internal.
[assembly: InternalsVisibleTo("YASS.Game.Tests.PlayMode")]
[assembly: InternalsVisibleTo("YASS.Game.Tests.EditMode")]
