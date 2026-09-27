namespace Backgammon.AI.Search
{
    /// <summary>How a level searches.</summary>
    /// <param name="Noise">The standard deviation of the Gaussian noise added to each play's match-winning chance.</param>
    /// <param name="LookAhead">How many of the best plays are played on through the known stages that follow (0: none).</param>
    /// <param name="EvaluationBudget">A cap on evaluations for the look-ahead, which keeps every decision fast.</param>
    /// <param name="OneRoll">
    /// How many of the best plays that end the turn are looked at one roll deeper (0: none): the opponent's best reply
    /// to each of its 21 rolls, averaged by their chances (an escalating double played out stage by stage).
    /// </param>
    internal readonly record struct SearchSettings(double Noise, int LookAhead, int EvaluationBudget, int OneRoll = 0);
}
