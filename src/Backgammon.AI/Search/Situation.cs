namespace Backgammon.AI.Search
{
    using System;
    using System.Collections.Generic;

    using Backgammon.AI.Evaluation;
    using Backgammon.Logic;
    using Backgammon.Logic.Rules;

    /// <summary>
    /// What a bot knows at a decision, read from a seat view: the position, the stage to play and the stages known to
    /// follow it, whose roll it is, and the match score. Nothing else (no history, no future dice).
    /// </summary>
    internal sealed class Situation
    {
        public Situation(in Position position, int mover, StageDice stage, IReadOnlyList<StageDice> rest, bool isRemainder, bool isEscalating, RollCounts rolls, BackgammonVariant variant, int target, int moverScore, int otherScore, int gameNumber)
        {
            this.Position = position;
            this.Mover = mover;
            this.Stage = stage;
            this.Rest = rest;
            this.IsRemainder = isRemainder;
            this.IsEscalating = isEscalating;
            this.Rolls = rolls;
            this.Variant = variant;
            this.Target = target;
            this.MoverScore = moverScore;
            this.OtherScore = otherScore;
            this.GameNumber = gameNumber;
        }

        public Position Position { get; }

        public int Mover { get; }

        public StageDice Stage { get; }

        public IReadOnlyList<StageDice> Rest { get; }

        public bool IsRemainder { get; }

        public bool IsEscalating { get; }

        public RollCounts Rolls { get; }

        public BackgammonVariant Variant { get; }

        public int Target { get; }

        public int MoverScore { get; }

        public int OtherScore { get; }

        public int GameNumber { get; }

        public static Situation FromView(BackgammonSeatView view)
        {
            ArgumentNullException.ThrowIfNull(view);
            if ((view.ToMove != 0 && view.ToMove != 1) || view.StageDice.Count == 0)
            {
                throw new ArgumentException("Nobody is to move in this view.", nameof(view));
            }

            var rest = new List<StageDice>(view.ChainRest.Count);
            foreach (var stage in view.ChainRest)
            {
                rest.Add(StageDice.Same(stage.Die, stage.Count));
            }

            var mover = view.ToMove;
            return new Situation(
                ViewConverter.ToPosition(view.Version, view.Board),
                mover,
                ViewConverter.ToStageDice(view.StageDice),
                rest,
                view.IsPlayingRemainder,
                view.IsEscalating,
                new RollCounts(view.RollsMade.Count > 0 ? view.RollsMade[0] : 0, view.RollsMade.Count > 1 ? view.RollsMade[1] : 0),
                view.Variant,
                view.TargetPoints,
                view.Scores.Count == 2 ? view.Scores[mover] : 0,
                view.Scores.Count == 2 ? view.Scores[1 - mover] : 0,
                Math.Max(1, view.GameNumber));
        }
    }
}
