namespace Backgammon.Trainer
{
    using Backgammon.AI.Evaluation;
    using Backgammon.Logic.Rules;

    /// <summary>A position with its roller about to roll, and the rolls each seat had made before.</summary>
    internal readonly record struct TrainingState(Position Position, int Roller, RollCounts Rolls);
}
