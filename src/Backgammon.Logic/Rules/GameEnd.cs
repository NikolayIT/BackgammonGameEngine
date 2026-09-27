namespace Backgammon.Logic.Rules
{
    /// <summary>How a step ended the game, if it did.</summary>
    internal enum GameEnd : byte
    {
        /// <summary>The game goes on.</summary>
        None = 0,

        /// <summary>The mover bore off its last checker.</summary>
        BorneOff = 1,

        /// <summary>
        /// Тапа: the opponent's last checker on its start is pinned by the mover, and the mover has none left on its
        /// own start. The mover wins марс.
        /// </summary>
        Mother = 2,

        /// <summary>Тапа: both mothers are pinned; a draw, 1 point each.</summary>
        BothMothers = 3,
    }
}
