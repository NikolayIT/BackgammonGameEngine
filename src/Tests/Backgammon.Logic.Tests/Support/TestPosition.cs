namespace Backgammon.Logic.Tests.Support
{
    using System.Collections.Generic;
    using System.Linq;

    using Backgammon.Logic.Rules;

    /// <summary>
    /// Builds positions for rule tests in each seat's own numbering. Checkers that are not placed count as borne off,
    /// so a test only lists the checkers that matter.
    /// </summary>
    internal sealed class TestPosition
    {
        private readonly BackgammonVersion version;
        private readonly int[][] counts = { new int[26], new int[26] };
        private readonly List<(int Seat, int Point)> pins = new();

        private TestPosition(BackgammonVersion version)
        {
            this.version = version;
        }

        public static TestPosition Of(BackgammonVersion version) => new(version);

        /// <summary>Puts <paramref name="count"/> of the seat's checkers on its own point (25 = bar).</summary>
        public TestPosition With(int seat, int point, int count)
        {
            this.counts[seat][point] += count;
            return this;
        }

        /// <summary>
        /// Тапа: one checker of <paramref name="pinnedSeat"/> on its own <paramref name="point"/>, pinned by
        /// <paramref name="pinners"/> of the other seat's checkers on the same spot.
        /// </summary>
        public TestPosition Pinned(int pinnedSeat, int point, int pinners = 1)
        {
            this.counts[pinnedSeat][point] += 1;
            this.counts[1 - pinnedSeat][Geometry.Other(this.version, point)] += pinners;
            this.pins.Add((pinnedSeat, point));
            return this;
        }

        public Position Build()
        {
            var position = new Position { Version = this.version };
            for (var seat = 0; seat < 2; seat++)
            {
                for (var point = 1; point <= Geometry.Bar; point++)
                {
                    position.Set(seat, point, this.counts[seat][point]);
                }

                position.Set(seat, Geometry.Off, 15 - this.counts[seat].Sum());
            }

            foreach (var (seat, point) in this.pins)
            {
                position.SetPinned(seat, point, true);
            }

            return position;
        }
    }
}
