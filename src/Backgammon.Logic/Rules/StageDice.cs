namespace Backgammon.Logic.Rules
{
    using System;

    /// <summary>
    /// The dice of one stage: either two different dice, from a roll that is not a double, or one to four equal
    /// dice, from a double, a stage of an escalating chain, or the rest of one.
    /// </summary>
    internal readonly struct StageDice : IEquatable<StageDice>
    {
        private StageDice(int high, int low, int count)
        {
            this.High = (byte)high;
            this.Low = (byte)low;
            this.Count = (byte)count;
        }

        /// <summary>Gets the larger die (the only die value when the dice are equal).</summary>
        public byte High { get; }

        /// <summary>Gets the smaller die (equal to <see cref="High"/> when the dice are equal).</summary>
        public byte Low { get; }

        /// <summary>Gets how many dice there are: 2 for different dice, 1..4 for equal ones.</summary>
        public byte Count { get; }

        public bool IsDistinct => this.High != this.Low;

        public static StageDice Distinct(int first, int second)
        {
            if (first == second || !IsDie(first) || !IsDie(second))
            {
                throw new ArgumentOutOfRangeException(nameof(second), "Two different dice 1..6 are needed.");
            }

            return new StageDice(Math.Max(first, second), Math.Min(first, second), 2);
        }

        public static StageDice Same(int die, int count)
        {
            if (!IsDie(die) || count < 1 || count > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "A die 1..6 used one to four times is needed.");
            }

            return new StageDice(die, die, count);
        }

        public bool Equals(StageDice other) => this.High == other.High && this.Low == other.Low && this.Count == other.Count;

        public override bool Equals(object? obj) => obj is StageDice other && this.Equals(other);

        public override int GetHashCode() => HashCode.Combine(this.High, this.Low, this.Count);

        public override string ToString() => this.IsDistinct ? $"{this.High}-{this.Low}" : $"{this.High}x{this.Count}";

        private static bool IsDie(int value) => value >= 1 && value <= 6;
    }
}
