namespace Backgammon.AI.Neural
{
    using System;
    using System.IO;

    using Backgammon.Logic;

    /// <summary>
    /// The trained networks shipped inside the package, one per version, loaded once and shared. The versions listed in
    /// <see cref="Shipped"/> must have their file; for any other version the bots play with the hand-written evaluator.
    /// Which versions ship a network is decided by the arena gate (NEURAL_NETWORK.md).
    /// </summary>
    internal static class Networks
    {
        /// <summary>The versions whose network passed the gate against the baseline.</summary>
        public static readonly BackgammonVersion[] Shipped = { BackgammonVersion.Obiknovena };

        private static readonly Lazy<NeuralNetwork?>[] Loaded =
        {
            new(() => Load(BackgammonVersion.Obiknovena)),
            new(() => Load(BackgammonVersion.Gyulbara)),
            new(() => Load(BackgammonVersion.Tapa)),
            new(() => Load(BackgammonVersion.Chelebi)),
        };

        /// <summary>The shipped network of a version, or null when the version plays with the baseline.</summary>
        public static NeuralNetwork? For(BackgammonVersion version) => Loaded[(int)version].Value;

        public static string ResourceName(BackgammonVersion version) => $"Backgammon.AI.Neural.Weights.{version.ToString().ToLowerInvariant()}.bin";

        private static NeuralNetwork? Load(BackgammonVersion version)
        {
            if (Array.IndexOf(Shipped, version) < 0)
            {
                return null;
            }

            using var stream = typeof(Networks).Assembly.GetManifestResourceStream(ResourceName(version))
                ?? throw new InvalidOperationException($"The {version} network is missing from the package.");
            var network = NeuralNetwork.Read(stream);
            if (network.Version != version)
            {
                throw new InvalidDataException($"The {version} weights are for {network.Version}.");
            }

            return network;
        }
    }
}
