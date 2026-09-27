namespace Backgammon.Logic.Rules
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Checks a player's steps against the stage rules without listing every play: each step must be a legal single
    /// step with a die still unused, nothing may follow a step that ends the game, and the whole stage must use as
    /// many dice as the legal plays do (the larger die when only one of two can be played).
    /// </summary>
    internal static class StageChecker
    {
        /// <summary>Plays the steps in order; false when a step is illegal, uses a die not left, or follows the game's end.</summary>
        public static bool TryPlay(in Position start, int seat, StageDice dice, IReadOnlyList<BackgammonStep> steps, out Position end, out GameEnd gameEnd, Span<int> remaining, out int remainingCount)
        {
            end = start;
            gameEnd = GameEnd.None;
            remainingCount = Fill(dice, remaining);
            if (steps.Count > remainingCount)
            {
                return false;
            }

            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var slot = remaining[..remainingCount].IndexOf(step.Die);
                if (gameEnd != GameEnd.None || slot < 0 || !StepRules.CanStep(end, seat, step.From, step.Die))
                {
                    return false;
                }

                gameEnd = StepRules.Apply(ref end, seat, step.From, step.Die);
                remaining[slot] = remaining[remainingCount - 1];
                remainingCount--;
            }

            return true;
        }

        /// <summary>Whether the steps are a legal way to play the whole stage, given the dice the legal plays use.</summary>
        public static bool IsLegal(in Position start, int seat, StageDice dice, int playableDice, IReadOnlyList<BackgammonStep> steps, out Position end, out GameEnd gameEnd)
        {
            Span<int> remaining = stackalloc int[4];
            if (!TryPlay(start, seat, dice, steps, out end, out gameEnd, remaining, out _))
            {
                return false;
            }

            var effective = gameEnd != GameEnd.None ? dice.Count : steps.Count;
            if (effective != playableDice)
            {
                return false;
            }

            return !(dice.IsDistinct && playableDice == 1 && gameEnd == GameEnd.None && steps[0].Die != dice.High && CanPlayAny(start, seat, dice.High));
        }

        /// <summary>Whether the steps can be continued into a legal play of the whole stage.</summary>
        public static bool IsLegalPrefix(in Position start, int seat, StageDice dice, int playableDice, IReadOnlyList<BackgammonStep> steps)
        {
            Span<int> remaining = stackalloc int[4];
            if (!TryPlay(start, seat, dice, steps, out var end, out var gameEnd, remaining, out var remainingCount))
            {
                return false;
            }

            if (gameEnd != GameEnd.None)
            {
                // A game-ending play is complete; it is legal whenever one exists, since then every die counts as used.
                return playableDice == dice.Count;
            }

            if (steps.Count + MaxCompletion(end, seat, remaining[..remainingCount]) != playableDice)
            {
                return false;
            }

            return !(dice.IsDistinct && playableDice == 1 && steps.Count == 1 && steps[0].Die != dice.High && CanPlayAny(start, seat, dice.High));
        }

        /// <summary>
        /// How many of the remaining dice the best continuation uses. A continuation that ends the game counts as using
        /// them all.
        /// </summary>
        public static int MaxCompletion(in Position position, int seat, ReadOnlySpan<int> remaining)
        {
            if (remaining.Length == 0)
            {
                return 0;
            }

            var best = 0;
            Span<int> rest = stackalloc int[4];
            for (var i = 0; i < remaining.Length; i++)
            {
                var die = remaining[i];
                if (remaining[..i].Contains(die))
                {
                    continue;
                }

                remaining[..i].CopyTo(rest);
                remaining[(i + 1)..].CopyTo(rest[i..]);
                for (var from = Geometry.Bar; from >= 1; from--)
                {
                    if (!StepRules.CanStep(position, seat, from, die))
                    {
                        continue;
                    }

                    var next = position;
                    var end = StepRules.Apply(ref next, seat, from, die);
                    var used = end != GameEnd.None ? remaining.Length : 1 + MaxCompletion(next, seat, rest[..(remaining.Length - 1)]);
                    if (used > best)
                    {
                        best = used;
                        if (best == remaining.Length)
                        {
                            return best;
                        }
                    }
                }
            }

            return best;
        }

        public static bool CanPlayAny(in Position position, int seat, int die)
        {
            for (var from = Geometry.Bar; from >= 1; from--)
            {
                if (StepRules.CanStep(position, seat, from, die))
                {
                    return true;
                }
            }

            return false;
        }

        private static int Fill(StageDice dice, Span<int> remaining)
        {
            if (dice.IsDistinct)
            {
                remaining[0] = dice.High;
                remaining[1] = dice.Low;
                return 2;
            }

            for (var i = 0; i < dice.Count; i++)
            {
                remaining[i] = dice.High;
            }

            return dice.Count;
        }
    }
}
