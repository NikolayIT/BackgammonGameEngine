namespace Backgammon.AI.Tests
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;

    using Backgammon.Logic;
    using Xunit;

    /// <summary>
    /// A bot must choose the same plays on every machine: the host may replay a game elsewhere, and the arena's numbers
    /// must hold for everyone. Each case plays a whole match between two levels from fixed seeds and pins a hash of its
    /// record. The hashes were computed on Windows x64, and CI checks them on Linux.
    /// </summary>
    public class CrossMachineTests
    {
        public static TheoryData<BackgammonVariant, int, int, string> Matches => new()
        {
            { BackgammonVariant.Obiknovena, 6, 3, "a15762754ab4bdee28dc93948c0e33f9198250f74d091c9013fdac739901f797" },
            { BackgammonVariant.Gyulbara, 6, 2, "434c4114109ed97c0cc6b92bd086513786b07392d8b083d2b9118685258a0355" },
            { BackgammonVariant.Tapa, 5, 6, "0b4d9394be41b6bb19d540b0dfaeb2306218353adca69dd154fd519569ad91a3" },
            { BackgammonVariant.Chelebi, 6, 4, "22aecfe8e1160989b7e93005058ac1019a7ee3fc2d68e5972d43dd24a8046195" },
            { BackgammonVariant.Sreshta, 6, 1, "6763a3e666e1fbb105bd9183b687d9fa989b3dc3773694249aeaa2c5f8c15870" },
        };

        [Theory]
        [MemberData(nameof(Matches))]
        public void ABotMatchShouldPlayTheSameOnEveryMachine(BackgammonVariant variant, int level0, int level1, string expected)
        {
            var dice = new Random(2026);
            var match = new BackgammonMatch(new BackgammonMatchOptions { Variant = variant, Dice = (n, _) => dice.Next(n) });
            var randoms = new[] { new Random(1), new Random(2) };
            var levels = new[] { level0, level1 };
            match.Start();
            while (!match.IsFinished)
            {
                var seat = match.ToMove;
                Assert.Equal(BackgammonActResult.Ok, match.Act(seat, BackgammonBot.Choose(match.GetView(seat), levels[seat], randoms[seat])));
            }

            var record = JsonSerializer.Serialize(match.GetRecord());
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(record)));
            Assert.Equal(expected, hash);
        }
    }
}
