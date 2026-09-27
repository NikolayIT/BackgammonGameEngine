namespace Backgammon.Arena
{
    using System.Collections.Generic;

    using Backgammon.Logic;
    using Backgammon.Logic.Rules;

    /// <summary>
    /// A hand-picked position for the tricky test vectors. Each one shows one rule at work: what the dice allow
    /// there is the point of the case. Checkers that are not placed count as borne off.
    /// </summary>
    internal sealed class Scenario
    {
        private readonly int[][] counts = { new int[26], new int[26] };
        private readonly List<(int Seat, int Point)> pins = new();

        public Scenario(string name, string description, BackgammonVersion version, int seat, params int[] dice)
        {
            this.Name = name;
            this.Description = description;
            this.Version = version;
            this.Seat = seat;
            this.Dice = dice;
        }

        public string Name { get; }

        public string Description { get; }

        public BackgammonVersion Version { get; }

        public int Seat { get; }

        public int[] Dice { get; }

        /// <summary>Puts checkers of <paramref name="seat"/> on its own point (25 = bar).</summary>
        public Scenario With(int seat, int point, int count)
        {
            this.counts[seat][point] += count;
            return this;
        }

        /// <summary>One checker of <paramref name="pinnedSeat"/> on its own point, pinned by the other seat's checkers.</summary>
        public Scenario Pinned(int pinnedSeat, int point, int pinners = 1)
        {
            this.counts[pinnedSeat][point] += 1;
            this.counts[1 - pinnedSeat][Geometry.Other(this.Version, point)] += pinners;
            this.pins.Add((pinnedSeat, point));
            return this;
        }

        public Position Build()
        {
            var position = new Position { Version = this.Version };
            for (var seat = 0; seat < 2; seat++)
            {
                var total = 0;
                for (var point = 1; point <= Geometry.Bar; point++)
                {
                    position.Set(seat, point, this.counts[seat][point]);
                    total += this.counts[seat][point];
                }

                position.Set(seat, Geometry.Off, 15 - total);
            }

            foreach (var (seat, point) in this.pins)
            {
                position.SetPinned(seat, point, true);
            }

            return position;
        }
    }
}
