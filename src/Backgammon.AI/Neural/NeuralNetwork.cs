namespace Backgammon.AI.Neural
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Runtime.Intrinsics;
    using System.Text;

    using Backgammon.Logic;

    /// <summary>
    /// A TD-Gammon-style value network with one hidden layer of sigmoid units and three sigmoid outputs: the chances to
    /// win, to win by 2 points (марс or майка), and to lose by 2, for the seat about to roll.
    /// <list type="bullet">
    /// <item>The inputs are sparse, so the first layer only adds up the rows of the inputs that are set.</item>
    /// <item>All arithmetic is explicit 8-wide float vectors with a fixed order of operations. The results are the
    /// same bit for bit on every machine: the runtime keeps the same lane order when the hardware has no such
    /// vectors.</item>
    /// <item>The weights are read-only once loaded, so any number of threads can evaluate with the same
    /// network.</item>
    /// </list>
    /// </summary>
    internal sealed class NeuralNetwork
    {
        public const int Outputs = 3;

        /// <summary>The largest hidden layer a network file may have.</summary>
        public const int MaxHidden = 4096;

        private const int Magic = 0x4E4E4742; // "BGNN"
        private const int FormatVersion = 1;

        public NeuralNetwork(BackgammonVersion version, int inputs, int hidden, string description, int layout = FeatureEncoder.Layout)
        {
            if (inputs != FeatureEncoder.InputsOf(layout))
            {
                throw new ArgumentOutOfRangeException(nameof(inputs), inputs, $"Input layout {layout} has {FeatureEncoder.InputsOf(layout)} inputs.");
            }

            this.Layout = layout;
            if (hidden % Vector256<float>.Count != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(hidden), hidden, "The hidden layer must be a multiple of 8.");
            }

            this.Version = version;
            this.Inputs = inputs;
            this.Hidden = hidden;
            this.Description = description;
            this.W1 = new float[inputs * hidden];
            this.B1 = new float[hidden];
            this.W2 = new float[Outputs * hidden];
            this.B2 = new float[Outputs];
        }

        public BackgammonVersion Version { get; }

        /// <summary>Gets the input layout the network reads (see <see cref="FeatureEncoder"/>).</summary>
        public int Layout { get; }

        public int Inputs { get; }

        public int Hidden { get; }

        /// <summary>Gets how the network was trained (seed, games, settings), for NEURAL_NETWORK.md and reproducibility.</summary>
        public string Description { get; }

        /// <summary>Gets the first layer, input-major: the weights of input i are W1[i * Hidden .. (i + 1) * Hidden).</summary>
        public float[] W1 { get; }

        public float[] B1 { get; }

        /// <summary>Gets the output layer, output-major: the weights of output k are W2[k * Hidden .. (k + 1) * Hidden).</summary>
        public float[] W2 { get; }

        public float[] B2 { get; }

        public int ParameterCount => this.W1.Length + this.B1.Length + this.W2.Length + this.B2.Length;

        /// <summary>A network with small random weights (Xavier-uniform) and zero biases, for training from scratch.</summary>
        public static NeuralNetwork CreateRandom(BackgammonVersion version, int inputs, int hidden, int seed, int layout = FeatureEncoder.Layout)
        {
            var network = new NeuralNetwork(version, inputs, hidden, $"random seed {seed}", layout);
            var random = new Random(seed);
            var limit1 = MathF.Sqrt(6f / (inputs + hidden)) * 0.5f;
            for (var i = 0; i < network.W1.Length; i++)
            {
                network.W1[i] = (float)(((random.NextDouble() * 2) - 1) * limit1);
            }

            var limit2 = MathF.Sqrt(6f / (hidden + Outputs));
            for (var i = 0; i < network.W2.Length; i++)
            {
                network.W2[i] = (float)(((random.NextDouble() * 2) - 1) * limit2);
            }

            return network;
        }

        public static NeuralNetwork Read(Stream stream)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            if (reader.ReadInt32() != Magic || reader.ReadInt32() != FormatVersion)
            {
                throw new InvalidDataException("Not a network file.");
            }

            var layout = reader.ReadInt32();
            if (layout < 1 || layout > FeatureEncoder.LatestLayout)
            {
                throw new InvalidDataException($"The network was trained on input layout {layout}, which this encoder does not have.");
            }

            var version = (BackgammonVersion)reader.ReadInt32();
            var inputs = reader.ReadInt32();
            var hidden = reader.ReadInt32();
            if (reader.ReadInt32() != Outputs || inputs != FeatureEncoder.InputsOf(layout))
            {
                throw new InvalidDataException("The network does not fit the encoder.");
            }

            // A corrupt size would otherwise load a constant network (0), throw the wrong exception (below 0), or
            // overflow inputs * hidden into a first layer too short for the evaluation's unchecked reads.
            if (!Enum.IsDefined(version) || hidden < Vector256<float>.Count || hidden > MaxHidden || hidden % Vector256<float>.Count != 0)
            {
                throw new InvalidDataException($"The network's version ({(int)version}) or hidden layer ({hidden}) is impossible.");
            }

            var description = reader.ReadString();
            var network = new NeuralNetwork(version, inputs, hidden, description, layout);
            ReadFloats(reader, network.W1);
            ReadFloats(reader, network.B1);
            ReadFloats(reader, network.W2);
            ReadFloats(reader, network.B2);
            return network;
        }

        public void Write(Stream stream)
        {
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(Magic);
            writer.Write(FormatVersion);
            writer.Write(this.Layout);
            writer.Write((int)this.Version);
            writer.Write(this.Inputs);
            writer.Write(this.Hidden);
            writer.Write(Outputs);
            writer.Write(this.Description);
            writer.Write(MemoryMarshal.AsBytes(this.W1.AsSpan()));
            writer.Write(MemoryMarshal.AsBytes(this.B1.AsSpan()));
            writer.Write(MemoryMarshal.AsBytes(this.W2.AsSpan()));
            writer.Write(MemoryMarshal.AsBytes(this.B2.AsSpan()));
        }

        public NeuralNetwork Copy(string description) => this.Copy(description, this.Version);

        /// <summary>A copy for another version, such as a network warm-started from another version's weights.</summary>
        public NeuralNetwork Copy(string description, BackgammonVersion version)
        {
            var copy = new NeuralNetwork(version, this.Inputs, this.Hidden, description, this.Layout);
            this.W1.CopyTo(copy.W1, 0);
            this.B1.CopyTo(copy.B1, 0);
            this.W2.CopyTo(copy.W2, 0);
            this.B2.CopyTo(copy.B2, 0);
            return copy;
        }

        /// <summary>
        /// Runs the network on sparse inputs. <paramref name="hidden"/> receives the hidden activations (the trainer
        /// needs them) and <paramref name="outputs"/> the three chances.
        /// </summary>
        public void Evaluate(ReadOnlySpan<int> indices, ReadOnlySpan<float> values, Span<float> hidden, Span<float> outputs)
        {
            var width = this.Hidden;
            var sums = hidden[..width];
            this.B1.AsSpan().CopyTo(sums);
            ref var sum = ref MemoryMarshal.GetReference(sums);
            ref var weights = ref MemoryArrayStart(this.W1);
            for (var n = 0; n < indices.Length; n++)
            {
                var value = Vector256.Create(values[n]);
                ref var row = ref Unsafe.Add(ref weights, indices[n] * width);
                for (var j = 0; j < width; j += Vector256<float>.Count)
                {
                    var current = Vector256.LoadUnsafe(ref sum, (nuint)j);
                    var product = Vector256.Multiply(Vector256.LoadUnsafe(ref row, (nuint)j), value);
                    Vector256.StoreUnsafe(Vector256.Add(current, product), ref sum, (nuint)j);
                }
            }

            for (var j = 0; j < width; j++)
            {
                sums[j] = NeuralMath.Sigmoid(sums[j]);
            }

            ref var output = ref MemoryArrayStart(this.W2);
            for (var k = 0; k < Outputs; k++)
            {
                var total = Vector256<float>.Zero;
                ref var row = ref Unsafe.Add(ref output, k * width);
                for (var j = 0; j < width; j += Vector256<float>.Count)
                {
                    total = Vector256.Add(total, Vector256.Multiply(Vector256.LoadUnsafe(ref row, (nuint)j), Vector256.LoadUnsafe(ref sum, (nuint)j)));
                }

                // The eight lanes are added in a fixed order: a hardware horizontal sum may add them in another.
                var lanes = total.GetElement(0);
                for (var lane = 1; lane < Vector256<float>.Count; lane++)
                {
                    lanes += total.GetElement(lane);
                }

                outputs[k] = NeuralMath.Sigmoid(lanes + this.B2[k]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ref float MemoryArrayStart(float[] array) => ref MemoryMarshal.GetArrayDataReference(array);

        private static void ReadFloats(BinaryReader reader, float[] target)
        {
            var bytes = reader.ReadBytes(target.Length * sizeof(float));
            if (bytes.Length != target.Length * sizeof(float))
            {
                throw new InvalidDataException("The network file is cut short.");
            }

            MemoryMarshal.Cast<byte, float>(bytes).CopyTo(target);
        }
    }
}
