namespace Backgammon.Logic
{
    /// <summary>What a match is played as: one version for every game, or the traditional среща rotation.</summary>
    public enum BackgammonVariant
    {
        /// <summary>Every game is обикновена (права) табла. Played to 3 points by default.</summary>
        Obiknovena = 0,

        /// <summary>Every game is гюлбара. Played to 3 points by default.</summary>
        Gyulbara = 1,

        /// <summary>Every game is тапа. Played to 3 points by default.</summary>
        Tapa = 2,

        /// <summary>Every game is челеби. Played to 3 points by default.</summary>
        Chelebi = 3,

        /// <summary>
        /// Среща, the traditional match: game 1 is обикновена, game 2 гюлбара, game 3 тапа, game 4 обикновена again,
        /// and so on. Played to 5 points by default.
        /// </summary>
        Sreshta = 4,
    }
}
