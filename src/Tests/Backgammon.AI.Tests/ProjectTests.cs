namespace Backgammon.AI.Tests
{
    using Backgammon.Logic;
    using Xunit;

    public class ProjectTests
    {
        [Fact]
        public void TheBotsShouldBuildOnTheEngine()
        {
            Assert.Equal(4, (int)BackgammonVariant.Sreshta);
        }
    }
}
