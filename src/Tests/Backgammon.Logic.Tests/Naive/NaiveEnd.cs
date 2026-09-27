namespace Backgammon.Logic.Tests.Naive
{
    using System.Collections.Generic;

    /// <summary>One legal end: its canonical steps and how it ended the game.</summary>
    internal sealed record NaiveEnd(IReadOnlyList<(int From, int Die)> Steps, int End);
}
