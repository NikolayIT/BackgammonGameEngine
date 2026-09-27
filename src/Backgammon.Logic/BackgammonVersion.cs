namespace Backgammon.Logic
{
    /// <summary>
    /// The rules one game is played by. A match of a single version plays every game by it; a среща rotates
    /// обикновена, гюлбара and тапа (see <see cref="BackgammonVariant"/>).
    /// </summary>
    public enum BackgammonVersion
    {
        /// <summary>
        /// Обикновена (права) табла: 2 checkers on the 24 point, 5 on the 13, 3 on the 8 and 5 on the 6; the players
        /// move in opposite directions; a lone checker that is hit goes to the bar and must re-enter first.
        /// </summary>
        Obiknovena = 0,

        /// <summary>
        /// Гюлбара: all 15 checkers start on the player's own 24 point, the two start points in diagonally opposite
        /// corners, and both players move the same way round. There is no hitting: a single checker holds a point.
        /// Doubles escalate from each player's 4th roll.
        /// </summary>
        Gyulbara = 1,

        /// <summary>
        /// Тапа: all 15 checkers start on the player's own 24 point (the opponent's 1 point) and the players move in
        /// opposite directions. Landing on a lone opponent checker pins it; pinning the opponent's last checker on its
        /// start (the майка) can win the game at once.
        /// </summary>
        Tapa = 2,

        /// <summary>Челеби: обикновена табла with гюлбара's escalating doubles.</summary>
        Chelebi = 3,
    }
}
