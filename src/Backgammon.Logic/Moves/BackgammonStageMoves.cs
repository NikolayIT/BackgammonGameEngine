namespace Backgammon.Logic
{
    using System;
    using System.Collections.Generic;

    using Backgammon.Logic.Rules;

    /// <summary>
    /// The legal moves of the stage to play now, for user interfaces. It gives the distinct positions the stage can end
    /// in, how many dice the legal plays use, and, after a partial stage (the steps a player has made so far), the
    /// legal next steps and where a checker can go. Get it from <see cref="BackgammonMatch.GetStageMoves"/> or, from a
    /// view alone, with <see cref="For"/>.
    /// </summary>
    public sealed class BackgammonStageMoves
    {
        private static readonly BackgammonStep[] NoSteps = Array.Empty<BackgammonStep>();

        private readonly Position start;
        private readonly StageDice dice;
        private readonly List<StageEnd> ends;
        private IReadOnlyList<BackgammonStageOutcome>? outcomes;

        internal BackgammonStageMoves(in Position start, int seat, StageDice dice, int playableDice, List<StageEnd> ends, IReadOnlyList<int> diceList)
        {
            this.start = start;
            this.Seat = seat;
            this.dice = dice;
            this.PlayableDice = playableDice;
            this.ends = ends;
            this.Dice = diceList;
        }

        /// <summary>Gets the seat to play the stage.</summary>
        public int Seat { get; }

        /// <summary>Gets the rules of the game.</summary>
        public BackgammonVersion Version => this.start.Version;

        /// <summary>Gets the stage's dice.</summary>
        public IReadOnlyList<int> Dice { get; }

        /// <summary>
        /// Gets how many dice every legal play of the stage uses: 0 when nothing can be played. A play that ends the game
        /// counts as using them all, even when it needs fewer steps.
        /// </summary>
        public int PlayableDice { get; }

        /// <summary>Gets the number of distinct positions the stage can end in.</summary>
        public int OutcomeCount => this.ends.Count;

        /// <summary>
        /// Gets the distinct positions the stage can end in, each with its canonical steps, in the canonical order of those
        /// steps. With no legal move there is one outcome: the unchanged board, with no steps.
        /// </summary>
        public IReadOnlyList<BackgammonStageOutcome> Outcomes => this.outcomes ??= this.BuildOutcomes();

        /// <summary>Works out the stage's moves from a view alone: its board, version, seat to move and stage dice.</summary>
        /// <param name="view">A view of a match in which a seat is to move.</param>
        /// <returns>The moves of the stage <paramref name="view"/>'s seat to move has to play.</returns>
        public static BackgammonStageMoves For(BackgammonSeatView view)
        {
            ArgumentNullException.ThrowIfNull(view);
            if (view.ToMove != 0 && view.ToMove != 1)
            {
                throw new ArgumentException("Nobody is to move in this view.", nameof(view));
            }

            var position = ViewConverter.ToPosition(view.Version, view.Board);
            var dice = ViewConverter.ToStageDice(view.StageDice);
            var ends = new List<StageEnd>();
            var playable = new StageGenerator().Generate(position, view.ToMove, dice, ends);
            return new BackgammonStageMoves(position, view.ToMove, dice, playable, ends, view.StageDice);
        }

        /// <summary>Whether <paramref name="steps"/> is a legal way to play the whole stage.</summary>
        /// <param name="steps">The steps, in the mover's numbering and in order.</param>
        /// <returns>True when the steps play the stage legally.</returns>
        public bool IsLegal(IReadOnlyList<BackgammonStep> steps)
        {
            ArgumentNullException.ThrowIfNull(steps);
            return steps.Count <= this.dice.Count && StageChecker.IsLegal(this.start, this.Seat, this.dice, this.PlayableDice, steps, out _, out _);
        }

        /// <summary>Whether <paramref name="partial"/> can be continued into a legal play of the whole stage.</summary>
        /// <param name="partial">The steps made so far, in order.</param>
        /// <returns>True when the partial stage is the start of a legal play.</returns>
        public bool IsLegalPrefix(IReadOnlyList<BackgammonStep> partial)
        {
            ArgumentNullException.ThrowIfNull(partial);
            return partial.Count <= this.dice.Count && StageChecker.IsLegalPrefix(this.start, this.Seat, this.dice, this.PlayableDice, partial);
        }

        /// <summary>
        /// How many more dice a legal play starting with <paramref name="partial"/> uses: 0 when the partial stage is
        /// already complete (or has ended the game).
        /// </summary>
        /// <param name="partial">The steps made so far; they must be the start of a legal play.</param>
        /// <returns>The number of dice still to play.</returns>
        public int DiceLeft(IReadOnlyList<BackgammonStep> partial)
        {
            if (!this.IsLegalPrefix(partial))
            {
                throw new ArgumentException("The steps are not the start of a legal play.", nameof(partial));
            }

            Span<int> remaining = stackalloc int[4];
            StageChecker.TryPlay(this.start, this.Seat, this.dice, partial, out _, out var gameEnd, remaining, out _);
            return gameEnd != GameEnd.None ? 0 : this.PlayableDice - partial.Count;
        }

        /// <summary>The legal next steps after <paramref name="partial"/>, in canonical order (empty when none is left).</summary>
        /// <param name="partial">The steps made so far, in order; empty at the start of the stage.</param>
        /// <returns>Every step that keeps the stage on the way to a legal play.</returns>
        public IReadOnlyList<BackgammonStepOption> NextSteps(IReadOnlyList<BackgammonStep>? partial = null)
        {
            partial ??= NoSteps;
            var options = new List<BackgammonStepOption>();
            if (!this.IsLegalPrefix(partial))
            {
                return options;
            }

            Span<int> remaining = stackalloc int[4];
            StageChecker.TryPlay(this.start, this.Seat, this.dice, partial, out var position, out var gameEnd, remaining, out var remainingCount);
            if (gameEnd != GameEnd.None || remainingCount == 0)
            {
                return options;
            }

            var candidate = new BackgammonStep[partial.Count + 1];
            for (var i = 0; i < partial.Count; i++)
            {
                candidate[i] = partial[i];
            }

            for (var from = Geometry.Bar; from >= 1; from--)
            {
                for (var die = 6; die >= 1; die--)
                {
                    if (!remaining[..remainingCount].Contains(die) || !StepRules.CanStep(position, this.Seat, from, die))
                    {
                        continue;
                    }

                    candidate[partial.Count] = new BackgammonStep(from, die);
                    if (StageChecker.IsLegalPrefix(this.start, this.Seat, this.dice, this.PlayableDice, candidate))
                    {
                        options.Add(this.Describe(position, from, die));
                    }
                }
            }

            return options;
        }

        /// <summary>The legal next steps after <paramref name="partial"/> that move a checker from <paramref name="from"/>.</summary>
        /// <param name="partial">The steps made so far, in order.</param>
        /// <param name="from">The point (in the mover's numbering) or 25 for the bar.</param>
        /// <returns>The steps from that point, one per die that can be used.</returns>
        public IReadOnlyList<BackgammonStepOption> Destinations(IReadOnlyList<BackgammonStep>? partial, int from)
        {
            var options = new List<BackgammonStepOption>();
            foreach (var option in this.NextSteps(partial))
            {
                if (option.Step.From == from)
                {
                    options.Add(option);
                }
            }

            return options;
        }

        private BackgammonStepOption Describe(in Position position, int from, int die)
        {
            var version = position.Version;
            var to = Math.Max(0, from - die);
            var captures = false;
            if (to >= 1 && version != BackgammonVersion.Gyulbara)
            {
                var other = Geometry.Other(version, to);
                captures = position.Count(1 - this.Seat, other) == 1 && !position.IsPinned(1 - this.Seat, other);
            }

            return new BackgammonStepOption
            {
                Step = new BackgammonStep(from, die),
                To = to,
                Seat0From = from == Geometry.Bar ? Geometry.Bar : Geometry.ToSeat0(version, this.Seat, from),
                Seat0To = to == 0 ? 0 : Geometry.ToSeat0(version, this.Seat, to),
                Captures = captures,
            };
        }

        private IReadOnlyList<BackgammonStageOutcome> BuildOutcomes()
        {
            var outcomes = new BackgammonStageOutcome[this.ends.Count];
            for (var i = 0; i < outcomes.Length; i++)
            {
                var end = this.ends[i];
                outcomes[i] = new BackgammonStageOutcome
                {
                    Board = ViewConverter.ToBoard(end.Position),
                    Steps = end.Steps.ToSteps(),
                    EndsGame = end.End != GameEnd.None,
                };
            }

            return outcomes;
        }
    }
}
