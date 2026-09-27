namespace Backgammon.AI.Neural
{
    using System;

    using Backgammon.AI.Evaluation;
    using Backgammon.Logic.Rules;

    /// <summary>An evaluator backed by a network. Thread-safe: the network is read-only and the buffers are per thread.</summary>
    internal sealed class NeuralEvaluator : IEvaluator
    {
        [ThreadStatic]
        private static Buffers? buffers;

        public NeuralEvaluator(NeuralNetwork network)
        {
            this.Network = network;
        }

        public NeuralNetwork Network { get; }

        public Outcome Evaluate(in Position position, int onRoll, RollCounts rollsMade)
        {
            var scratch = buffers ??= new Buffers();
            if (scratch.Hidden.Length < this.Network.Hidden)
            {
                scratch.Hidden = new float[this.Network.Hidden];
            }

            var count = FeatureEncoder.Encode(this.Network.Layout, position, onRoll, rollsMade, scratch.Indices, scratch.Values);
            Span<float> outputs = stackalloc float[NeuralNetwork.Outputs];
            this.Network.Evaluate(scratch.Indices.AsSpan(0, count), scratch.Values.AsSpan(0, count), scratch.Hidden, outputs);
            return Outcome.FromNetwork(outputs[0], outputs[1], outputs[2]);
        }

        private sealed class Buffers
        {
            public int[] Indices { get; } = new int[FeatureEncoder.MaxActive];

            public float[] Values { get; } = new float[FeatureEncoder.MaxActive];

            public float[] Hidden { get; set; } = new float[256];
        }
    }
}
