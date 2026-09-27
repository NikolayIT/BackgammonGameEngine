namespace Backgammon.Logic
{
    /// <summary>
    /// One checker moved by one die, in the mover's own numbering: every player numbers the points 1..24 in the
    /// direction it moves. The checker goes from <paramref name="From"/> to <c>From - Die</c>; 0 or below means it is
    /// borne off.
    /// </summary>
    /// <param name="From">The point the checker leaves: 1..24, or 25 for the bar.</param>
    /// <param name="Die">The die used, 1..6.</param>
    public readonly record struct BackgammonStep(int From, int Die)
    {
        /// <summary>The number used for the bar in <see cref="From"/>.</summary>
        public const int Bar = 25;
    }
}
