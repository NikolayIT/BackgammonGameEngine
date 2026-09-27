namespace Backgammon.Arena
{
    using System;

    /// <summary>The totals of many matches between two players, from the first player's side.</summary>
    internal sealed class PairStats
    {
        public PairStats(string a, string b)
        {
            this.A = a;
            this.B = b;
        }

        public string A { get; }

        public string B { get; }

        public int Matches { get; private set; }

        public int WinsA { get; private set; }

        public long PointsA { get; private set; }

        public long PointsB { get; private set; }

        public long Games { get; private set; }

        public long Plies { get; private set; }

        public long DecisionsA { get; private set; }

        public long DecisionsB { get; private set; }

        public int[] Kinds { get; } = new int[5];

        public double WinRate => this.Matches == 0 ? 0 : (double)this.WinsA / this.Matches;

        /// <summary>Gets the standard error of the win rate.</summary>
        public double Error => this.Matches == 0 ? 0 : Math.Sqrt(this.WinRate * (1 - this.WinRate) / this.Matches);

        /// <summary>Gets the rating difference the win rate implies (logistic, 400 points a factor of 10).</summary>
        public double Elo => -400 * Math.Log10((1 / Math.Clamp(this.WinRate, 0.001, 0.999)) - 1);

        public void Add(MatchStats match, int seatOfA)
        {
            this.Matches++;
            if (match.Winner == seatOfA)
            {
                this.WinsA++;
            }

            this.PointsA += match.Scores[seatOfA];
            this.PointsB += match.Scores[1 - seatOfA];
            this.Games += match.Games;
            this.Plies += match.Plies;
            this.DecisionsA += match.Decisions[seatOfA];
            this.DecisionsB += match.Decisions[1 - seatOfA];
            for (var kind = 0; kind < this.Kinds.Length; kind++)
            {
                this.Kinds[kind] += match.Kinds[kind];
            }
        }

        public override string ToString() =>
            $"{this.A} vs {this.B}: {this.WinsA}/{this.Matches} = {this.WinRate:P1} ± {1.96 * this.Error:P1} (Elo {this.Elo:+0;-0}), " +
            $"points {this.PointsA}:{this.PointsB}, {(double)this.Games / this.Matches:F2} games and {(double)(this.DecisionsA + this.DecisionsB) / this.Matches:F0} decisions a match " +
            $"(normal {this.Kinds[0]}, mars {this.Kinds[1]}, mother {this.Kinds[2]}, draw {this.Kinds[3]}, stuck {this.Kinds[4]})";
    }
}
