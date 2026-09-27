namespace Backgammon.Logic.Rules
{
    using System.Collections.Generic;

    /// <summary>
    /// Finds every distinct position a stage can end in. The stage rules are these:
    /// <list type="bullet">
    /// <item>A sequence of single steps is <i>complete</i> when it uses every die or when its last step ends the game
    /// (the last checker borne off, or a тапа майка or double-mother draw). Nothing may follow a step that ends the
    /// game.</item>
    /// <item>The legal sequences are those of the greatest effective length, a complete one counting as all the dice.</item>
    /// <item>With two different dice of which only one can be played, the larger one must be played if it can be.</item>
    /// </list>
    /// Each end comes with its canonical steps: the first legal sequence reaching it in canonical order (the higher
    /// point first, then the larger die). The ends are listed in the canonical order of those steps. Not thread-safe:
    /// keep one generator per thread and reuse it, since it keeps its buffers.
    /// </summary>
    internal sealed class StageGenerator
    {
        private readonly Dictionary<Position, int> endIndex = new();
        private readonly List<Candidate> candidates = new();
        private readonly HashSet<Position>[] visited = { new(), new(), new(), new(), new() };

        private int seat;
        private int die;
        private int diceCount;
        private int maxEffective;

        /// <summary>
        /// Fills <paramref name="ends"/> with the legal distinct ends of the stage and returns how many dice the
        /// legal sequences use: the effective length, where a game-ending sequence counts as all the dice. It is 0
        /// when nothing can be played; then the single end is the unchanged position with no steps.
        /// </summary>
        public int Generate(in Position start, int seat, StageDice dice, List<StageEnd> ends)
        {
            this.seat = seat;
            this.maxEffective = 0;
            this.endIndex.Clear();
            this.candidates.Clear();
            ends.Clear();

            if (dice.IsDistinct)
            {
                this.SearchDistinct(start, dice.High, dice.Low);
                if (this.maxEffective == 1)
                {
                    // Only one die can be played: the larger one if it can be, else the smaller one.
                    if (!this.AddSingleSteps(start, dice.High, ends))
                    {
                        this.AddSingleSteps(start, dice.Low, ends);
                    }

                    return 1;
                }
            }
            else
            {
                this.die = dice.High;
                this.diceCount = dice.Count;
                for (var depth = 0; depth < this.visited.Length; depth++)
                {
                    this.visited[depth].Clear();
                }

                this.SearchSame(start, 0, StepSequence.Empty);
            }

            foreach (var candidate in this.candidates)
            {
                if (candidate.Effective == this.maxEffective)
                {
                    ends.Add(new StageEnd(candidate.Position, candidate.Steps, candidate.End));
                }
            }

            ends.Sort(ByCanonicalSteps);
            return this.maxEffective;
        }

        private static int ByCanonicalSteps(StageEnd a, StageEnd b) => a.Steps.CompareTo(b.Steps);

        private void SearchSame(in Position position, int depth, StepSequence steps)
        {
            var moved = false;
            for (var from = Geometry.Bar; from >= 1; from--)
            {
                if (!StepRules.CanStep(position, this.seat, from, this.die))
                {
                    continue;
                }

                moved = true;
                var next = position;
                var end = StepRules.Apply(ref next, this.seat, from, this.die);
                var nextSteps = steps.Append(from, this.die);
                if (end != GameEnd.None || depth + 1 == this.diceCount)
                {
                    this.Record(next, nextSteps, this.diceCount, end);
                }
                else if (this.visited[depth + 1].Add(next))
                {
                    // A position reached before at this depth has the same future, reached by a canonically earlier
                    // prefix, so searching it again cannot find anything new.
                    this.SearchSame(next, depth + 1, nextSteps);
                }
            }

            if (!moved)
            {
                this.Record(position, steps, depth, GameEnd.None);
            }
        }

        private void SearchDistinct(in Position start, int high, int low)
        {
            var moved = false;
            for (var from = Geometry.Bar; from >= 1; from--)
            {
                for (var which = 0; which < 2; which++)
                {
                    var first = which == 0 ? high : low;
                    if (!StepRules.CanStep(start, this.seat, from, first))
                    {
                        continue;
                    }

                    moved = true;
                    var next = start;
                    var end = StepRules.Apply(ref next, this.seat, from, first);
                    var steps = StepSequence.Empty.Append(from, first);
                    if (end != GameEnd.None)
                    {
                        this.Record(next, steps, 2, end);
                        continue;
                    }

                    var second = which == 0 ? low : high;
                    var movedAgain = false;
                    for (var from2 = Geometry.Bar; from2 >= 1; from2--)
                    {
                        if (!StepRules.CanStep(next, this.seat, from2, second))
                        {
                            continue;
                        }

                        movedAgain = true;
                        var last = next;
                        var lastEnd = StepRules.Apply(ref last, this.seat, from2, second);
                        this.Record(last, steps.Append(from2, second), 2, lastEnd);
                    }

                    if (!movedAgain)
                    {
                        this.Record(next, steps, 1, GameEnd.None);
                    }
                }
            }

            if (!moved)
            {
                this.Record(start, StepSequence.Empty, 0, GameEnd.None);
            }
        }

        private bool AddSingleSteps(in Position start, int die, List<StageEnd> ends)
        {
            this.endIndex.Clear();
            for (var from = Geometry.Bar; from >= 1; from--)
            {
                if (!StepRules.CanStep(start, this.seat, from, die))
                {
                    continue;
                }

                var next = start;
                var end = StepRules.Apply(ref next, this.seat, from, die);
                if (this.endIndex.TryAdd(next, ends.Count))
                {
                    ends.Add(new StageEnd(next, StepSequence.Empty.Append(from, die), end));
                }
            }

            return ends.Count > 0;
        }

        private void Record(in Position position, StepSequence steps, int effective, GameEnd end)
        {
            if (effective > this.maxEffective)
            {
                this.maxEffective = effective;
            }

            if (this.endIndex.TryGetValue(position, out var index))
            {
                // The search runs in canonical order, so the first sequence of a given length is the canonical one;
                // only a longer one replaces it.
                if (effective > this.candidates[index].Effective)
                {
                    this.candidates[index] = new Candidate(position, steps, effective, end);
                }
            }
            else
            {
                this.endIndex.Add(position, this.candidates.Count);
                this.candidates.Add(new Candidate(position, steps, effective, end));
            }
        }

        private readonly record struct Candidate(Position Position, StepSequence Steps, int Effective, GameEnd End);
    }
}
