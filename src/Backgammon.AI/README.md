# BackgammonGameEngine.Bots

Computer players for [BackgammonGameEngine](https://www.nuget.org/packages/BackgammonGameEngine): six levels for
табла's обикновена, гюлбара, тапа, челеби and среща.

```
dotnet add package BackgammonGameEngine.Bots
```

Use the same version as BackgammonGameEngine.

```csharp
using Backgammon.AI;
using Backgammon.Logic;

var seat = match.ToMove;
var decision = BackgammonBot.Decide(match.GetView(seat), level: 6, random);
match.Act(seat, decision.Action);
```

## Levels

- **Level 6** is the strongest player of each version. It uses a TD-Gammon-style neural network trained by self-play,
  or the hand-written evaluator for a version whose network did not beat it. It values every distinct play by its
  chance to win the *match*: a марс counts only when it matters to the match score. It also looks through the known
  stages that follow, such as the rest of an escalating chain or a remainder passed to the opponent.
- **Levels 1..5** add calibrated noise to those values, as GNU Backgammon does. Each level is an even rating step
  above the one below, and level 1 is weak but not random. The measured ratings are in
  [ARENA.md](https://github.com/NikolayIT/BackgammonGameEngine/blob/master/ARENA.md).

## The contract

- **Input.** A bot decides from a seat view alone, and never sees the dice stream. The view may have come back from
  the host's own JSON.
- **Determinism.** It is deterministic for a given view, level and `Random` state. Only levels 1..5 use the `Random`.
- **Speed.** A decision takes well under 20 ms on one core.
- **Threads.** It is thread-safe: any number of decisions can run at once.
- **Think time.** `Decide` also returns a `Complexity` computed from the number of distinct plays: 0 when forced, about
  1 for a typical roll, and up to 3. A host can scale a bot's think time by it. `BackgammonBot.Complexity(view)` gives
  it without deciding.
