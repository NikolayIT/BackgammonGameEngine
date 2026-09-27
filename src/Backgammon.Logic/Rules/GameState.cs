namespace Backgammon.Logic.Rules
{
    using System.Collections.Generic;

    /// <summary>The state of the game in progress: the position, the roll in play and its stages, and the history.</summary>
    internal sealed class GameState
    {
        public GameState(int number, BackgammonVersion version)
        {
            this.Number = number;
            this.Version = version;
            this.Position = Position.Start(version);
        }

        public int Number { get; }

        public BackgammonVersion Version { get; }

        public Position Position { get; set; }

        public int Starter { get; set; }

        public List<IReadOnlyList<int>> Openings { get; } = new();

        public List<BackgammonPlay> Plays { get; } = new();

        public int[] RollsMade { get; } = new int[2];

        public int StuckRolls { get; set; }

        public BackgammonGameResult? Result { get; set; }

        // The roll in play.
        public int RollSeat { get; set; }

        public int FirstDie { get; set; }

        public int SecondDie { get; set; }

        public int RollNumber { get; set; }

        public bool IsOpeningRoll { get; set; }

        public bool IsEscalating { get; set; }

        /// <summary>Gets or sets how many dice the roller has played in this roll's chain so far.</summary>
        public int ChainPlayed { get; set; }

        public bool MovedThisRoll { get; set; }

        /// <summary>Gets or sets a value indicating whether the roll in play has a play recorded yet.</summary>
        public bool RollPlayed { get; set; }

        // The stage in play: the roller's own stages, or the remainder's.
        public List<StageDice> Stages { get; } = new();

        public int StageIndex { get; set; }

        public bool IsRemainder { get; set; }

        public int Mover { get; set; }

        public StageDice Stage => this.Stages[this.StageIndex];

        // The legal plays of the stage in play, found when it started.
        public int PlayableDice { get; set; }

        public BackgammonGameRecord ToRecord() => new()
        {
            GameNumber = this.Number,
            Version = this.Version,
            Openings = this.Openings.ToArray(),
            Starter = this.Starter,
            Plays = this.Plays.ToArray(),
            PendingRoll = this.Result == null && !this.IsOpeningRoll && !this.RollPlayed ? new[] { this.FirstDie, this.SecondDie } : System.Array.Empty<int>(),
            Result = this.Result,
        };
    }
}
