namespace Backgammon.Trainer
{
    using System;

    using Backgammon.AI.Neural;

    /// <summary>
    /// One step of plain SGD on a network: its three outputs are pushed towards a target with the cross-entropy
    /// gradient (target minus output at each sigmoid), and the weights change at once.
    /// </summary>
    internal sealed class SupervisedLearner
    {
        private readonly NeuralNetwork network;
        private readonly int[] indices = new int[FeatureEncoder.MaxActive];
        private readonly float[] values = new float[FeatureEncoder.MaxActive];
        private readonly float[] hidden;
        private readonly float[] hiddenDelta;
        private readonly float[] outputs = new float[NeuralNetwork.Outputs];

        public SupervisedLearner(NeuralNetwork network)
        {
            this.network = network;
            this.hidden = new float[network.Hidden];
            this.hiddenDelta = new float[network.Hidden];
        }

        /// <summary>Moves the network's value of <paramref name="state"/> towards <paramref name="target"/>; returns the squared error before.</summary>
        public double Step(in TrainingState state, ReadOnlySpan<float> target, float alpha)
        {
            var width = this.network.Hidden;
            var active = FeatureEncoder.Encode(this.network.Layout, state.Position, state.Roller, state.Rolls, this.indices, this.values);
            this.network.Evaluate(this.indices.AsSpan(0, active), this.values.AsSpan(0, active), this.hidden, this.outputs);

            // The hidden layer's share of the error, with the output weights as they were.
            var error = 0.0;
            Array.Clear(this.hiddenDelta);
            Span<float> delta = stackalloc float[NeuralNetwork.Outputs];
            for (var k = 0; k < NeuralNetwork.Outputs; k++)
            {
                delta[k] = target[k] - this.outputs[k];
                error += delta[k] * delta[k];
                var row = this.network.W2.AsSpan(k * width, width);
                for (var j = 0; j < width; j++)
                {
                    this.hiddenDelta[j] += delta[k] * row[j];
                }
            }

            for (var k = 0; k < NeuralNetwork.Outputs; k++)
            {
                var step = alpha * delta[k];
                this.network.B2[k] += step;
                var row = this.network.W2.AsSpan(k * width, width);
                for (var j = 0; j < width; j++)
                {
                    row[j] += step * this.hidden[j];
                }
            }

            for (var j = 0; j < width; j++)
            {
                var h = this.hidden[j];
                this.hiddenDelta[j] *= alpha * h * (1 - h);
                this.network.B1[j] += this.hiddenDelta[j];
            }

            for (var n = 0; n < active; n++)
            {
                var value = this.values[n];
                var row = this.network.W1.AsSpan(this.indices[n] * width, width);
                for (var j = 0; j < width; j++)
                {
                    row[j] += this.hiddenDelta[j] * value;
                }
            }

            return error;
        }
    }
}
