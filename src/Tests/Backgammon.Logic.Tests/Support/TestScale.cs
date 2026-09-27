namespace Backgammon.Logic.Tests.Support
{
    using System;

    /// <summary>
    /// How big the randomized runs are. By default (and in CI) they are a few thousand per version. With
    /// BACKGAMMON_LONG=1 they are the full runs, 100k+ per version, used before each milestone.
    /// </summary>
    internal static class TestScale
    {
        public static bool IsLong { get; } = Environment.GetEnvironmentVariable("BACKGAMMON_LONG") == "1";

        public static int Pick(int normal, int full) => IsLong ? full : normal;
    }
}
