namespace Backgammon.Logic
{
    /// <summary>How a game ended.</summary>
    public enum BackgammonResultKind
    {
        /// <summary>The winner bore off all 15 checkers and the loser had borne off at least one: 1 point.</summary>
        Normal = 0,

        /// <summary>Марс: the winner bore off all 15 checkers and the loser had borne off none: 2 points.</summary>
        Mars = 1,

        /// <summary>
        /// Тапа: the winner pinned the loser's last checker on its start (the майка) while having no checkers on its
        /// own start: 2 points, at once.
        /// </summary>
        Mother = 2,

        /// <summary>Тапа: both mothers were pinned. A draw, 1 point each.</summary>
        Draw = 3,

        /// <summary>
        /// Neither side moved a checker for <see cref="BackgammonMatch.StuckRollLimit"/> rolls in a row. A draw with no
        /// points.
        /// </summary>
        Stuck = 4,
    }
}
