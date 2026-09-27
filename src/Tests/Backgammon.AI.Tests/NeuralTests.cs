namespace Backgammon.AI.Tests
{
    using System;
    using System.IO;
    using System.Linq;

    using Backgammon.AI.Evaluation;
    using Backgammon.AI.Neural;
    using Backgammon.Logic;
    using Backgammon.Logic.Rules;
    using Xunit;

    /// <summary>The network's arithmetic, file format and inputs.</summary>
    public class NeuralTests
    {
        [Fact]
        public void ThePortableExpShouldMatchTheRuntimes()
        {
            for (var x = -30f; x <= 30f; x += 0.01f)
            {
                var expected = Math.Exp(x);
                Assert.True(Math.Abs(NeuralMath.Exp(x) - expected) / expected < 1e-6, $"exp({x})");
            }

            Assert.Equal(0.5f, NeuralMath.Sigmoid(0));
            Assert.True(NeuralMath.Sigmoid(-200) >= 0 && NeuralMath.Sigmoid(200) <= 1);
        }

        [Fact]
        public void ANetworkShouldReadBackFromItsFileAndEvaluateTheSame()
        {
            var network = NeuralNetwork.CreateRandom(BackgammonVersion.Tapa, FeatureEncoder.Inputs, 64, seed: 3);
            using var file = new MemoryStream();
            network.Write(file);
            file.Position = 0;
            var copy = NeuralNetwork.Read(file);

            Assert.Equal(network.Version, copy.Version);
            Assert.Equal(network.Description, copy.Description);
            var position = Position.Start(BackgammonVersion.Tapa);
            var first = new NeuralEvaluator(network).Evaluate(position, 0, new RollCounts(1, 0));
            var second = new NeuralEvaluator(copy).Evaluate(position, 0, new RollCounts(1, 0));
            Assert.Equal(first.Win, second.Win);
            Assert.Equal(first.WinDouble, second.WinDouble);
            Assert.Equal(first.LoseDouble, second.LoseDouble);
            Assert.InRange(first.Win, 0, 1);
        }

        [Fact]
        public void AFileForAnotherLayoutShouldBeRefused()
        {
            var network = NeuralNetwork.CreateRandom(BackgammonVersion.Obiknovena, FeatureEncoder.Inputs, 8, seed: 1);
            using var file = new MemoryStream();
            network.Write(file);
            var bytes = file.ToArray();
            bytes[8] = 99;

            Assert.Throws<InvalidDataException>(() => NeuralNetwork.Read(new MemoryStream(bytes)));
        }

        [Theory]
        [InlineData(BackgammonVersion.Obiknovena)]
        [InlineData(BackgammonVersion.Gyulbara)]
        [InlineData(BackgammonVersion.Tapa)]
        [InlineData(BackgammonVersion.Chelebi)]
        public void TheStartShouldLookTheSameToBothSeats(BackgammonVersion version)
        {
            var position = Position.Start(version);
            Span<int> indices0 = stackalloc int[FeatureEncoder.MaxActive];
            Span<float> values0 = stackalloc float[FeatureEncoder.MaxActive];
            Span<int> indices1 = stackalloc int[FeatureEncoder.MaxActive];
            Span<float> values1 = stackalloc float[FeatureEncoder.MaxActive];

            var count0 = FeatureEncoder.Encode(position, 0, new RollCounts(2, 2), indices0, values0);
            var count1 = FeatureEncoder.Encode(position, 1, new RollCounts(2, 2), indices1, values1);

            Assert.Equal(count0, count1);
            Assert.True(indices0[..count0].SequenceEqual(indices1[..count1]));
            Assert.True(values0[..count0].SequenceEqual(values1[..count1]));
            for (var i = 1; i < count0; i++)
            {
                Assert.True(indices0[i] > indices0[i - 1]);
            }
        }

        [Fact]
        public void TheShippedNetworksShouldBeTheOnesRecordedInNeuralNetworkMd()
        {
            var expected = new System.Collections.Generic.Dictionary<BackgammonVersion, string>
            {
                [BackgammonVersion.Obiknovena] = "bb7b6adeb7719185bec7fb95328244960ab3a38d793a1a7b9dcf3c126a4c50f5",
                [BackgammonVersion.Gyulbara] = "93f7bb52acb1d662eee60a81c3470fc7da1d986e47d8b82983a3d912a8033097",
                [BackgammonVersion.Tapa] = "e63b0edf808e5c9cf36dc80d6bc034024ab9e1b358475d382b0359304b756a4a",
            };

            Assert.Equal(expected.Keys.OrderBy(v => v), Networks.Shipped.OrderBy(v => v));
            foreach (var (version, hash) in expected)
            {
                using var stream = typeof(Networks).Assembly.GetManifestResourceStream(Networks.ResourceName(version))!;
                Assert.Equal(hash, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(stream)));
                Assert.Equal(version, Networks.For(version)!.Version);
            }
        }

        [Fact]
        public void EveryVersionWithoutAShippedNetworkShouldUseTheBaseline()
        {
            foreach (var version in Enum.GetValues<BackgammonVersion>())
            {
                var shipped = Array.IndexOf(Networks.Shipped, version) >= 0;
                Assert.Equal(shipped, Networks.For(version) != null);
                Assert.Equal(shipped, BotLevels.EvaluatorFor(version) is NeuralEvaluator);
            }
        }
    }
}
