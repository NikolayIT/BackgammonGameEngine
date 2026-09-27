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
            { BackgammonVariant.Obiknovena, 6, 3, "8dfdf9b03e02435d97b6ca6e2ac347ddc9767acc80ef92ac66f2748a78cf3039" },
            { BackgammonVariant.Gyulbara, 6, 2, "cdbadc082eb8704932649a6bc1224fc60e14d840f42227954b73c59c49cb6dc0" },
            { BackgammonVariant.Tapa, 5, 6, "825b9289548568f0ffb44e4d45ad7f8711ef6f963bf747349ca084c8b854ea38" },
            { BackgammonVariant.Chelebi, 6, 4, "3f25185f2ab000500e105aff55145d1fb1801ab58896b5fc6d026858bcf398eb" },
            { BackgammonVariant.Sreshta, 6, 1, "040edf92d63143599676354b597b0070c7a98f49aaa8914304eeef24d8a06db8" },
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
