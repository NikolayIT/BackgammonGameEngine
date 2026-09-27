namespace Backgammon.AI.Evaluation
{
    using System;

    /// <summary>
    /// The chances of how a game ends, from one player's point of view: a win (of which a марс or a майка, 2 points),
    /// a loss (of which 2 points), and a draw (тапа's double mother). The five parts add up to 1.
    /// </summary>
    internal readonly struct Outcome
    {
        public Outcome(double winSingle, double winDouble, double loseSingle, double loseDouble, double draw)
        {
            this.WinSingle = winSingle;
            this.WinDouble = winDouble;
            this.LoseSingle = loseSingle;
            this.LoseDouble = loseDouble;
            this.Draw = draw;
        }

        public static Outcome Drawn => new(0, 0, 0, 0, 1);

        public double WinSingle { get; }

        public double WinDouble { get; }

        public double LoseSingle { get; }

        public double LoseDouble { get; }

        public double Draw { get; }

        public double Win => this.WinSingle + this.WinDouble;

        public static Outcome Won(bool doubled) => doubled ? new Outcome(0, 1, 0, 0, 0) : new Outcome(1, 0, 0, 0, 0);

        /// <summary>
        /// Builds an outcome from a network's three outputs: the chance to win, to win with 2 points, and to lose with
        /// 2 points. Inconsistent outputs are clamped.
        /// </summary>
        public static Outcome FromNetwork(double win, double winDouble, double loseDouble)
        {
            win = Math.Clamp(win, 0, 1);
            winDouble = Math.Clamp(winDouble, 0, win);
            loseDouble = Math.Clamp(loseDouble, 0, 1 - win);
            return new Outcome(win - winDouble, winDouble, 1 - win - loseDouble, loseDouble, 0);
        }

        /// <summary>The same outcome seen by the other player.</summary>
        public Outcome Flip() => new(this.LoseSingle, this.LoseDouble, this.WinSingle, this.WinDouble, this.Draw);

        /// <summary>Points won minus points lost, on average (a draw is even).</summary>
        public double Points() => this.WinSingle + (2 * this.WinDouble) - this.LoseSingle - (2 * this.LoseDouble);

        public override string ToString() => $"win {this.Win:P1} (2pt {this.WinDouble:P1}) lose 2pt {this.LoseDouble:P1} draw {this.Draw:P1}";
    }
}
