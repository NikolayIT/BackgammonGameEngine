namespace Backgammon.AI.Evaluation
{
    using System;
    using System.Collections.Concurrent;

    using Backgammon.Logic;

    /// <summary>
    /// The chance to win the match from a score, for equal players: the match equity table. It is small and computed
    /// once per variant and target. It follows the rules of the match: the target has to be reached with a lead, a
    /// тапа draw gives both players a point, and a среща rotates the versions. The share of 2-point results per
    /// version comes from self-play. A bot ranks its plays by match-winning chance, so a марс counts only when it
    /// changes the chance to win the match.
    /// </summary>
    internal sealed class MatchEquity
    {
        private static readonly ConcurrentDictionary<(BackgammonVariant, int), MatchEquity> Cache = new();

        private readonly BackgammonVariant variant;
        private readonly int target;
        private readonly int size;

        // [a, b, phase]: the chance that the player on a points wins against b points, when the next game is number
        // n with (n - 1) mod 3 == phase (the phase only matters for a среща).
        private readonly double[,,] table;

        private MatchEquity(BackgammonVariant variant, int target)
        {
            this.variant = variant;
            this.target = target;
            this.size = target + 3;
            this.table = new double[this.size, this.size, 3];
            for (var a = this.size - 1; a >= 0; a--)
            {
                for (var b = this.size - 1; b >= 0; b--)
                {
                    for (var phase = 0; phase < 3; phase++)
                    {
                        this.table[a, b, phase] = this.Compute(a, b, phase);
                    }
                }
            }
        }

        public static MatchEquity For(BackgammonVariant variant, int target) => Cache.GetOrAdd((variant, target), key => new MatchEquity(key.Item1, key.Item2));

        /// <summary>
        /// The share of decisive games won by 2 points, and of games drawn, per version, as measured in self-play by
        /// the strongest bots.
        /// </summary>
        public static (double Double, double Draw) Rates(BackgammonVersion version) => version switch
        {
            BackgammonVersion.Obiknovena => (0.24, 0),
            BackgammonVersion.Gyulbara => (0.12, 0),
            BackgammonVersion.Tapa => (0.30, 0.02),
            _ => (0.28, 0),
        };

        /// <summary>The chance that the player on <paramref name="mine"/> points wins the match.</summary>
        /// <param name="mine">The player's points.</param>
        /// <param name="theirs">The opponent's points.</param>
        /// <param name="nextGame">The number of the next game to be played.</param>
        /// <returns>The match-winning chance, 0..1.</returns>
        public double Get(int mine, int theirs, int nextGame)
        {
            if (Math.Max(mine, theirs) >= this.target && mine != theirs)
            {
                return mine > theirs ? 1 : 0;
            }

            if (mine == theirs)
            {
                return 0.5;
            }

            var phase = (nextGame - 1) % 3;
            if (mine < this.size && theirs < this.size)
            {
                return this.table[mine, theirs, phase];
            }

            return this.Compute(mine, theirs, phase);
        }

        /// <summary>
        /// The match-winning chances after the current game, numbered <paramref name="game"/>, ends each way, for the
        /// player on <paramref name="mine"/> points.
        /// </summary>
        public (double WinSingle, double WinDouble, double LoseSingle, double LoseDouble, double Draw) AfterGame(int mine, int theirs, int game) =>
            (this.Get(mine + 1, theirs, game + 1),
             this.Get(mine + 2, theirs, game + 1),
             this.Get(mine, theirs + 1, game + 1),
             this.Get(mine, theirs + 2, game + 1),
             this.Get(mine + 1, theirs + 1, game + 1));

        /// <summary>The match-winning chance of an outcome of the current game.</summary>
        public double Of(in Outcome outcome, int mine, int theirs, int game)
        {
            var after = this.AfterGame(mine, theirs, game);
            return (outcome.WinSingle * after.WinSingle) + (outcome.WinDouble * after.WinDouble) + (outcome.LoseSingle * after.LoseSingle)
                + (outcome.LoseDouble * after.LoseDouble) + (outcome.Draw * after.Draw);
        }

        private double Compute(int a, int b, int phase)
        {
            if (Math.Max(a, b) >= this.target && a != b)
            {
                return a > b ? 1 : 0;
            }

            if (a == b)
            {
                // Equal players on equal scores: an even match, whatever the rules of the next games.
                return 0.5;
            }

            var version = this.variant == BackgammonVariant.Sreshta ? (BackgammonVersion)phase : (BackgammonVersion)(int)this.variant;
            var (doubled, draw) = Rates(version);
            var decisive = (1 - draw) / 2;
            var next = this.variant == BackgammonVariant.Sreshta ? (phase + 1) % 3 : phase;
            return (decisive * (1 - doubled) * this.Lookup(a + 1, b, next))
                + (decisive * doubled * this.Lookup(a + 2, b, next))
                + (decisive * (1 - doubled) * this.Lookup(a, b + 1, next))
                + (decisive * doubled * this.Lookup(a, b + 2, next))
                + (draw * this.Lookup(a + 1, b + 1, next));
        }

        private double Lookup(int a, int b, int phase)
        {
            if (Math.Max(a, b) >= this.target && a != b)
            {
                return a > b ? 1 : 0;
            }

            if (a == b)
            {
                return 0.5;
            }

            return a < this.size && b < this.size ? this.table[a, b, phase] : this.Compute(a, b, phase);
        }
    }
}
