namespace Backgammon.Logic.Rules
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// A board position: both players' checkers in their own numbering (0 = off, 1..24 = points, 25 = bar) and, for
    /// тапа, which of them are pinned. A plain value type, cheap to copy, compared by value.
    /// </summary>
    internal struct Position : IEquatable<Position>
    {
        // Seat s's count on its own point p is at [s * 26 + p]; the last 4 bytes are padding and always 0, so the
        // counts are compared and hashed as 7 whole ulongs.
        internal CheckerCounts Counts;

        // Bit p set: the seat's checker on its own point p is pinned (тапа).
        internal uint Pinned0;
        internal uint Pinned1;

        internal BackgammonVersion Version;

        public readonly bool HasPins => (this.Pinned0 | this.Pinned1) != 0;

        public static bool operator ==(Position left, Position right) => left.Equals(right);

        public static bool operator !=(Position left, Position right) => !left.Equals(right);

        public static Position Start(BackgammonVersion version)
        {
            var position = new Position { Version = version };
            for (var seat = 0; seat < 2; seat++)
            {
                if (version == BackgammonVersion.Obiknovena || version == BackgammonVersion.Chelebi)
                {
                    position.Set(seat, 24, 2);
                    position.Set(seat, 13, 5);
                    position.Set(seat, 8, 3);
                    position.Set(seat, 6, 5);
                }
                else
                {
                    position.Set(seat, 24, Geometry.Checkers);
                }
            }

            return position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int Count(int seat, int point) => this.Counts[(seat * Geometry.Slots) + point];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int seat, int point, int count) => this.Counts[(seat * Geometry.Slots) + point] = (byte)count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int seat, int point, int delta) =>
            this.Counts[(seat * Geometry.Slots) + point] = (byte)(this.Counts[(seat * Geometry.Slots) + point] + delta);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly uint PinnedMask(int seat) => seat == 0 ? this.Pinned0 : this.Pinned1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsPinned(int seat, int point) => ((this.PinnedMask(seat) >> point) & 1) != 0;

        public void SetPinned(int seat, int point, bool pinned)
        {
            var bit = 1u << point;
            if (seat == 0)
            {
                this.Pinned0 = pinned ? this.Pinned0 | bit : this.Pinned0 & ~bit;
            }
            else
            {
                this.Pinned1 = pinned ? this.Pinned1 | bit : this.Pinned1 & ~bit;
            }
        }

        /// <summary>The seat's pip count: the sum of its checkers' own point numbers, the bar counting 25.</summary>
        public readonly int Pips(int seat)
        {
            var pips = 0;
            var offset = seat * Geometry.Slots;
            for (var point = 1; point <= Geometry.Bar; point++)
            {
                pips += point * this.Counts[offset + point];
            }

            return pips;
        }

        /// <summary>The seat's checkers on the board and the bar, i.e. not yet borne off.</summary>
        public readonly int OnBoard(int seat) => Geometry.Checkers - this.Count(seat, Geometry.Off);

        public readonly bool Equals(Position other)
        {
            var mine = MemoryMarshal.Cast<byte, ulong>((ReadOnlySpan<byte>)this.Counts);
            var theirs = MemoryMarshal.Cast<byte, ulong>((ReadOnlySpan<byte>)other.Counts);
            return mine.SequenceEqual(theirs)
                && this.Pinned0 == other.Pinned0
                && this.Pinned1 == other.Pinned1
                && this.Version == other.Version;
        }

        public override readonly bool Equals(object? obj) => obj is Position other && this.Equals(other);

        public override readonly int GetHashCode()
        {
            var words = MemoryMarshal.Cast<byte, ulong>((ReadOnlySpan<byte>)this.Counts);
            var hash = 0x9E3779B97F4A7C15UL ^ (ulong)this.Version;
            for (var i = 0; i < words.Length; i++)
            {
                hash = (hash ^ words[i]) * 0xFF51AFD7ED558CCDUL;
                hash ^= hash >> 32;
            }

            hash = (hash ^ this.Pinned0 ^ ((ulong)this.Pinned1 << 32)) * 0xC4CEB9FE1A85EC53UL;
            return (int)(hash ^ (hash >> 29));
        }

        public override readonly string ToString() => PositionCode.Format(this);

        [InlineArray(56)]
        internal struct CheckerCounts
        {
            private byte element;
        }
    }
}
