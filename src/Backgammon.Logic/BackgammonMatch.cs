namespace Backgammon.Logic
{
    using System;
    using System.Collections.Generic;

    using Backgammon.Logic.Rules;

    /// <summary>
    /// A match of табла, driven one action at a time: it never waits for a player and holds no thread between
    /// decisions. Call <see cref="Start"/>; then, while the match is not <see cref="IsFinished"/>, the seat in
    /// <see cref="ToMove"/> plays the current stage with <see cref="Act"/>. Dice are rolled automatically, stages
    /// with no legal move are passed automatically, and a remainder of an escalating chain is handed to the
    /// opponent, so <see cref="ToMove"/> is always the seat that has a real decision to make.
    /// </summary>
    /// <remarks>Not thread-safe: drive a match from one thread at a time (for example a table actor).</remarks>
    public sealed class BackgammonMatch
    {
        /// <summary>After this many rolls in a row that move no checker, the game ends as a draw with no points.</summary>
        public const int StuckRollLimit = 100;

        private readonly Func<int, string, int> dice;
        private readonly bool autoPlay;
        private readonly bool history;
        private readonly StageGenerator generator = new();
        private readonly List<StageEnd> ends = new();
        private readonly List<BackgammonGameRecord> finishedGames = new();
        private readonly List<BackgammonGameResult> results = new();
        private readonly int[] scores = new int[2];

        private GameState? game;
        private int ply;

        /// <summary>Initializes a new instance of the <see cref="BackgammonMatch"/> class.</summary>
        /// <param name="options">How the match is played; null for an обикновена match to 3 with <see cref="Random.Shared"/> dice.</param>
        public BackgammonMatch(BackgammonMatchOptions? options = null)
        {
            options ??= new BackgammonMatchOptions();
            if (!Enum.IsDefined(options.Variant))
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.Variant, "Unknown variant.");
            }

            if (options.TargetPoints is < 1 or > 1000)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.TargetPoints, "The target must be 1..1000 points.");
            }

            this.Variant = options.Variant;
            this.TargetPoints = options.TargetPoints ?? (options.Variant == BackgammonVariant.Sreshta ? 5 : 3);
            this.dice = options.Dice ?? ((n, _) => Random.Shared.Next(n));
            this.autoPlay = options.AutoPlayForcedStages;
            this.history = options.RecordHistory;
        }

        /// <summary>Gets what the match is played as.</summary>
        public BackgammonVariant Variant { get; }

        /// <summary>Gets the points a player needs to win the match, which it must reach while leading.</summary>
        public int TargetPoints { get; }

        /// <summary>Gets a value indicating whether <see cref="Start"/> has been called.</summary>
        public bool IsStarted { get; private set; }

        /// <summary>Gets the seat that has to play the current stage, or -1 before the start and once the match is over.</summary>
        public int ToMove { get; private set; } = -1;

        /// <summary>Gets a value indicating whether the match is over: won, or stopped.</summary>
        public bool IsFinished { get; private set; }

        /// <summary>Gets a value indicating whether the match was stopped with <see cref="Stop"/>.</summary>
        public bool IsStopped { get; private set; }

        /// <summary>Gets the seat that won the match, or -1 while it goes on and after <see cref="Stop"/>.</summary>
        public int Winner { get; private set; } = -1;

        /// <summary>Gets each seat's points.</summary>
        public IReadOnlyList<int> Scores => new[] { this.scores[0], this.scores[1] };

        /// <summary>Gets the current (or last) game's number, counting from 1; 0 before the start.</summary>
        public int GameNumber => this.game?.Number ?? 0;

        /// <summary>Gets the rules of the current (or last) game; before the start, of the first game.</summary>
        public BackgammonVersion Version => this.game?.Version ?? VersionOf(this.Variant, 1);

        /// <summary>Gets the number of plays made in the match (see <see cref="BackgammonPlay.Ply"/>).</summary>
        public int Ply => this.ply;

        internal GameState? CurrentGame => this.game;

        internal List<StageEnd> CurrentEnds => this.ends;

        /// <summary>Gets the rules game <paramref name="gameNumber"/> of a match is played by.</summary>
        /// <param name="variant">What the match is played as.</param>
        /// <param name="gameNumber">The game's number, counting from 1.</param>
        /// <returns>The version: the variant's own, or the среща rotation обикновена, гюлбара, тапа.</returns>
        public static BackgammonVersion VersionOf(BackgammonVariant variant, int gameNumber) => variant switch
        {
            BackgammonVariant.Sreshta => (BackgammonVersion)((gameNumber - 1) % 3),
            _ => (BackgammonVersion)(int)variant,
        };

        /// <summary>Starts the match: the first game's opening roll is drawn and played up to the first decision.</summary>
        public void Start()
        {
            if (this.IsStarted)
            {
                throw new InvalidOperationException("The match has already started.");
            }

            this.IsStarted = true;
            this.StartGame(1);
            this.Advance();
        }

        /// <summary>
        /// Says what <see cref="Act"/> would do with the action, without changing anything.
        /// </summary>
        /// <param name="seat">The acting seat, 0 or 1.</param>
        /// <param name="action">How it plays the current stage.</param>
        /// <returns><see cref="BackgammonActResult.Ok"/> when <see cref="Act"/> would play it; otherwise why not.</returns>
        public BackgammonActResult Validate(int seat, BackgammonAction action) => this.Check(seat, action, out _, out _);

        /// <summary>
        /// Plays the current stage for <paramref name="seat"/>, then goes on (rolling, passing stages with no legal move,
        /// handing over remainders, starting the next game) up to the next decision or the end of the match. Nothing
        /// changes unless the result is <see cref="BackgammonActResult.Ok"/>.
        /// </summary>
        /// <param name="seat">The acting seat, 0 or 1.</param>
        /// <param name="action">How it plays the current stage.</param>
        /// <returns><see cref="BackgammonActResult.Ok"/>, or why the action was refused.</returns>
        public BackgammonActResult Act(int seat, BackgammonAction action)
        {
            var result = this.Check(seat, action, out var end, out var gameEnd);
            if (result == BackgammonActResult.Ok)
            {
                this.PlayStage(StepSequence.Of(action.Steps), end, gameEnd, auto: false);
                this.Advance();
            }

            return result;
        }

        /// <summary>
        /// Ends the match now, for a resignation, a timeout or an abandoned table: it is finished, stopped and has no
        /// winner (the host decides the outcome). Does nothing to a match that is already over.
        /// </summary>
        public void Stop()
        {
            if (this.IsFinished)
            {
                return;
            }

            this.IsFinished = true;
            this.IsStopped = true;
            this.ToMove = -1;
        }

        /// <summary>Gets the view of <paramref name="seat"/>.</summary>
        /// <param name="seat">The seat, 0 or 1.</param>
        /// <returns>What the seat sees now.</returns>
        public BackgammonSeatView GetView(int seat)
        {
            if (seat != 0 && seat != 1)
            {
                throw new ArgumentOutOfRangeException(nameof(seat), seat, "A seat is 0 or 1.");
            }

            return this.BuildView(seat, record: null);
        }

        /// <summary>Gets the final view: no seat, and the full <see cref="BackgammonSeatView.Record"/> of the match.</summary>
        /// <returns>The view with the record.</returns>
        public BackgammonSeatView GetFinalView() => this.BuildView(-1, this.BuildRecord());

        /// <summary>Gets the full record of the match: every game's openings, rolls and steps.</summary>
        /// <returns>The record.</returns>
        public BackgammonMatchRecord GetRecord()
        {
            if (!this.history)
            {
                throw new InvalidOperationException("The match does not record its history (RecordHistory is off).");
            }

            return this.BuildRecord();
        }

        /// <summary>Gets the legal moves of the stage <see cref="ToMove"/> has to play.</summary>
        /// <returns>The stage's moves.</returns>
        public BackgammonStageMoves GetStageMoves()
        {
            var state = this.game;
            if (this.ToMove < 0 || state == null)
            {
                throw new InvalidOperationException("Nobody is to move.");
            }

            return new BackgammonStageMoves(state.Position, state.Mover, state.Stage, state.PlayableDice, new List<StageEnd>(this.ends), this.StageDiceList(state));
        }

        /// <summary>
        /// Test seam: starts the match with game 1 at <paramref name="position"/>, where <paramref name="seat"/> has just
        /// rolled <paramref name="first"/>-<paramref name="second"/> as its roll number <paramref name="rollsMade"/>[seat],
        /// with the match score at <paramref name="scores"/>.
        /// </summary>
        internal void StartAt(in Position position, int seat, int first, int second, int[] rollsMade, int[]? scores = null)
        {
            if (this.IsStarted)
            {
                throw new InvalidOperationException("The match has already started.");
            }

            this.IsStarted = true;
            this.scores[0] = scores?[0] ?? 0;
            this.scores[1] = scores?[1] ?? 0;
            var state = new GameState(1, position.Version) { Position = position, Starter = seat };
            state.RollsMade[0] = rollsMade[0];
            state.RollsMade[1] = rollsMade[1];
            this.game = state;
            this.BeginRoll(seat, first, second, opening: false);
            this.Advance();
        }

        private BackgammonActResult Check(int seat, BackgammonAction action, out Position end, out GameEnd gameEnd)
        {
            end = default;
            gameEnd = GameEnd.None;
            if (!this.IsStarted)
            {
                throw new InvalidOperationException("The match has not started.");
            }

            if (this.IsFinished)
            {
                return BackgammonActResult.MatchFinished;
            }

            if (seat != this.ToMove)
            {
                return BackgammonActResult.NotYourTurn;
            }

            var state = this.game!;
            if (action?.Steps == null || action.Steps.Count > state.Stage.Count)
            {
                return BackgammonActResult.Illegal;
            }

            return StageChecker.IsLegal(state.Position, seat, state.Stage, state.PlayableDice, action.Steps, out end, out gameEnd)
                ? BackgammonActResult.Ok
                : BackgammonActResult.Illegal;
        }

        // Runs the automatic part of the match: works out the stage in play, and passes it or plays it itself when there
        // is nothing to decide, until a seat has a real decision or the match is over.
        private void Advance()
        {
            while (!this.IsFinished)
            {
                var state = this.game!;
                state.PlayableDice = this.generator.Generate(state.Position, state.Mover, state.Stage, this.ends);
                if (state.PlayableDice == 0)
                {
                    this.PlayStage(StepSequence.Empty, state.Position, GameEnd.None, auto: true);
                }
                else if (this.autoPlay && this.ends.Count == 1)
                {
                    var only = this.ends[0];
                    this.PlayStage(only.Steps, only.Position, only.End, auto: true);
                }
                else
                {
                    this.ToMove = state.Mover;
                    return;
                }
            }

            this.ToMove = -1;
        }

        private void PlayStage(StepSequence steps, in Position end, GameEnd gameEnd, bool auto)
        {
            var state = this.game!;
            var stage = state.Stage;
            this.ply++;
            if (this.history)
            {
                state.Plays.Add(new BackgammonPlay
                {
                    Ply = this.ply,
                    Seat = state.Mover,
                    Roll = new[] { state.FirstDie, state.SecondDie },
                    RollNumber = state.RollNumber,
                    Stage = state.StageIndex,
                    Dice = ViewConverter.ToList(stage, state.FirstDie),
                    Steps = steps.ToSteps(),
                    Auto = auto,
                    IsRemainder = state.IsRemainder,
                    IsOpeningRoll = state.IsOpeningRoll,
                });
            }

            state.Position = end;
            state.RollPlayed = true;
            if (steps.Count > 0)
            {
                state.StuckRolls = 0;
                if (!state.IsRemainder)
                {
                    state.MovedThisRoll = true;
                }
            }

            if (gameEnd != GameEnd.None)
            {
                this.EndGame(gameEnd, state.Mover);
                return;
            }

            if (state.IsRemainder)
            {
                // A remainder is played stage by stage; dice that cannot be played are lost. After it the player rolls.
                state.StageIndex++;
                if (state.StageIndex == state.Stages.Count)
                {
                    this.Roll(state.Mover);
                }

                return;
            }

            if (state.IsEscalating)
            {
                state.ChainPlayed += steps.Count;
                if (steps.Count == stage.Count)
                {
                    state.StageIndex++;
                    if (state.StageIndex < state.Stages.Count)
                    {
                        return;
                    }
                }
                else if (state.ChainPlayed > 0)
                {
                    // The roller could not play the whole chain: the rest goes to the opponent, who plays it before
                    // rolling. A chain the roller could not even start is lost instead.
                    var remainder = new List<StageDice> { StageDice.Same(stage.High, stage.Count - steps.Count) };
                    for (var index = state.StageIndex + 1; index < state.Stages.Count; index++)
                    {
                        remainder.Add(state.Stages[index]);
                    }

                    state.Stages.Clear();
                    state.Stages.AddRange(remainder);
                    state.StageIndex = 0;
                    state.IsRemainder = true;
                    state.Mover = 1 - state.Mover;
                    return;
                }
            }

            this.EndRoll();
        }

        // The roller's turn is over: count a roll that moved nothing, then the opponent rolls.
        private void EndRoll()
        {
            var state = this.game!;
            if (!state.MovedThisRoll)
            {
                state.StuckRolls++;
                if (state.StuckRolls >= StuckRollLimit)
                {
                    this.EndStuckGame();
                    return;
                }
            }

            this.Roll(1 - state.RollSeat);
        }

        private void Roll(int seat)
        {
            var first = this.Draw(BackgammonMatchOptions.DicePurpose);
            var second = this.Draw(BackgammonMatchOptions.DicePurpose);
            this.game!.RollsMade[seat]++;
            this.BeginRoll(seat, first, second, opening: false);
        }

        private void BeginRoll(int seat, int first, int second, bool opening)
        {
            var state = this.game!;
            state.RollSeat = seat;
            state.Mover = seat;
            state.FirstDie = first;
            state.SecondDie = second;
            state.RollNumber = state.RollsMade[seat];
            state.IsOpeningRoll = opening;
            state.IsRemainder = false;
            state.ChainPlayed = 0;
            state.MovedThisRoll = false;
            state.RollPlayed = false;
            state.StageIndex = 0;
            state.Stages.Clear();
            state.IsEscalating = false;
            if (first != second)
            {
                state.Stages.Add(StageDice.Distinct(first, second));
            }
            else if (Geometry.HasEscalation(state.Version) && state.RollNumber >= 4)
            {
                // Escalating doubles: n-n is four n's, then four (n+1)'s, … up to four 6's.
                state.IsEscalating = true;
                for (var die = first; die <= 6; die++)
                {
                    state.Stages.Add(StageDice.Same(die, 4));
                }
            }
            else
            {
                state.Stages.Add(StageDice.Same(first, 4));
            }
        }

        private void StartGame(int number)
        {
            var state = new GameState(number, VersionOf(this.Variant, number));
            this.game = state;
            int first, second;
            do
            {
                first = this.Draw(BackgammonMatchOptions.OpeningPurpose);
                second = this.Draw(BackgammonMatchOptions.OpeningPurpose);
                state.Openings.Add(new[] { first, second });
            }
            while (first == second);

            // The higher die starts and plays both dice; that opening roll is the starter's 1st roll.
            state.Starter = first > second ? 0 : 1;
            state.RollsMade[state.Starter] = 1;
            this.BeginRoll(state.Starter, first, second, opening: true);
        }

        private void EndGame(GameEnd end, int mover)
        {
            var state = this.game!;
            var points = new int[2];
            int winner = -1;
            BackgammonResultKind kind;
            switch (end)
            {
                case GameEnd.BorneOff:
                    winner = mover;
                    kind = state.Position.Count(1 - mover, Geometry.Off) == 0 ? BackgammonResultKind.Mars : BackgammonResultKind.Normal;
                    points[mover] = kind == BackgammonResultKind.Mars ? 2 : 1;
                    break;

                case GameEnd.Mother:
                    winner = mover;
                    kind = BackgammonResultKind.Mother;
                    points[mover] = 2;
                    break;

                default:
                    kind = BackgammonResultKind.Draw;
                    points[0] = 1;
                    points[1] = 1;
                    break;
            }

            this.FinishGame(new BackgammonGameResult { GameNumber = state.Number, Version = state.Version, Winner = winner, Points = points, Kind = kind });
        }

        private void EndStuckGame()
        {
            var state = this.game!;
            this.FinishGame(new BackgammonGameResult
            {
                GameNumber = state.Number,
                Version = state.Version,
                Winner = -1,
                Points = new[] { 0, 0 },
                Kind = BackgammonResultKind.Stuck,
            });
        }

        private void FinishGame(BackgammonGameResult result)
        {
            var state = this.game!;
            state.Result = result;
            this.results.Add(result);
            this.scores[0] += result.Points[0];
            this.scores[1] += result.Points[1];
            if (this.history)
            {
                this.finishedGames.Add(state.ToRecord());
            }

            if (Math.Max(this.scores[0], this.scores[1]) >= this.TargetPoints && this.scores[0] != this.scores[1])
            {
                this.Winner = this.scores[0] > this.scores[1] ? 0 : 1;
                this.IsFinished = true;
                this.ToMove = -1;
                return;
            }

            this.StartGame(state.Number + 1);
        }

        private int Draw(string purpose)
        {
            var value = this.dice(6, purpose);
            if (value < 0 || value > 5)
            {
                throw new InvalidOperationException($"The dice source returned {value} for n = 6; it must return 0..5.");
            }

            return value + 1;
        }

        private IReadOnlyList<int> StageDiceList(GameState state) => ViewConverter.ToList(state.Stage, state.FirstDie);

        // The finished game before the current one. Once the match is over, the current game is the last one and is
        // itself finished, so the previous game is the one before it.
        private BackgammonGameRecord? PreviousGame()
        {
            var finished = this.finishedGames.Count;
            if (this.game?.Result != null)
            {
                return finished >= 2 ? this.finishedGames[finished - 2] : null;
            }

            return finished >= 1 ? this.finishedGames[finished - 1] : null;
        }

        private BackgammonMatchRecord BuildRecord()
        {
            var games = new List<BackgammonGameRecord>(this.finishedGames);
            if (this.history && this.game is { Result: null })
            {
                games.Add(this.game.ToRecord());
            }

            return new BackgammonMatchRecord
            {
                Variant = this.Variant,
                TargetPoints = this.TargetPoints,
                Games = games,
                Scores = this.Scores,
                Winner = this.Winner,
                IsStopped = this.IsStopped,
            };
        }

        private BackgammonSeatView BuildView(int seat, BackgammonMatchRecord? record)
        {
            var state = this.game;
            var inPlay = state != null && this.ToMove >= 0;
            var position = state?.Position ?? Position.Start(this.Version);
            var chainRest = new List<BackgammonStage>();
            if (inPlay)
            {
                for (var index = state!.StageIndex + 1; index < state.Stages.Count; index++)
                {
                    chainRest.Add(new BackgammonStage { Die = state.Stages[index].High, Count = state.Stages[index].Count });
                }
            }

            var rollsMade = state?.RollsMade ?? new int[2];
            var escalates = Geometry.HasEscalation(this.Version);
            return new BackgammonSeatView
            {
                Seat = seat,
                Variant = this.Variant,
                Version = this.Version,
                GameNumber = this.GameNumber,
                TargetPoints = this.TargetPoints,
                Scores = this.Scores,
                ToMove = this.ToMove,
                IsMatchFinished = this.IsFinished,
                IsStopped = this.IsStopped,
                MatchWinner = this.Winner,
                Ply = this.ply,
                Board = ViewConverter.ToBoard(position),
                Pips = new[] { position.Pips(0), position.Pips(1) },
                Roll = inPlay ? new[] { state!.FirstDie, state.SecondDie } : Array.Empty<int>(),
                RollSeat = inPlay ? state!.RollSeat : -1,
                RollNumber = inPlay ? state!.RollNumber : 0,
                StageDice = inPlay ? this.StageDiceList(state!) : Array.Empty<int>(),
                StageNumber = inPlay ? state!.StageIndex : 0,
                ChainRest = chainRest,
                IsEscalating = inPlay && state!.IsEscalating,
                IsPlayingRemainder = inPlay && state!.IsRemainder,
                RollsMade = new[] { rollsMade[0], rollsMade[1] },
                NextRollEscalates = new[] { escalates && rollsMade[0] + 1 >= 4, escalates && rollsMade[1] + 1 >= 4 },
                StuckRolls = state?.StuckRolls ?? 0,
                Plays = this.history && state != null ? state.Plays.ToArray() : Array.Empty<BackgammonPlay>(),
                LastGame = this.PreviousGame(),
                Results = this.results.ToArray(),
                Record = record,
            };
        }
    }
}
