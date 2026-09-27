namespace Backgammon.Arena
{
    using System.Collections.Generic;

    /// <summary>What happened in one arena match.</summary>
    internal sealed class MatchStats
    {
        public int Winner { get; set; } = -1;

        public int[] Scores { get; set; } = new int[2];

        public int Games { get; set; }

        public int Plies { get; set; }

        public int[] Decisions { get; } = new int[2];

        public List<double>[] DecisionMicroseconds { get; } = { new(), new() };

        public int Illegal { get; set; }

        // Games by result kind (BackgammonResultKind order).
        public int[] Kinds { get; } = new int[5];
    }
}
