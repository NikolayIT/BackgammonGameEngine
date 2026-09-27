namespace Backgammon.Trainer
{
    using System;
    using System.Runtime.Intrinsics;

    using Backgammon.AI.Neural;

    /// <summary>
    /// TD(λ) for one finished game, in the forward view.
    /// <list type="number">
    /// <item>The network values every training state.</item>
    /// <item>The λ-returns are built backwards from the result: each state's target mixes the value of the next state
    /// (weight 1 - λ) with the next state's own target (weight λ), both seen from this state's roller. The last target
    /// is the result itself.</item>
    /// <item>Each state's outputs are pushed towards its target with the cross-entropy gradient, which is target minus
    /// output at each sigmoid.</item>
    /// </list>
    /// Since the whole game is valued by the same, frozen network, this is exactly offline TD(λ). The changes are added
    /// to a buffer laid out like the network's parameters (W1, B1, W2, B2), which the caller applies.
    /// </summary>
    internal sealed class TdLearner
    {
        private readonly NeuralNetwork network;
        private readonly int[] indices = new int[FeatureEncoder.MaxActive];
        private readonly float[] values = new float[FeatureEncoder.MaxActive];
        private float[] hidden = Array.Empty<float>();
        private float[] outputs = Array.Empty<float>();
        private float[] targets = Array.Empty<float>();
        private int[] rollers = Array.Empty<int>();
        private float[] hiddenDelta = Array.Empty<float>();

        public TdLearner(NeuralNetwork network)
        {
            this.network = network;
        }

        public static void Flip(ReadOnlySpan<float> source, Span<float> target)
        {
            // Win for the other side, and the two 2-point chances swapped.
            var win = 1 - source[0];
            var winDouble = source[2];
            var loseDouble = source[1];
            target[0] = win;
            target[1] = winDouble;
            target[2] = loseDouble;
        }

        /// <summary>Adds the game's TD(λ) changes, scaled by <paramref name="alpha"/>, to <paramref name="delta"/>; returns the squared error.</summary>
        public double Accumulate(GameRecord game, float alpha, float lambda, float[] delta)
        {
            var target0 = game.TargetForSeat0();
            var states = game.States;
            if (target0 == null || states.Count == 0)
            {
                return 0;
            }

            var count = states.Count;
            var width = this.network.Hidden;
            const int outs = NeuralNetwork.Outputs;
            this.Ensure(count, width);

            // 1. Values, keeping every state's hidden layer for the gradient.
            for (var t = 0; t < count; t++)
            {
                var state = states[t];
                var active = FeatureEncoder.Encode(state.Position, state.Roller, state.Rolls, this.indices, this.values);
                this.network.Evaluate(this.indices.AsSpan(0, active), this.values.AsSpan(0, active), this.hidden.AsSpan(t * width, width), this.outputs.AsSpan(t * outs, outs));
                this.rollers[t] = state.Roller;
            }

            // 2. λ-returns, backwards.
            Span<float> next = stackalloc float[outs];
            Span<float> nextTarget = stackalloc float[outs];
            for (var t = count - 1; t >= 0; t--)
            {
                var target = this.targets.AsSpan(t * outs, outs);
                if (t == count - 1)
                {
                    if (this.rollers[t] == 0)
                    {
                        target0.CopyTo(target);
                    }
                    else
                    {
                        Flip(target0, target);
                    }

                    continue;
                }

                var same = this.rollers[t + 1] == this.rollers[t];
                var nextValue = this.outputs.AsSpan((t + 1) * outs, outs);
                var nextReturn = this.targets.AsSpan((t + 1) * outs, outs);
                if (same)
                {
                    nextValue.CopyTo(next);
                    nextReturn.CopyTo(nextTarget);
                }
                else
                {
                    Flip(nextValue, next);
                    Flip(nextReturn, nextTarget);
                }

                for (var k = 0; k < outs; k++)
                {
                    target[k] = ((1 - lambda) * next[k]) + (lambda * nextTarget[k]);
                }
            }

            // 3. Gradients.
            var w1 = 0;
            var b1 = this.network.W1.Length;
            var w2 = b1 + width;
            var b2 = w2 + (outs * width);
            var error = 0.0;
            for (var t = 0; t < count; t++)
            {
                var state = states[t];
                var active = FeatureEncoder.Encode(state.Position, state.Roller, state.Rolls, this.indices, this.values);
                var h = this.hidden.AsSpan(t * width, width);
                Array.Clear(this.hiddenDelta);
                for (var k = 0; k < outs; k++)
                {
                    var d = this.targets[(t * outs) + k] - this.outputs[(t * outs) + k];
                    error += d * d;
                    var step = alpha * d;
                    delta[b2 + k] += step;
                    var row = this.network.W2.AsSpan(k * width, width);
                    var deltaRow = delta.AsSpan(w2 + (k * width), width);
                    for (var j = 0; j < width; j++)
                    {
                        deltaRow[j] += step * h[j];
                        this.hiddenDelta[j] += d * row[j];
                    }
                }

                for (var j = 0; j < width; j++)
                {
                    this.hiddenDelta[j] *= alpha * h[j] * (1 - h[j]);
                    delta[b1 + j] += this.hiddenDelta[j];
                }

                for (var n = 0; n < active; n++)
                {
                    var input = Vector256.Create(this.values[n]);
                    var deltaRow = delta.AsSpan(w1 + (this.indices[n] * width), width);
                    for (var j = 0; j < width; j += Vector256<float>.Count)
                    {
                        var current = Vector256.Create<float>(deltaRow[j..]);
                        var change = Vector256.Multiply(Vector256.Create<float>(this.hiddenDelta.AsSpan(j)), input);
                        Vector256.Add(current, change).CopyTo(deltaRow[j..]);
                    }
                }
            }

            return error;
        }

        private void Ensure(int count, int width)
        {
            if (this.rollers.Length < count)
            {
                var size = Math.Max(count, this.rollers.Length * 2);
                this.hidden = new float[size * width];
                this.outputs = new float[size * NeuralNetwork.Outputs];
                this.targets = new float[size * NeuralNetwork.Outputs];
                this.rollers = new int[size];
            }

            if (this.hiddenDelta.Length != width)
            {
                this.hiddenDelta = new float[width];
            }
        }
    }
}
