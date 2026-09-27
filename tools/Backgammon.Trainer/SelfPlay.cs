namespace Backgammon.Trainer
{
    using System;
    using System.Collections.Generic;

    using Backgammon.AI.Evaluation;
    using Backgammon.AI.Search;
    using Backgammon.Logic;
    using Backgammon.Logic.Rules;

    /// <summary>
    /// Plays self-play games with a network, through the engine's own <see cref="BackgammonMatch"/> so the rules can
    /// never drift from the engine's.
    /// <list type="bullet">
    /// <item>Every play is the one with the best expected points for the mover, from the network's value of each
    /// distinct end (0-ply, a win counted exactly).</item>
    /// <item>The training states are the positions at the start of each roll, with the roller about to roll.</item>
    /// </list>
    /// </summary>
    internal sealed class SelfPlay
    {
        private readonly IEvaluator evaluator;

        public SelfPlay(IEvaluator evaluator)
        {
            this.evaluator = evaluator;
        }

        /// <summary>Plays a match to 1 point (one game, or more after a тапа draw); returns its games.</summary>
        public List<GameRecord> PlayMatch(BackgammonVersion version, int seed, double exploration)
        {
            var dice = new Random(seed);
            var noise = new Random(seed ^ 0x5bd1e995);
            var match = new BackgammonMatch(new BackgammonMatchOptions
            {
                Variant = (BackgammonVariant)(int)version,
                TargetPoints = 1,
                RecordHistory = false,
                Dice = (n, _) => dice.Next(n),
            });
            var games = new List<GameRecord>();
            var current = new GameRecord();
            match.Start();
            var gameNumber = match.GameNumber;
            while (!match.IsFinished)
            {
                var state = match.CurrentGame!;
                var ends = match.CurrentEnds;
                var mover = state.Mover;
                var rolls = new RollCounts(state.RollsMade[0], state.RollsMade[1]);
                if (state.StageIndex == 0 && !state.IsRemainder)
                {
                    // The position before this roll, with its roller about to roll.
                    current.States.Add(new TrainingState(state.Position, mover, mover == 0 ? rolls with { Seat0 = rolls.Seat0 - 1 } : rolls with { Seat1 = rolls.Seat1 - 1 }));
                }

                var choice = exploration > 0 && noise.NextDouble() < exploration ? noise.Next(ends.Count) : this.Best(state, ends, rolls);
                match.Act(mover, new BackgammonAction { Steps = ends[choice].Steps.ToSteps() });

                if (match.IsFinished || match.GameNumber != gameNumber)
                {
                    var result = match.GetView(0).Results[^1];
                    current.Result = result;
                    games.Add(current);
                    current = new GameRecord();
                    gameNumber = match.GameNumber;
                }
            }

            return games;
        }

        /// <summary>The end with the best expected points for the mover.</summary>
        public int Best(GameState state, List<StageEnd> ends, RollCounts rolls)
        {
            var mover = state.Mover;

            // Who rolls after a play that does not end the game, as the bots see it.
            var moverRollsNext = Chooser.OnRollAfter(state.IsRemainder, mover) == mover;
            var best = 0;
            var bestValue = double.NegativeInfinity;
            for (var i = 0; i < ends.Count; i++)
            {
                var end = ends[i];
                double value;
                if (end.End == GameEnd.BorneOff)
                {
                    value = end.Position.Count(1 - mover, Geometry.Off) == 0 ? 2 : 1;
                }
                else if (end.End == GameEnd.Mother)
                {
                    value = 2;
                }
                else if (end.End == GameEnd.BothMothers)
                {
                    value = 0;
                }
                else if (moverRollsNext)
                {
                    value = this.evaluator.Evaluate(end.Position, mover, rolls).Points();
                }
                else
                {
                    value = -this.evaluator.Evaluate(end.Position, 1 - mover, rolls).Points();
                }

                if (value > bestValue)
                {
                    bestValue = value;
                    best = i;
                }
            }

            return best;
        }
    }
}
