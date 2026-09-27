namespace Backgammon.Logic
{
    /// <summary>A stage still to come in an escalating chain or a remainder: <see cref="Count"/> dice of <see cref="Die"/>.</summary>
    public sealed class BackgammonStage
    {
        /// <summary>Gets the die value, 1..6.</summary>
        public int Die { get; init; }

        /// <summary>Gets how many times it is played, 1..4.</summary>
        public int Count { get; init; }
    }
}
