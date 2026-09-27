namespace Backgammon.AI.Neural
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// The network's sigmoid, written out in plain float arithmetic rather than through <see cref="MathF.Exp"/>. The
    /// runtime's exp may differ in the last bit between platforms, and a different last bit could make two machines
    /// choose different plays. This version gives the same result everywhere. Its relative error is below 2e-7, well
    /// under what matters to a network.
    /// </summary>
    internal static class NeuralMath
    {
        private const float Log2E = 1.44269504f;
        private const float Ln2High = 0.693359375f;
        private const float Ln2Low = -2.12194440e-4f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sigmoid(float x) => 1f / (1f + Exp(-x));

        /// <summary>e to the <paramref name="x"/>, for x clamped to ±80.</summary>
        public static float Exp(float x)
        {
            x = Math.Clamp(x, -80f, 80f);

            // x = k ln 2 + r with |r| <= ln 2 / 2, then e^x = 2^k e^r.
            var k = MathF.Round(x * Log2E);
            var r = x - (k * Ln2High) - (k * Ln2Low);

            // e^r by its Taylor polynomial of degree 7 (Horner).
            var p = 1f / 5040f;
            p = (p * r) + (1f / 720f);
            p = (p * r) + (1f / 120f);
            p = (p * r) + (1f / 24f);
            p = (p * r) + (1f / 6f);
            p = (p * r) + 0.5f;
            p = (p * r) + 1f;
            p = (p * r) + 1f;

            // 2^k built from the exponent bits.
            var bits = ((int)k + 127) << 23;
            return p * BitConverter.Int32BitsToSingle(bits);
        }
    }
}
