# BackgammonGameEngine

A game engine for **табла**, the Bulgarian family of backgammon games: обикновена (права), гюлбара, тапа, челеби
and the traditional среща match.

- Matches are driven one action at a time, and the host supplies every die, so a provably fair host can verify each
  roll.
- Per-seat views and match records are plain JSON-serializable models, and a record replays its match exactly.
- Move helpers support tap-to-move boards.

The bots are in the companion package `BackgammonGameEngine.Bots`. The rules the engine plays by, with every
decision, are in [RULES.md](https://github.com/NikolayIT/BackgammonGameEngine/blob/master/RULES.md).

```
dotnet add package BackgammonGameEngine
```

## Play a match

```csharp
using Backgammon.Logic;

var match = new BackgammonMatch(new BackgammonMatchOptions
{
    Variant = BackgammonVariant.Sreshta,   // or Obiknovena, Gyulbara, Tapa, Chelebi
    Dice = (n, purpose) => Random.Shared.Next(n),
});
match.Start();
while (!match.IsFinished)
{
    var seat = match.ToMove;
    var moves = match.GetStageMoves();
    var steps = moves.Outcomes[0].Steps;   // pick one of the legal plays
    match.Act(seat, new BackgammonAction { Steps = steps });
}

Console.WriteLine($"Seat {match.Winner} wins {match.Scores[0]}:{match.Scores[1]}");
```

## Driving a match

- **Starting.** `Start()` draws the first opening roll and plays up to the first decision.
- **Stages.** After that, the seat in `ToMove` always has a real decision: a *stage* to play.
  - A stage is the two dice of a roll, the four dice of a double, one group of four of an escalating chain
    (гюлбара, челеби), or one stage of a remainder passed on by the opponent.
  - The engine rolls the dice itself.
  - It passes stages with no legal move.
  - It hands the rest of an escalating chain the roller cannot finish to the opponent.
  - It starts the next game when one ends.
- **Acting.** `Act(seat, action)` plays a stage, given as steps `(from, die)` in the mover's own numbering: `from` is
  1..24, or 25 for the bar, and the checker goes to `from − die`, where 0 or below is off. It returns `Ok`,
  `NotYourTurn`, `Illegal` or `MatchFinished`, and changes nothing unless it returns `Ok`.
- **Validating.** `Validate(seat, action)` returns exactly what `Act` would, without changing anything.
- **Stopping.** `Stop()` ends the match for a resignation or a timeout, with no winner; the host decides the
  outcome.
- **Forced stages.** With `AutoPlayForcedStages`, the engine also plays a stage whose legal plays all lead to the same
  position. Such plays are marked `Auto`.

Every game ends with a result:

| Result | Points |
| --- | --- |
| Normal win | 1 |
| Марс | 2 |
| Майка (тапа) | 2 |
| Both mothers pinned (тапа) | 1 : 1 |
| Stuck (100 rolls with no move) | 0 : 0 |

A match ends when a player has reached the target (3, or 5 for a среща) **and leads**.

A match is not thread-safe: drive it from one thread at a time.

## Dice and fairness

`BackgammonMatchOptions.Dice` is called as `Dice(6, purpose)`. It returns a uniform integer 0..5, and the die is that
plus one. The calls come in exactly this order, and the rules use no other randomness:

1. Each game starts with pairs of `"opening"` draws, seat 0's die first, until the two differ. The higher die starts
   and plays both dice.
2. Every later roll is two `"dice"` draws, made when that roll starts.
3. Escalating chains and remainders draw nothing, and nothing is drawn after the match ends.

A host can verify every die from the final record:

- the `Openings` of each game;
- the `Roll` of every play with `Stage == 0` that is neither `IsRemainder` nor `IsOpeningRoll`;
- the game's `PendingRoll`.

## Views and records

- `GetView(seat)` shows what a seat sees, with no future dice:
  - the board in seat 0's numbering (checkers, тапа pins, bar and borne off) and the pip counts;
  - the roll, the stage's dice, the rest of an escalating chain, and whether a remainder is being played;
  - the rolls each seat has made, the score, the game number and version;
  - this game's plays, the previous game's record (to animate how it ended), and every game's result;
  - a match-wide `Ply` counter, so a host can find the plays it has not shown yet.
- `GetFinalView()` also carries the full `Record`: every game's openings, rolls and steps. It is enough to replay the
  match and to check every die.

Views are plain sealed classes with `init` properties. They serialize with System.Text.Json as they are, including
source-generated contexts with camelCase names.

## Helpers for a board

`BackgammonStageMoves`, from `match.GetStageMoves()` or `BackgammonStageMoves.For(view)`, provides:

- `Outcomes`: every distinct position the stage can end in, with its canonical steps;
- `NextSteps(partial)` and `Destinations(partial, from)`: the legal next steps after the steps made so far, with their
  landing points in both numberings;
- `DiceLeft(partial)`, `IsLegalPrefix(partial)` and `IsLegal(steps)`.

`BackgammonGeometry` converts point numbers between the two seats' numberings.

## Test vectors

`test-vectors/` in the repository holds JSON files that pin the rules down: the legal plays of many positions and
rolls, tricky positions, and whole recorded matches with every die. A port of the rules (for example a TypeScript
board) can check itself against them.
