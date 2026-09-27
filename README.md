# BackgammonGameEngine

A .NET 10 engine for **табла**, the Bulgarian family of backgammon games, with strong computer players. It is a
reusable library for NuGet, written for game servers such as [ednaigra.com](https://ednaigra.com) and for any other
host.

| Version | What it is |
| --- | --- |
| Обикновена (права) | Standard backgammon without the doubling cube: hitting, the bar, марс for 2 points. |
| Гюлбара | All 15 checkers start on the 24 point and both players move the same way round. No hitting: one checker holds a point. Doubles escalate from the 4th roll. |
| Тапа | All 15 checkers start on the 24 point and the players move in opposite directions. A lone checker is pinned, not hit, and pinning the opponent's last checker on its start (майка) wins at once. |
| Челеби | Обикновена with гюлбара's escalating doubles. |
| Среща | The traditional match, rotating обикновена → гюлбара → тапа. |

The rules, with every decision the engine makes, are in [RULES.md](RULES.md).

## Packages

- `BackgammonGameEngine`: the rules and matches driven one action at a time. It uses injected dice, per-seat views,
  replayable records and move helpers for user interfaces.
- `BackgammonGameEngine.Bots`: six computer levels per version.

## Repository layout

```
src/Backgammon.Logic/     the engine (package BackgammonGameEngine)
src/Backgammon.AI/        the bots (package BackgammonGameEngine.Bots)
src/Tests/                xUnit v3 tests
tools/                    the trainer and the arena (not in the solution)
test-vectors/             JSON test vectors for ports of the rules
```

## Build and test

```
cd src
dotnet build Backgammon.slnx -c Release
dotnet test --solution Backgammon.slnx -c Release
```

Set `BACKGAMMON_LONG=1` for the full differential and property runs (100k+ positions and games per version).
