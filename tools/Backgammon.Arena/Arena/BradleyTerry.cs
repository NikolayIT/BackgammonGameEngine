namespace Backgammon.Arena
{
    using System;

    /// <summary>
    /// Fits Bradley-Terry strengths to a table of wins by the MM algorithm, with a small prior (half a win against every
    /// other player) so that a player who never lost or never won still gets a finite rating. The ratings are in Elo
    /// points: a difference of 400 means 10 to 1.
    /// </summary>
    internal static class BradleyTerry
    {
        public static double[] Fit(double[,] wins)
        {
            var n = wins.GetLength(0);
            var strength = new double[n];
            Array.Fill(strength, 1.0);
            for (var iteration = 0; iteration < 5_000; iteration++)
            {
                var next = new double[n];
                for (var i = 0; i < n; i++)
                {
                    double won = 0, weight = 0;
                    for (var j = 0; j < n; j++)
                    {
                        if (i == j)
                        {
                            continue;
                        }

                        var games = wins[i, j] + wins[j, i] + 1.0;
                        won += wins[i, j] + 0.5;
                        weight += games / (strength[i] + strength[j]);
                    }

                    next[i] = won / weight;
                }

                // Normalise to a geometric mean of 1.
                var logMean = 0.0;
                for (var i = 0; i < n; i++)
                {
                    logMean += Math.Log(next[i]) / n;
                }

                for (var i = 0; i < n; i++)
                {
                    strength[i] = next[i] / Math.Exp(logMean);
                }
            }

            var ratings = new double[n];
            for (var i = 0; i < n; i++)
            {
                ratings[i] = 400 * Math.Log10(strength[i]);
            }

            return ratings;
        }
    }
}
