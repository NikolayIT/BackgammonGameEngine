namespace Backgammon.Logic.Tests.Support
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Xunit;

    /// <summary>Plays matches in tests: canonical or random legal stages, step by step when asked.</summary>
    internal static class MatchDriver
    {
        /// <summary>Plays the first outcome (in canonical order) of the stage to move.</summary>
        public static void PlayFirst(BackgammonMatch match)
        {
            var seat = match.ToMove;
            var steps = match.GetStageMoves().Outcomes[0].Steps;
            Assert.Equal(BackgammonActResult.Ok, match.Act(seat, new BackgammonAction { Steps = steps }));
        }

        /// <summary>
        /// Picks a random legal play. Half the time it takes a random outcome's canonical steps; otherwise it builds the
        /// stage one random legal next step at a time, the way a person taps it in.
        /// </summary>
        public static BackgammonAction RandomAction(BackgammonMatch match, Random random)
        {
            var moves = match.GetStageMoves();
            if (random.Next(2) == 0)
            {
                return new BackgammonAction { Steps = moves.Outcomes[random.Next(moves.OutcomeCount)].Steps };
            }

            var steps = new List<BackgammonStep>();
            while (true)
            {
                var next = moves.NextSteps(steps);
                if (next.Count == 0)
                {
                    break;
                }

                steps.Add(next[random.Next(next.Count)].Step);
            }

            return new BackgammonAction { Steps = steps.ToArray() };
        }

        /// <summary>Plays random legal stages to the end of the match; returns the number of actions.</summary>
        public static int PlayOut(BackgammonMatch match, Random random, int maxActions = 100_000)
        {
            var actions = 0;
            while (!match.IsFinished)
            {
                Assert.True(actions++ < maxActions, "The match did not finish.");
                var seat = match.ToMove;
                Assert.Equal(BackgammonActResult.Ok, match.Act(seat, RandomAction(match, random)));
            }

            return actions;
        }

        public static BackgammonAction Steps(params (int From, int Die)[] steps) =>
            new() { Steps = steps.Select(s => new BackgammonStep(s.From, s.Die)).ToArray() };
    }
}
