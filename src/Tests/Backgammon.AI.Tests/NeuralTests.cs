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
        [InlineData(12, 7)] // an unknown version
        [InlineData(20, 0)] // no hidden layer: it used to load as a constant network
        [InlineData(20, -8)] // it used to throw OverflowException
        [InlineData(20, (1 << 24) + 8)] // 256 inputs times this overflows to a first layer of 2,048 weights
        public void AFileWithAnImpossibleSizeOrVersionShouldBeRefused(int offset, int value)
        {
            var network = NeuralNetwork.CreateRandom(BackgammonVersion.Obiknovena, FeatureEncoder.Inputs, 8, seed: 1);
            using var file = new MemoryStream();
            network.Write(file);
            var bytes = file.ToArray();
            BitConverter.GetBytes(value).CopyTo(bytes, offset);

            Assert.Throws<InvalidDataException>(() => NeuralNetwork.Read(new MemoryStream(bytes)));
        }

        [Theory]
        [InlineData(1, 11)]
        [InlineData(2, 12)]
        [InlineData(3, 14)]
        [InlineData(4, 15)]
        [InlineData(5, 15)]
        [InlineData(6, 17)]
        [InlineData(7, 6)]
        [InlineData(8, 6)]
        [InlineData(9, 5)]
        [InlineData(10, 3)]
        [InlineData(11, 2)]
        [InlineData(12, 3)]
        public void OneCheckerShouldHitABlotWithTheStandardNumberOfRolls(int distance, int rolls)
        {
            // Seat 0's checker on its 20 and seat 1's lone checker `distance` pips ahead, with nothing in between: the
            // two blots face each other, so each hits the other with the same rolls.
            var position = Board(BackgammonVersion.Obiknovena, (0, 20, 1), (1, Geometry.Other(BackgammonVersion.Obiknovena, 20 - distance), 1));

            Assert.Equal(rolls, FeatureEncoder.HittingRolls(position, 0));
            Assert.Equal(rolls, FeatureEncoder.HittingRolls(position, 1));
        }

        [Fact]
        public void HittingRollsShouldRespectBlockedPointsAndTheBar()
        {
            // 8 pips away with seat 1's point 4 pips ahead: 4-4 and 2-2 are blocked, 6-2 and 5-3 are not.
            var blocked = Board(BackgammonVersion.Obiknovena, (0, 20, 1), (1, Geometry.Other(BackgammonVersion.Obiknovena, 12), 1), (1, Geometry.Other(BackgammonVersion.Obiknovena, 16), 2));
            Assert.Equal(4, FeatureEncoder.HittingRolls(blocked, 0));

            // From the bar, a blot on the entry point 20 is hit by any 5, 4-1 or 3-2.
            var bar = Board(BackgammonVersion.Obiknovena, (0, Geometry.Bar, 1), (1, Geometry.Other(BackgammonVersion.Obiknovena, 20), 1));
            Assert.Equal(15, FeatureEncoder.HittingRolls(bar, 0));

            // No hitting in гюлбара, and a тапа pin counts like a hit.
            Assert.Equal(0, FeatureEncoder.HittingRolls(Board(BackgammonVersion.Gyulbara, (0, 20, 1), (1, Geometry.Other(BackgammonVersion.Gyulbara, 14), 1)), 0));
            Assert.Equal(17, FeatureEncoder.HittingRolls(Board(BackgammonVersion.Tapa, (0, 20, 1), (1, Geometry.Other(BackgammonVersion.Tapa, 14), 1)), 0));
        }

        [Theory]
        [InlineData(BackgammonVariant.Obiknovena)]
        [InlineData(BackgammonVariant.Tapa)]
        [InlineData(BackgammonVariant.Chelebi)]
        public void HittingRollsShouldCountExactlyAsTheNetworksWereTrained(BackgammonVariant variant)
        {
            // Positions from random matches, the bar and pins included, for both seats.
            var positions = 0;
            for (var seed = 0; positions < 5_000; seed++)
            {
                var dice = new Random(seed);
                var match = new BackgammonMatch(new BackgammonMatchOptions { Variant = variant, Dice = (n, _) => dice.Next(n), RecordHistory = false });
                var random = new Random(seed);
                match.Start();
                while (!match.IsFinished)
                {
                    var view = match.GetView(0);
                    var position = ViewConverter.ToPosition(view.Version, view.Board);
                    Assert.Equal(HittingRollsReference.Of(position, 0), FeatureEncoder.HittingRolls(position, 0));
                    Assert.Equal(HittingRollsReference.Of(position, 1), FeatureEncoder.HittingRolls(position, 1));
                    positions++;
                    var moves = match.GetStageMoves();
                    match.Act(match.ToMove, new BackgammonAction { Steps = moves.Outcomes[random.Next(moves.OutcomeCount)].Steps });
                }
            }
        }

        [Fact]
        public void TheLatestLayoutShouldAddItsInputsAfterTheFirstLayouts()
        {
            var position = Position.Start(BackgammonVersion.Obiknovena);
            Span<int> indices1 = stackalloc int[FeatureEncoder.MaxActive];
            Span<float> values1 = stackalloc float[FeatureEncoder.MaxActive];
            Span<int> indices2 = stackalloc int[FeatureEncoder.MaxActive];
            Span<float> values2 = stackalloc float[FeatureEncoder.MaxActive];

            var count1 = FeatureEncoder.Encode(1, position, 0, new RollCounts(1, 0), indices1, values1);
            var count2 = FeatureEncoder.Encode(2, position, 0, new RollCounts(1, 0), indices2, values2);

            Assert.True(indices2[..count1].SequenceEqual(indices1[..count1]) && values2[..count1].SequenceEqual(values1[..count1]));
            Assert.All(indices2[count1..count2].ToArray(), index => Assert.InRange(index, FeatureEncoder.Inputs, FeatureEncoder.InputsOf(2) - 1));
            Assert.Equal(FeatureEncoder.LongestBlock(position, 0), FeatureEncoder.LongestBlock(position, 1));
        }

        [Fact]
        public void AWarmStartCopyShouldCarryTheVersionItIsTrainedFor()
        {
            // The trainer's --init copies another version's network; its files must be tagged with the new version,
            // or the bots refuse the network on its first decision.
            var obiknovena = NeuralNetwork.CreateRandom(BackgammonVersion.Obiknovena, FeatureEncoder.Inputs, 8, seed: 1);

            var chelebi = obiknovena.Copy("init", BackgammonVersion.Chelebi);

            Assert.Equal(BackgammonVersion.Chelebi, chelebi.Version);
            Assert.Equal(BackgammonVersion.Chelebi, chelebi.Copy("saved").Version);
            Assert.Equal(obiknovena.W1, chelebi.W1);
            Assert.Equal(obiknovena.B2, chelebi.B2);
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
                [BackgammonVersion.Obiknovena] = "ae03ac24fb73d81f7938d1c87e04a8369c0745edd778c806761eb1ccdae3395f",
                [BackgammonVersion.Gyulbara] = "93f7bb52acb1d662eee60a81c3470fc7da1d986e47d8b82983a3d912a8033097",
                [BackgammonVersion.Chelebi] = "da6a0fb92f3e1b1f4a21296370afc9b9f57f5d838dd16324b296c5416e9347f0",
                [BackgammonVersion.Tapa] = "99febb2037d17e2667d5dd228f582fa217f5430a7674056f6d89d644ba8474d6",
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

        // A position with the given checkers (seat, own point, count); every other checker is borne off.
        private static Position Board(BackgammonVersion version, params (int Seat, int Point, int Count)[] checkers)
        {
            var position = new Position { Version = version };
            var off = new[] { 15, 15 };
            foreach (var (seat, point, count) in checkers)
            {
                position.Add(seat, point, count);
                off[seat] -= count;
            }

            position.Set(0, Geometry.Off, off[0]);
            position.Set(1, Geometry.Off, off[1]);
            return position;
        }
    }
}
