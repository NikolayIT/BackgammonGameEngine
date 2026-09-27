namespace Backgammon.Logic.Tests.Naive
{
    using System.Collections.Generic;

    /// <summary>
    /// What the naive generator found: the effective length, every legal end with its canonical steps, every legal
    /// play (all orders of the steps), and every sequence that cannot be extended, legal or not.
    /// </summary>
    internal sealed record NaiveResult(
        int MaxEffective,
        IReadOnlyDictionary<string, NaiveEnd> Ends,
        IReadOnlyList<List<(int From, int Die)>> Plays,
        IReadOnlyList<List<(int From, int Die)>> Sequences);
}
