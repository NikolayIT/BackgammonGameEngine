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
    }
}
