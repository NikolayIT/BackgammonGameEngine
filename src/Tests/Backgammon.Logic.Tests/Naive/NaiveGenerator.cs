namespace Backgammon.Logic.Tests.Naive
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// A brute-force move generator to check the engine's fast one against. It tries every remaining die value on
    /// every point, in every order, and keeps every sequence that cannot be extended. Then it applies the stage rules
    /// as RULES.md states them: the greatest effective length (a game-ending sequence counts as all the dice), and
    /// the larger die when only one of two different dice can be played. It is slow, obvious code with no pruning
    /// and no shared helpers.
    /// </summary>
    internal static class NaiveGenerator
    {
        public const int EndNone = 0;
        public const int EndBorneOff = 1;
        public const int EndMother = 2;
        public const int EndBothMothers = 3;

        public static NaiveResult Generate(NaiveBoard board, int seat, IReadOnlyList<int> dice)
        {
            var leaves = new List<Leaf>();
            Extend(board, seat, dice.ToList(), new List<(int From, int Die)>(), leaves);

            int Effective(Leaf leaf) => leaf.End != EndNone ? dice.Count : leaf.Steps.Count;

            var max = leaves.Max(Effective);
            var legal = leaves.Where(s => Effective(s) == max).ToList();
            var distinct = dice.Count == 2 && dice[0] != dice[1];
            if (distinct && max == 1)
            {
                var high = Math.Max(dice[0], dice[1]);
                if (legal.Any(s => s.Steps[0].Die == high))
                {
                    legal = legal.Where(s => s.Steps[0].Die == high).ToList();
                }
            }

            var ends = new Dictionary<string, NaiveEnd>();
            foreach (var leaf in legal)
            {
                if (!ends.TryGetValue(leaf.Code, out var known) || Compare(leaf.Steps, known.Steps) < 0)
                {
                    ends[leaf.Code] = new NaiveEnd(leaf.Steps, leaf.End);
                }
            }

            return new NaiveResult(max, ends);
        }

        /// <summary>The canonical order: the higher point first, then the larger die; a prefix comes first.</summary>
        public static int Compare(IReadOnlyList<(int From, int Die)> a, IReadOnlyList<(int From, int Die)> b)
        {
            for (var i = 0; i < Math.Min(a.Count, b.Count); i++)
            {
                if (a[i].From != b[i].From)
                {
                    return b[i].From.CompareTo(a[i].From);
                }

                if (a[i].Die != b[i].Die)
                {
                    return b[i].Die.CompareTo(a[i].Die);
                }
            }

            return a.Count.CompareTo(b.Count);
        }

        public static bool IsLegal(NaiveBoard board, int seat, int from, int die)
        {
            var opponent = 1 - seat;
            if (from == 25)
            {
                if (board.Bar[seat] == 0)
                {
                    return false;
                }
            }
            else
            {
                if (board.Bar[seat] > 0 || board.CountOwn(seat, from) == 0)
                {
                    return false;
                }

                if (board.Pinned[seat][board.Absolute(seat, from)])
                {
                    return false;
                }
            }

            var to = from - die;
            if (to >= 1)
            {
                var at = board.Absolute(seat, to);
                var theirs = board.Checkers[opponent][at];
                switch (board.Version)
                {
                    case BackgammonVersion.Gyulbara:
                        return theirs == 0;
                    case BackgammonVersion.Tapa:
                        return !board.Pinned[seat][at] && theirs <= 1;
                    default:
                        return theirs <= 1;
                }
            }

            if (board.Bar[seat] > 0)
            {
                return false;
            }

            for (var point = 7; point <= 24; point++)
            {
                if (board.CountOwn(seat, point) > 0)
                {
                    return false;
                }
            }

            if (board.Pinned[seat].Any(pinned => pinned))
            {
                return false;
            }

            if (to == 0)
            {
                return true;
            }

            for (var point = from + 1; point <= 6; point++)
            {
                if (board.CountOwn(seat, point) > 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Moves the checker and returns how the step ended the game (one of the End constants).</summary>
        public static int Apply(NaiveBoard board, int seat, int from, int die)
        {
            var opponent = 1 - seat;
            if (from == 25)
            {
                board.Bar[seat]--;
            }
            else
            {
                var fromAt = board.Absolute(seat, from);
                board.Checkers[seat][fromAt]--;
                if (board.Version == BackgammonVersion.Tapa && board.Checkers[seat][fromAt] == 0 && board.Pinned[opponent][fromAt])
                {
                    board.Pinned[opponent][fromAt] = false;
                }
            }

            var to = from - die;
            if (to <= 0)
            {
                board.Off[seat]++;
                return board.Off[seat] == 15 ? EndBorneOff : EndNone;
            }

            var at = board.Absolute(seat, to);
            if (board.Checkers[opponent][at] == 1)
            {
                if (board.Version == BackgammonVersion.Tapa)
                {
                    board.Pinned[opponent][at] = true;
                }
                else
                {
                    board.Checkers[opponent][at] = 0;
                    board.Bar[opponent]++;
                }
            }

            board.Checkers[seat][at]++;

            if (board.Version == BackgammonVersion.Tapa)
            {
                var ownStart = board.Absolute(seat, 24);
                var theirStart = board.Absolute(opponent, 24);
                if (board.Pinned[opponent][theirStart] && board.Pinned[seat][ownStart])
                {
                    return EndBothMothers;
                }

                if (board.Pinned[opponent][theirStart] && board.Checkers[seat][ownStart] == 0)
                {
                    return EndMother;
                }
            }

            return EndNone;
        }

        private static void Extend(NaiveBoard board, int seat, List<int> remaining, List<(int From, int Die)> steps, List<Leaf> output)
        {
            var moved = false;
            foreach (var die in remaining.Distinct().ToList())
            {
                for (var from = 25; from >= 1; from--)
                {
                    if (!IsLegal(board, seat, from, die))
                    {
                        continue;
                    }

                    moved = true;
                    var next = board.Clone();
                    var end = Apply(next, seat, from, die);
                    var nextSteps = new List<(int From, int Die)>(steps) { (from, die) };
                    var rest = new List<int>(remaining);
                    rest.Remove(die);
                    if (end != EndNone || rest.Count == 0)
                    {
                        output.Add(new Leaf(next.ToCode(), nextSteps, end));
                    }
                    else
                    {
                        Extend(next, seat, rest, nextSteps, output);
                    }
                }
            }

            if (!moved)
            {
                output.Add(new Leaf(board.ToCode(), steps, EndNone));
            }
        }

        // A sequence that cannot be extended, kept as the code of the board it ends on.
        private sealed record Leaf(string Code, List<(int From, int Die)> Steps, int End);
    }
}
