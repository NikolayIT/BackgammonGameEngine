namespace Backgammon.Logic.Tests.Naive
{
    using System.Collections.Generic;

    /// <summary>What the naive generator found: the effective length and every legal end with its canonical steps.</summary>
    internal sealed record NaiveResult(int MaxEffective, IReadOnlyDictionary<string, NaiveEnd> Ends);
}
