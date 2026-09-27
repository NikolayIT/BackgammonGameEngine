namespace Backgammon.AI
{
    using Backgammon.Logic;

    /// <summary>A bot's decision: the action, and how hard the choice was, so a host can pace its think time like a person's.</summary>
    public sealed class BackgammonBotDecision
    {
        /// <summary>Gets the action to play.</summary>
        public BackgammonAction Action { get; init; } = new();

        /// <summary>
        /// Gets how hard the choice was: 0 when the play was forced, about 1 for a typical stage, and more for a stage
        /// with very many distinct plays (see <see cref="BackgammonBot.Complexity"/>).
        /// </summary>
        public double Complexity { get; init; }

        /// <summary>Gets the number of distinct positions the stage could end in.</summary>
        public int DistinctPlays { get; init; }
    }
}
