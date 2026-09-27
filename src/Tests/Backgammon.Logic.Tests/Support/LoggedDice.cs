namespace Backgammon.Logic.Tests.Support
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A dice source for tests that logs every draw (purpose, n, the die value). It either plays a script of die values
    /// (1..6), failing when the script runs out, or rolls from a seeded <see cref="Random"/>.
    /// </summary>
    internal sealed class LoggedDice
    {
        private readonly Queue<int>? script;
        private readonly Random? random;

        private LoggedDice(Queue<int>? script, Random? random)
        {
            this.script = script;
            this.random = random;
        }

        public List<(string Purpose, int N, int Die)> Log { get; } = new();

        public int ScriptLeft => this.script?.Count ?? 0;

        public Func<int, string, int> Source => this.Next;

        public static LoggedDice Script(params int[] dice) => new(new Queue<int>(dice), null);

        public static LoggedDice Seeded(int seed) => new(null, new Random(seed));

        /// <summary>The draws a record implies, in order: see BackgammonMatchOptions.Dice.</summary>
        public static List<(string Purpose, int Die)> DrawsOf(BackgammonMatchRecord record)
        {
            var draws = new List<(string Purpose, int Die)>();
            foreach (var game in record.Games)
            {
                foreach (var pair in game.Openings)
                {
                    draws.Add((BackgammonMatchOptions.OpeningPurpose, pair[0]));
                    draws.Add((BackgammonMatchOptions.OpeningPurpose, pair[1]));
                }

                foreach (var play in game.Plays)
                {
                    if (play.Stage == 0 && !play.IsRemainder && !play.IsOpeningRoll)
                    {
                        draws.Add((BackgammonMatchOptions.DicePurpose, play.Roll[0]));
                        draws.Add((BackgammonMatchOptions.DicePurpose, play.Roll[1]));
                    }
                }

                foreach (var die in game.PendingRoll)
                {
                    draws.Add((BackgammonMatchOptions.DicePurpose, die));
                }
            }

            return draws;
        }

        private int Next(int n, string purpose)
        {
            if (n != 6)
            {
                throw new InvalidOperationException($"Dice are drawn with n = 6, not {n}.");
            }

            int die;
            if (this.script != null)
            {
                if (this.script.Count == 0)
                {
                    throw new InvalidOperationException("The dice script ran out.");
                }

                die = this.script.Dequeue();
            }
            else
            {
                die = this.random!.Next(1, 7);
            }

            this.Log.Add((purpose, n, die));
            return die - 1;
        }
    }
}
