namespace Backgammon.Logic.Tests.Naive
{
    using System;
    using System.Text;

    /// <summary>
    /// A deliberately simple board for the naive move generator. It shares no code with the engine. Checkers are kept
    /// by absolute point (seat 0's numbering, 1..24), with plain arrays and no bit tricks, and every rule is written
    /// again from RULES.md.
    /// </summary>
    internal sealed class NaiveBoard
    {
        public NaiveBoard(BackgammonVersion version)
        {
            this.Version = version;
        }

        public BackgammonVersion Version { get; }

        // [seat][absolute point 1..24]
        public int[][] Checkers { get; } = { new int[25], new int[25] };

        // [seat][absolute point 1..24]: the seat's checker there is pinned (тапа).
        public bool[][] Pinned { get; } = { new bool[25], new bool[25] };

        public int[] Bar { get; } = new int[2];

        public int[] Off { get; } = new int[2];

        public static NaiveBoard FromCode(string code)
        {
            var version = (BackgammonVersion)"OGTC".IndexOf(code[0], StringComparison.Ordinal);
            var board = new NaiveBoard(version);
            var parts = code[2..].Split('|');
            var points = parts[0].Split(',');
            for (var point = 1; point <= 24; point++)
            {
                var token = points[point - 1];
                if (token.Length == 0)
                {
                    continue;
                }

                var pinned = token.EndsWith('*');
                var count = int.Parse(pinned ? token[..^1] : token, System.Globalization.CultureInfo.InvariantCulture);
                var owner = count > 0 ? 0 : 1;
                board.Checkers[owner][point] = Math.Abs(count);
                if (pinned)
                {
                    board.Checkers[1 - owner][point] = 1;
                    board.Pinned[1 - owner][point] = true;
                }
            }

            var bar = parts[1].Split(',');
            var off = parts[2].Split(',');
            for (var seat = 0; seat < 2; seat++)
            {
                board.Bar[seat] = int.Parse(bar[seat], System.Globalization.CultureInfo.InvariantCulture);
                board.Off[seat] = int.Parse(off[seat], System.Globalization.CultureInfo.InvariantCulture);
            }

            return board;
        }

        /// <summary>
        /// The absolute point of a seat's own point. Seat 0 numbers the board absolutely. Seat 1 moves the other
        /// way in обикновена, тапа and челеби, so its own point p is absolute 25 - p. In гюлбара seat 1 starts in the
        /// diagonally opposite corner and moves the same way round, so its own 24 is absolute 12, its own 13 is
        /// absolute 1, its own 12 is absolute 24 and its own 1 is absolute 13.
        /// </summary>
        public int Absolute(int seat, int ownPoint)
        {
            if (seat == 0)
            {
                return ownPoint;
            }

            if (this.Version != BackgammonVersion.Gyulbara)
            {
                return 25 - ownPoint;
            }

            return ownPoint >= 13 ? ownPoint - 12 : ownPoint + 12;
        }

        public int CountOwn(int seat, int ownPoint) => this.Checkers[seat][this.Absolute(seat, ownPoint)];

        public NaiveBoard Clone()
        {
            var copy = new NaiveBoard(this.Version);
            for (var seat = 0; seat < 2; seat++)
            {
                Array.Copy(this.Checkers[seat], copy.Checkers[seat], 25);
                Array.Copy(this.Pinned[seat], copy.Pinned[seat], 25);
                copy.Bar[seat] = this.Bar[seat];
                copy.Off[seat] = this.Off[seat];
            }

            return copy;
        }

        /// <summary>The position code documented in RULES.md, written independently of the engine's formatter.</summary>
        public string ToCode()
        {
            var text = new StringBuilder();
            text.Append("OGTC"[(int)this.Version]).Append(':');
            for (var point = 1; point <= 24; point++)
            {
                if (point > 1)
                {
                    text.Append(',');
                }

                var zero = this.Checkers[0][point];
                var one = this.Checkers[1][point];
                if (this.Pinned[1][point])
                {
                    text.Append(zero).Append('*');
                }
                else if (this.Pinned[0][point])
                {
                    text.Append(-one).Append('*');
                }
                else if (zero > 0)
                {
                    text.Append(zero);
                }
                else if (one > 0)
                {
                    text.Append(-one);
                }
            }

            text.Append('|').Append(this.Bar[0]).Append(',').Append(this.Bar[1]);
            text.Append('|').Append(this.Off[0]).Append(',').Append(this.Off[1]);
            return text.ToString();
        }
    }
}
