namespace Backgammon.AI.Evaluation
{
    using Backgammon.Logic.Rules;

    /// <summary>How many rolls each seat has made in the game, which decides when its doubles start to escalate.</summary>
    internal readonly record struct RollCounts(int Seat0, int Seat1)
    {
        public int Of(int seat) => seat == 0 ? this.Seat0 : this.Seat1;

        /// <summary>Whether a double in <paramref name="seat"/>'s next roll would escalate (гюлбара and челеби, from the 4th roll).</summary>
        public bool NextEscalates(in Position position, int seat) => Geometry.HasEscalation(position.Version) && this.Of(seat) + 1 >= 4;

        /// <summary>The counts after <paramref name="seat"/> makes one more roll.</summary>
        public RollCounts After(int seat) => seat == 0 ? this with { Seat0 = this.Seat0 + 1 } : this with { Seat1 = this.Seat1 + 1 };
    }
}
