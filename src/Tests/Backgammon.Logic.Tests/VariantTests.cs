namespace Backgammon.Logic.Tests
{
    using System;

    using Xunit;

    public class VariantTests
    {
        [Fact]
        public void EverySingleVersionVariantShouldNameAVersionWithTheSameNumber()
        {
            foreach (var version in Enum.GetValues<BackgammonVersion>())
            {
                Assert.Equal(version.ToString(), ((BackgammonVariant)(int)version).ToString());
            }
        }

        [Fact]
        public void TheVersionOfAGameShouldFollowTheRotationAndRefuseImpossibleGames()
        {
            Assert.Equal(BackgammonVersion.Obiknovena, BackgammonMatch.VersionOf(BackgammonVariant.Sreshta, 1));
            Assert.Equal(BackgammonVersion.Gyulbara, BackgammonMatch.VersionOf(BackgammonVariant.Sreshta, 5));
            Assert.Equal(BackgammonVersion.Tapa, BackgammonMatch.VersionOf(BackgammonVariant.Sreshta, 6));
            Assert.Equal(BackgammonVersion.Chelebi, BackgammonMatch.VersionOf(BackgammonVariant.Chelebi, 7));

            // Game 0 used to give (BackgammonVersion)(-1), and an unknown variant passed straight through.
            Assert.Throws<ArgumentOutOfRangeException>(() => BackgammonMatch.VersionOf(BackgammonVariant.Sreshta, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => BackgammonMatch.VersionOf(BackgammonVariant.Tapa, -3));
            Assert.Throws<ArgumentOutOfRangeException>(() => BackgammonMatch.VersionOf((BackgammonVariant)9, 1));
        }
    }
}
