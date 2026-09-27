namespace Backgammon.Logic
{
    /// <summary>A legal next step: which checker moves with which die, and where it lands.</summary>
    public sealed class BackgammonStepOption
    {
        /// <summary>Gets the step, in the mover's own numbering.</summary>
        public BackgammonStep Step { get; init; }

        /// <summary>Gets where the checker lands, in the mover's numbering: 1..24, or 0 when it is borne off.</summary>
        public int To { get; init; }

        /// <summary>Gets the point the checker leaves in seat 0's numbering, or 25 for the bar.</summary>
        public int Seat0From { get; init; }

        /// <summary>Gets where the checker lands in seat 0's numbering, or 0 when it is borne off.</summary>
        public int Seat0To { get; init; }

        /// <summary>Gets a value indicating whether the step hits a lone checker (to the bar) or, in тапа, pins it.</summary>
        public bool Captures { get; init; }
    }
}
