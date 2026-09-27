namespace Backgammon.Logic.Rules
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Up to four steps packed in a value: step i is the byte <c>(from &lt;&lt; 3) | die</c> at bits 8i..8i+7. Sequences
    /// compare in the canonical order, where the step from the higher point comes first and, for the same point, the
    /// larger die comes first. When one sequence is a prefix of the other, the shorter comes first.
    /// </summary>
    internal readonly struct StepSequence : IEquatable<StepSequence>, IComparable<StepSequence>
    {
        public static readonly StepSequence Empty = default;

        private readonly uint bits;
        private readonly byte count;

        private StepSequence(uint bits, byte count)
        {
            this.bits = bits;
            this.count = count;
        }

        public int Count => this.count;

        public static StepSequence Of(IReadOnlyList<BackgammonStep> steps)
        {
            var sequence = Empty;
            foreach (var step in steps)
            {
                sequence = sequence.Append(step.From, step.Die);
            }

            return sequence;
        }

        public StepSequence Append(int from, int die) =>
            new(this.bits | ((uint)((from << 3) | die) << (8 * this.count)), (byte)(this.count + 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int From(int index) => (int)(this.bits >> ((8 * index) + 3)) & 31;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Die(int index) => (int)(this.bits >> (8 * index)) & 7;

        public BackgammonStep[] ToSteps()
        {
            var steps = new BackgammonStep[this.count];
            for (var i = 0; i < steps.Length; i++)
            {
                steps[i] = new BackgammonStep(this.From(i), this.Die(i));
            }

            return steps;
        }

        public int CompareTo(StepSequence other)
        {
            var shared = Math.Min(this.count, other.count);
            for (var i = 0; i < shared; i++)
            {
                var mine = (int)(this.bits >> (8 * i)) & 0xFF;
                var theirs = (int)(other.bits >> (8 * i)) & 0xFF;
                if (mine != theirs)
                {
                    // Higher from (then higher die) first.
                    return theirs.CompareTo(mine);
                }
            }

            return this.count.CompareTo(other.count);
        }

        public bool Equals(StepSequence other) => this.bits == other.bits && this.count == other.count;

        public override bool Equals(object? obj) => obj is StepSequence other && this.Equals(other);

        public override int GetHashCode() => HashCode.Combine(this.bits, this.count);

        public override string ToString()
        {
            var parts = new string[this.count];
            for (var i = 0; i < parts.Length; i++)
            {
                parts[i] = $"{this.From(i)}/{this.Die(i)}";
            }

            return string.Join(' ', parts);
        }
    }
}
