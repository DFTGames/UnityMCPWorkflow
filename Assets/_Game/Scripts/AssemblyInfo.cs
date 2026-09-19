using System.Runtime.CompilerServices;

// Test seams on the gameplay layer (for example GameRunner.CommandOverride) are internal.
[assembly: InternalsVisibleTo("YASS.Game.Tests.PlayMode")]
