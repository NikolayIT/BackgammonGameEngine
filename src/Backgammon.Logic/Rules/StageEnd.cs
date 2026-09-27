namespace Backgammon.Logic.Rules
{
    /// <summary>One distinct position a stage can end in, with the canonical steps that reach it.</summary>
    internal readonly struct StageEnd
    {
        public StageEnd(in Position position, StepSequence steps, GameEnd end)
        {
            this.Position = position;
            this.Steps = steps;
            this.End = end;
        }

        public Position Position { get; }

        public StepSequence Steps { get; }

        /// <summary>Gets whether (and how) the last step ended the game.</summary>
        public GameEnd End { get; }
    }
}
