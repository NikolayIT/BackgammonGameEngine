namespace Backgammon.Logic.Rules
{
    using System;
    using System.Collections.Generic;

    /// <summary>Converts between the engine's positions and the boards and dice of the views.</summary>
    internal static class ViewConverter
    {
        public static BackgammonBoard ToBoard(in Position position)
        {
            var version = position.Version;
            var points = new BackgammonPoint[24];
            for (var point = 1; point <= 24; point++)
            {
                var other = Geometry.Other(version, point);
                var pinnedSeat = position.IsPinned(0, point) ? 0 : position.IsPinned(1, other) ? 1 : -1;
                points[point - 1] = new BackgammonPoint
                {
                    Number = point,
                    Seat0 = position.Count(0, point),
                    Seat1 = position.Count(1, other),
                    PinnedSeat = pinnedSeat,
                };
            }

            return new BackgammonBoard
            {
                Points = points,
                Bar = new[] { position.Count(0, Geometry.Bar), position.Count(1, Geometry.Bar) },
                Off = new[] { position.Count(0, Geometry.Off), position.Count(1, Geometry.Off) },
            };
        }

        /// <summary>Reads a view's board back into a position, checking that it is a valid one.</summary>
        public static Position ToPosition(BackgammonVersion version, BackgammonBoard board)
        {
            ArgumentNullException.ThrowIfNull(board);
            if (board.Points is not { Count: 24 } || board.Bar is not { Count: 2 } || board.Off is not { Count: 2 })
            {
                throw new ArgumentException("A board has 24 points and a bar and an off count for each seat.", nameof(board));
            }

            var position = new Position { Version = version };
            for (var index = 0; index < 24; index++)
            {
                var point = board.Points[index] ?? throw new ArgumentException("A board point is missing.", nameof(board));
                var number = index + 1;
                var other = Geometry.Other(version, number);
                if (point.Seat0 < 0 || point.Seat1 < 0 || point.Seat0 > 15 || point.Seat1 > 15)
                {
                    throw new ArgumentException($"Point {number} has an impossible count.", nameof(board));
                }

                var shared = point.Seat0 > 0 && point.Seat1 > 0;
                var pinned = point.PinnedSeat switch
                {
                    -1 => -1,
                    0 when point.Seat0 == 1 && point.Seat1 > 0 => 0,
                    1 when point.Seat1 == 1 && point.Seat0 > 0 => 1,
                    _ => throw new ArgumentException($"Point {number} has an impossible pin.", nameof(board)),
                };

                if (shared && (pinned < 0 || version != BackgammonVersion.Tapa))
                {
                    throw new ArgumentException($"Point {number} holds both seats' checkers without a pin.", nameof(board));
                }

                position.Set(0, number, point.Seat0);
                position.Set(1, other, point.Seat1);
                if (pinned == 0)
                {
                    position.SetPinned(0, number, true);
                }
                else if (pinned == 1)
                {
                    position.SetPinned(1, other, true);
                }
            }

            for (var seat = 0; seat < 2; seat++)
            {
                // Counts above 15 are refused here, before they are stored in a byte and could wrap to a valid one.
                if (board.Bar[seat] is < 0 or > Geometry.Checkers || board.Off[seat] is < 0 or > Geometry.Checkers
                    || (board.Bar[seat] > 0 && !Geometry.HasBar(version)))
                {
                    throw new ArgumentException("A bar or off count is impossible.", nameof(board));
                }

                position.Set(seat, Geometry.Bar, board.Bar[seat]);
                position.Set(seat, Geometry.Off, board.Off[seat]);
                var total = 0;
                for (var point = 0; point <= Geometry.Bar; point++)
                {
                    total += position.Count(seat, point);
                }

                if (total != Geometry.Checkers)
                {
                    throw new ArgumentException($"Seat {seat} has {total} checkers, not 15.", nameof(board));
                }
            }

            return position;
        }

        public static StageDice ToStageDice(IReadOnlyList<int> dice)
        {
            ArgumentNullException.ThrowIfNull(dice);
            if (dice.Count == 2 && dice[0] != dice[1])
            {
                return StageDice.Distinct(dice[0], dice[1]);
            }

            if (dice.Count < 1 || dice.Count > 4)
            {
                throw new ArgumentException("A stage has two different dice or one to four equal ones.", nameof(dice));
            }

            for (var i = 1; i < dice.Count; i++)
            {
                if (dice[i] != dice[0])
                {
                    throw new ArgumentException("A stage has two different dice or one to four equal ones.", nameof(dice));
                }
            }

            return StageDice.Same(dice[0], dice.Count);
        }

        public static int[] ToList(StageDice dice, int firstDrawn)
        {
            if (dice.IsDistinct)
            {
                return firstDrawn == dice.Low ? new[] { (int)dice.Low, dice.High } : new[] { (int)dice.High, dice.Low };
            }

            var list = new int[dice.Count];
            Array.Fill(list, dice.High);
            return list;
        }
    }
}
