namespace Backgammon.Logic.Tests.Support
{
    using System;
    using System.Collections.Generic;

    using Backgammon.Logic.Rules;

    /// <summary>
    /// Positions to test the move generator on. Position i of a source is a function of (seed, i) alone, so a
    /// failure can be reproduced from its index. Every position is a live one: nobody has won, and the seat is the
    /// one to move.
    /// </summary>
    internal static class PositionSources
    {
        public static readonly StageDice[] AllRolls = BuildAllRolls();

        /// <summary>
        /// A position reached by random play from the start. It plays a random number of turns, taking a uniformly
        /// random legal end of every stage, so both quiet and wild positions come up.
        /// </summary>
        public static (Position Position, int Seat) Reachable(BackgammonVersion version, int seed, int index)
        {
            var random = new Random(HashCode.Combine(seed, index, 17));
            var generator = new StageGenerator();
            var ends = new List<StageEnd>();
            while (true)
            {
                var position = Position.Start(version);
                var seat = random.Next(2);
                var turns = random.Next(0, 160);
                var finished = false;
                for (var turn = 0; turn < turns && !finished; turn++)
                {
                    finished = PlayRandomTurn(ref position, seat, random, generator, ends);
                    seat = 1 - seat;
                }

                if (!finished)
                {
                    return (position, seat);
                }
            }
        }

        /// <summary>
        /// A random valid position, not necessarily reachable in a real game, with a few random turns played from it
        /// so that hits, pins and blocks come up as well. Some positions have a seat with everything home (bearing
        /// off), checkers on the bar, a few checkers left, or тапа mothers alone on their start.
        /// </summary>
        public static (Position Position, int Seat) RandomValid(BackgammonVersion version, int seed, int index)
        {
            var random = new Random(HashCode.Combine(seed, index, 29));
            var generator = new StageGenerator();
            var ends = new List<StageEnd>();
            while (true)
            {
                var position = Scatter(version, random);
                var seat = random.Next(2);
                var turns = random.Next(0, 4) == 0 ? 0 : random.Next(1, 12);
                var finished = false;
                for (var turn = 0; turn < turns && !finished; turn++)
                {
                    finished = PlayRandomTurn(ref position, seat, random, generator, ends);
                    seat = 1 - seat;
                }

                if (!finished && !IsOver(position))
                {
                    return (position, seat);
                }
            }
        }

        /// <summary>Plays one random roll (doubles as four equal dice) and returns whether the game ended.</summary>
        public static bool PlayRandomTurn(ref Position position, int seat, Random random, StageGenerator generator, List<StageEnd> ends)
        {
            var first = random.Next(1, 7);
            var second = random.Next(1, 7);
            var dice = first == second ? StageDice.Same(first, 4) : StageDice.Distinct(first, second);
            generator.Generate(position, seat, dice, ends);
            var end = ends[random.Next(ends.Count)];
            position = end.Position;
            return end.End != GameEnd.None;
        }

        private static bool IsOver(in Position position)
        {
            if (position.Count(0, Geometry.Off) == 15 || position.Count(1, Geometry.Off) == 15)
            {
                return true;
            }

            return position.Version == BackgammonVersion.Tapa
                && (StepRules.MotherEnd(position, 0) != GameEnd.None || StepRules.MotherEnd(position, 1) != GameEnd.None);
        }

        private static Position Scatter(BackgammonVersion version, Random random)
        {
            while (true)
            {
                var position = new Position { Version = version };
                var ok = true;
                for (var seat = 0; seat < 2 && ok; seat++)
                {
                    var off = random.Next(3) == 0 ? random.Next(0, 15) : 0;
                    var bar = Geometry.HasBar(version) && random.Next(4) == 0 ? random.Next(1, 4) : 0;
                    position.Set(seat, Geometry.Off, off);
                    position.Set(seat, Geometry.Bar, bar);
                    var left = 15 - off - bar;
                    var mode = random.Next(5);
                    var (low, high) = mode switch
                    {
                        0 => (1, 6),
                        1 => (1, 12),
                        2 => (13, 24),
                        _ => (1, 24),
                    };

                    if (version == BackgammonVersion.Tapa && random.Next(4) == 0 && left > 0)
                    {
                        // A mother: exactly one checker left on the start.
                        if (!TryPlace(ref position, seat, 24))
                        {
                            ok = false;
                            break;
                        }

                        left--;
                        high = Math.Min(high, 23);
                        low = Math.Min(low, high);
                    }

                    for (var checker = 0; checker < left && ok; checker++)
                    {
                        var placed = false;
                        for (var attempt = 0; attempt < 50 && !placed; attempt++)
                        {
                            placed = TryPlace(ref position, seat, random.Next(low, high + 1));
                        }

                        ok = placed;
                    }
                }

                if (ok)
                {
                    return position;
                }
            }
        }

        private static bool TryPlace(ref Position position, int seat, int point)
        {
            if (position.Count(1 - seat, Geometry.Other(position.Version, point)) != 0)
            {
                return false;
            }

            position.Add(seat, point, 1);
            return true;
        }

        private static StageDice[] BuildAllRolls()
        {
            var rolls = new List<StageDice>();
            for (var high = 1; high <= 6; high++)
            {
                for (var low = 1; low <= high; low++)
                {
                    rolls.Add(high == low ? StageDice.Same(high, 4) : StageDice.Distinct(high, low));
                }
            }

            return rolls.ToArray();
        }
    }
}
