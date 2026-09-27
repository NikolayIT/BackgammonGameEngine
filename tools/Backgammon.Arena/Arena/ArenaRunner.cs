namespace Backgammon.Arena
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    using Backgammon.Logic;

    /// <summary>
    /// Plays arena matches. Every action a player makes is validated first: an illegal one is counted and fails the run.
    /// Matches are played in duplicate pairs: the same dice seed twice, with the players swapping seats, which cancels
    /// much of the luck.
    /// </summary>
    internal static class ArenaRunner
    {
        public static MatchStats Play(BackgammonVariant variant, ArenaPlayer seat0, ArenaPlayer seat1, int diceSeed, int botSeed, bool timeDecisions)
        {
            var dice = new Random(diceSeed);
            var match = new BackgammonMatch(new BackgammonMatchOptions { Variant = variant, Dice = (n, _) => dice.Next(n), RecordHistory = false });
            var players = new[] { seat0, seat1 };
            var randoms = new[] { new Random(botSeed * 2), new Random((botSeed * 2) + 1) };
            var stats = new MatchStats();
            match.Start();
            while (!match.IsFinished)
            {
                var seat = match.ToMove;
                var view = match.GetView(seat);
                var started = timeDecisions ? Stopwatch.GetTimestamp() : 0;
                var action = players[seat].Choose(view, randoms[seat]);
                if (timeDecisions)
                {
                    stats.DecisionMicroseconds[seat].Add(Stopwatch.GetElapsedTime(started).TotalMicroseconds);
                }

                stats.Decisions[seat]++;
                if (match.Validate(seat, action) != BackgammonActResult.Ok)
                {
                    stats.Illegal++;
                    throw new InvalidOperationException($"{players[seat].Name} made an illegal play: {action} in {view.Version} game {view.GameNumber}.");
                }

                match.Act(seat, action);
            }

            var results = match.GetView(0).Results;
            stats.Winner = match.Winner;
            stats.Scores = new[] { match.Scores[0], match.Scores[1] };
            stats.Games = results.Count;
            stats.Plies = match.Ply;
            foreach (var result in results)
            {
                stats.Kinds[(int)result.Kind]++;
            }

            return stats;
        }

        /// <summary>Plays <paramref name="pairs"/> duplicate pairs of matches between two players.</summary>
        public static PairStats PlayPairs(BackgammonVariant variant, ArenaPlayer a, ArenaPlayer b, int pairs, int threads, int seedBase = 1)
        {
            var total = new PairStats(a.Name, b.Name);
            var gate = new object();
            Parallel.For(0, pairs, new ParallelOptions { MaxDegreeOfParallelism = threads }, pair =>
            {
                var seed = seedBase + pair;
                var first = Play(variant, a, b, seed, seed, timeDecisions: false);
                var second = Play(variant, b, a, seed, seed + 1_000_000, timeDecisions: false);
                lock (gate)
                {
                    total.Add(first, seatOfA: 0);
                    total.Add(second, seatOfA: 1);
                }
            });

            return total;
        }
    }
}
