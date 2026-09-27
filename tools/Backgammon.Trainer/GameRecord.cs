namespace Backgammon.Trainer
{
    using System.Collections.Generic;

    using Backgammon.Logic;

    /// <summary>One self-play game: the positions at the start of each roll, and how the game ended.</summary>
    internal sealed class GameRecord
    {
        public List<TrainingState> States { get; } = new();

        public BackgammonGameResult? Result { get; set; }

        /// <summary>
        /// The final result as the network's three targets (win, win by 2, lose by 2) for seat 0, or null for a game
        /// with no winner that says nothing (stuck).
        /// </summary>
        public float[]? TargetForSeat0()
        {
            var result = this.Result!;
            return result.Kind switch
            {
                BackgammonResultKind.Stuck => null,
                BackgammonResultKind.Draw => new[] { 0.5f, 0f, 0f },
                _ => result.Winner == 0
                    ? new[] { 1f, result.Kind == BackgammonResultKind.Normal ? 0f : 1f, 0f }
                    : new[] { 0f, 0f, result.Kind == BackgammonResultKind.Normal ? 0f : 1f },
            };
        }
    }
}
