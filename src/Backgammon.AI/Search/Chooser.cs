namespace Backgammon.AI.Search
{
    using System;
    using System.Collections.Generic;

    using Backgammon.AI.Evaluation;
    using Backgammon.Logic.Rules;

    /// <summary>
    /// Picks a play for the stage in a <see cref="Situation"/>.
    /// <list type="bullet">
    /// <item>Every distinct end position is valued by the evaluator, as a match-winning chance through
    /// <see cref="MatchEquity"/>, from the point where somebody rolls next.</item>
    /// <item>When the turn does not end with the stage, the stages known to follow are played out: the rest of the
    /// bot's own escalating chain or remainder, or the remainder it hands to the opponent. Those are played greedily
    /// for the few most promising ends, the opponent answering for itself.</item>
    /// </list>
    /// No clock is involved: the work is capped by a count of evaluations, so a decision depends only on the situation,
    /// the settings and the <see cref="Random"/>. One chooser per thread; it keeps its buffers.
    /// </summary>
    internal sealed class Chooser
    {
        [ThreadStatic]
        private static Chooser? perThread;

        private readonly StageGenerator generator = new();
        private readonly List<StageEnd>[] lists = { new(), new(), new(), new(), new(), new(), new(), new() };
        private readonly List<double> scores = new();
        private readonly List<int> order = new();

        private Situation situation = null!;
        private IEvaluator evaluator = null!;
        private (double WinSingle, double WinDouble, double LoseSingle, double LoseDouble, double Draw) after;
        private int evaluations;

        // Evaluations beyond this count cut the deep search short (see Deepen).
        private int limit = int.MaxValue;

        public static Chooser Current => perThread ??= new Chooser();

        public int Evaluations => this.evaluations;

        private bool Exhausted => this.evaluations > this.limit;

        /// <summary>
        /// Who rolls next after a stage that does not end the game, valuing it as if the turn ended there: after a stage
        /// of a remainder the mover does, since it rolls as soon as the remainder is played; after the mover's own roll
        /// or chain the opponent does.
        /// </summary>
        public static int OnRollAfter(bool isRemainder, int mover) => isRemainder ? mover : 1 - mover;

        /// <summary>
        /// Chooses among the stage's distinct ends; returns them (in canonical order) and the index of the choice.
        /// </summary>
        public (List<StageEnd> Ends, int Choice) Choose(Situation situation, IEvaluator evaluator, SearchSettings settings, Random? random)
        {
            this.situation = situation;
            this.evaluator = evaluator;
            this.evaluations = 0;
            this.limit = int.MaxValue;
            var ends = this.lists[0];
            var playable = this.generator.Generate(situation.Position, situation.Mover, situation.Stage, ends);
            if (ends.Count == 1)
            {
                return (ends, 0);
            }

            this.after = MatchEquity.For(situation.Variant, situation.Target).AfterGame(situation.MoverScore, situation.OtherScore, situation.GameNumber);
            var next = this.Continuation(playable);

            // First, every end valued as if the turn ended with the stage (or exactly, when it does).
            this.scores.Clear();
            this.order.Clear();
            for (var i = 0; i < ends.Count; i++)
            {
                var end = ends[i];
                this.scores.Add(end.End != GameEnd.None
                    ? this.Terminal(end.Position, end.End, situation.Mover)
                    : this.Quiet(end.Position, OnRollAfter(situation.IsRemainder, situation.Mover)));
                this.order.Add(i);
            }

            // Then the few best are played on through the known stages that follow, or, when the turn ends, looked at
            // one roll deeper.
            if (settings.LookAhead > 0 && next.Kind is ContinuationKind.OwnStages or ContinuationKind.OpponentRemainder)
            {
                this.Deepen(ends, settings.LookAhead, settings.EvaluationBudget, index => next.Kind == ContinuationKind.OwnStages
                    ? this.OwnStages(ends[index].Position, next.Stages, 0, situation.IsRemainder, 1)
                    : this.OpponentRemainder(ends[index].Position, next.Stages, 1));
            }
            else if (settings.OneRoll > 0 && next.Kind == ContinuationKind.OpponentRolls)
            {
                this.Deepen(ends, settings.OneRoll, settings.EvaluationBudget, index => this.OneRoll(ends[index].Position));
            }

            var choice = 0;
            var top = double.NegativeInfinity;
            for (var i = 0; i < ends.Count; i++)
            {
                var score = this.scores[i] + (settings.Noise > 0 && random != null ? settings.Noise * Gaussian(random) : 0);
                if (score > top)
                {
                    top = score;
                    choice = i;
                }
            }

            return (ends, choice);
        }

        private static double Gaussian(Random random)
        {
            // Box-Muller, from the bot's own Random.
            var u = 1 - random.NextDouble();
            var v = random.NextDouble();
            return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v);
        }

        /// <summary>
        /// Replaces the first values of the <paramref name="count"/> best non-final ends with deeper ones. The deeper
        /// search may use <paramref name="budget"/> evaluations. An end whose search runs over keeps its first value,
        /// and the search stops there. That bounds the time of every decision without a clock, so the result stays
        /// deterministic. The ends not searched keep their first values but may not beat a searched end on them alone.
        /// </summary>
        private void Deepen(List<StageEnd> ends, int count, int budget, Func<int, double> deeper)
        {
            this.order.Sort(this.ByScoreDescending);
            this.limit = this.evaluations + budget;
            var best = double.NegativeInfinity;
            var searched = 0;
            for (var rank = 0; rank < Math.Min(count, this.order.Count); rank++)
            {
                var index = this.order[rank];
                if (ends[index].End != GameEnd.None)
                {
                    searched = rank + 1;
                    continue;
                }

                var value = deeper(index);
                if (this.Exhausted)
                {
                    break;
                }

                this.scores[index] = value;
                best = Math.Max(best, value);
                searched = rank + 1;
            }

            this.limit = int.MaxValue;
            for (var rank = searched; rank < this.order.Count && !double.IsNegativeInfinity(best); rank++)
            {
                var index = this.order[rank];
                if (ends[index].End == GameEnd.None)
                {
                    this.scores[index] = Math.Min(this.scores[index], best - 1e-9);
                }
            }
        }

        private int ByScoreDescending(int a, int b)
        {
            var byScore = this.scores[b].CompareTo(this.scores[a]);
            return byScore != 0 ? byScore : a.CompareTo(b);
        }

        /// <summary>What follows a non-final play of the current stage (all legal plays use the same number of dice).</summary>
        private (ContinuationKind Kind, List<StageDice> Stages) Continuation(int playable)
        {
            var s = this.situation;
            var stages = new List<StageDice>();
            if (s.IsRemainder)
            {
                stages.AddRange(s.Rest);
                return (stages.Count > 0 ? ContinuationKind.OwnStages : ContinuationKind.MoverRolls, stages);
            }

            if (!s.IsEscalating)
            {
                return (ContinuationKind.OpponentRolls, stages);
            }

            if (playable == s.Stage.Count)
            {
                stages.AddRange(s.Rest);
                return (stages.Count > 0 ? ContinuationKind.OwnStages : ContinuationKind.OpponentRolls, stages);
            }

            // The chain breaks here: the rest goes to the opponent.
            stages.Add(StageDice.Same(s.Stage.High, s.Stage.Count - playable));
            stages.AddRange(s.Rest);
            return (ContinuationKind.OpponentRemainder, stages);
        }

        /// <summary>The mover plays its own known stages on, greedily; returns its match-winning chance.</summary>
        private double OwnStages(in Position position, List<StageDice> stages, int index, bool isRemainder, int depth)
        {
            if (index == stages.Count)
            {
                return this.Quiet(position, OnRollAfter(isRemainder, this.situation.Mover));
            }

            var mover = this.situation.Mover;
            var stage = stages[index];
            var ends = this.lists[Math.Min(depth, this.lists.Length - 1)];
            var playable = this.generator.Generate(position, mover, stage, ends);
            if (playable == 0 && !isRemainder)
            {
                // The chain breaks with this stage untouched: all of it and what follows goes to the opponent.
                return this.OpponentRemainder(position, stages.GetRange(index, stages.Count - index), depth + 1);
            }

            // Greedy: the end that looks best if the turn ended there (a win is taken at once).
            var bestIndex = 0;
            var best = double.NegativeInfinity;
            var rollerAfter = OnRollAfter(isRemainder, mover);
            for (var i = 0; i < ends.Count; i++)
            {
                var value = ends[i].End != GameEnd.None ? this.Terminal(ends[i].Position, ends[i].End, mover) : this.Quiet(ends[i].Position, rollerAfter);
                if (value > best)
                {
                    best = value;
                    bestIndex = i;
                }
            }

            var chosen = ends[bestIndex];
            if (chosen.End != GameEnd.None || this.Exhausted)
            {
                return best;
            }

            if (isRemainder || playable == stage.Count)
            {
                return this.OwnStages(chosen.Position, stages, index + 1, isRemainder, depth + 1);
            }

            var remainder = new List<StageDice> { StageDice.Same(stage.High, stage.Count - playable) };
            remainder.AddRange(stages.GetRange(index + 1, stages.Count - index - 1));
            return this.OpponentRemainder(chosen.Position, remainder, depth + 1);
        }

        /// <summary>
        /// The opponent plays a remainder, stage by stage, greedily for itself, then rolls; returns the mover's
        /// match-winning chance.
        /// </summary>
        private double OpponentRemainder(in Position start, List<StageDice> stages, int depth)
        {
            var opponent = 1 - this.situation.Mover;
            var position = start;
            foreach (var stage in stages)
            {
                if (this.Exhausted)
                {
                    return 0;
                }

                var ends = this.lists[Math.Min(depth, this.lists.Length - 1)];
                this.generator.Generate(position, opponent, stage, ends);
                var bestIndex = 0;
                var worst = double.PositiveInfinity;
                for (var i = 0; i < ends.Count; i++)
                {
                    var value = ends[i].End != GameEnd.None ? this.Terminal(ends[i].Position, ends[i].End, opponent) : this.Quiet(ends[i].Position, opponent);
                    if (value < worst)
                    {
                        worst = value;
                        bestIndex = i;
                    }
                }

                if (ends[bestIndex].End != GameEnd.None)
                {
                    return worst;
                }

                position = ends[bestIndex].Position;
            }

            return this.Quiet(position, opponent);
        }

        /// <summary>
        /// The mover's match-winning chance after the opponent rolls from <paramref name="position"/>. For each of the
        /// 21 rolls the opponent makes its best play by the same evaluator, weighted by the roll's chance. When the
        /// opponent's doubles escalate, it plays the chain greedily stage by stage. A chain it cannot finish is
        /// simply stopped there: the remainder the mover would get is left out, a small bias against the mover.
        /// </summary>
        private double OneRoll(in Position position)
        {
            var mover = this.situation.Mover;
            var opponent = 1 - mover;
            var escalates = this.situation.Rolls.NextEscalates(this.situation.Position, opponent);
            var rolls = this.situation.Rolls.After(opponent);
            var ends = this.lists[1];
            var total = 0.0;
            for (var high = 1; high <= 6; high++)
            {
                for (var low = 1; low <= high; low++)
                {
                    if (this.Exhausted)
                    {
                        return 0;
                    }

                    var weight = (high == low ? 1.0 : 2.0) / 36;
                    if (high != low || !escalates)
                    {
                        total += weight * this.BestReply(position, high == low ? StageDice.Same(high, 4) : StageDice.Distinct(high, low), ends, rolls, out _, out _);
                        continue;
                    }

                    // An escalating double: four of each die from this one up to 6, stage by stage.
                    var current = position;
                    var value = 0.0;
                    for (var die = high; die <= 6; die++)
                    {
                        value = this.BestReply(current, StageDice.Same(die, 4), ends, rolls, out var chosen, out var complete);
                        if (!complete)
                        {
                            break;
                        }

                        current = chosen;
                    }

                    total += weight * value;
                }
            }

            return total;
        }

        /// <summary>
        /// The opponent's best play of a stage, by the mover's match-winning chance with the mover to roll next. It
        /// gives the position chosen, and whether the stage was fully played and the game goes on (so a chain can
        /// continue).
        /// </summary>
        private double BestReply(in Position position, StageDice dice, List<StageEnd> ends, RollCounts rolls, out Position chosen, out bool complete)
        {
            var mover = this.situation.Mover;
            var playable = this.generator.Generate(position, 1 - mover, dice, ends);
            var worst = double.PositiveInfinity;
            var best = 0;
            for (var i = 0; i < ends.Count; i++)
            {
                var value = ends[i].End != GameEnd.None ? this.Terminal(ends[i].Position, ends[i].End, 1 - mover) : this.Quiet(ends[i].Position, mover, rolls);
                if (value < worst)
                {
                    worst = value;
                    best = i;
                }
            }

            chosen = ends[best].Position;
            complete = playable == dice.Count && ends[best].End == GameEnd.None;
            return worst;
        }

        /// <summary>The mover's match-winning chance in a quiet position with <paramref name="onRoll"/> to roll.</summary>
        private double Quiet(in Position position, int onRoll) => this.Quiet(position, onRoll, this.situation.Rolls);

        private double Quiet(in Position position, int onRoll, RollCounts rolls)
        {
            this.evaluations++;
            if (this.Exhausted)
            {
                // Over budget: the deep value being built will be thrown away, so do not spend time on it.
                return 0;
            }

            var outcome = this.evaluator.Evaluate(position, onRoll, rolls);
            return this.Equity(onRoll == this.situation.Mover ? outcome : outcome.Flip());
        }

        /// <summary>The mover's match-winning chance when a step by <paramref name="endedBy"/> ended the game.</summary>
        private double Terminal(in Position position, GameEnd end, int endedBy)
        {
            var outcome = end switch
            {
                GameEnd.BorneOff => Outcome.Won(position.Count(1 - endedBy, Geometry.Off) == 0),
                GameEnd.Mother => Outcome.Won(doubled: true),
                _ => Outcome.Drawn,
            };

            return this.Equity(endedBy == this.situation.Mover || end == GameEnd.BothMothers ? outcome : outcome.Flip());
        }

        private double Equity(in Outcome outcome) =>
            (outcome.WinSingle * this.after.WinSingle) + (outcome.WinDouble * this.after.WinDouble) + (outcome.LoseSingle * this.after.LoseSingle)
            + (outcome.LoseDouble * this.after.LoseDouble) + (outcome.Draw * this.after.Draw);
    }
}
