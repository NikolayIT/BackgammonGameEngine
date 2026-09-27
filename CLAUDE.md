# CLAUDE.md

Notes for working on this repository.

## What it is

A .NET 10 engine for табла (Bulgarian backgammon) with bots, published to NuGet as `BackgammonGameEngine` and
`BackgammonGameEngine.Bots`. ednaigra.com (`C:\Dev\ednaigra.com`) plugs it in as the game `tabla`, following the
pattern of SantaseGameEngine (`C:\Dev\Games\SantaseGameEngine`). The rules and every decision about them are in
RULES.md.

## Rule decisions the owner made (do not re-ask)

- **Майка** is checked after every step. The mover wins марс (2 points) once the opponent's last checker on its
  start is pinned by the mover and the mover has no checkers on its own start. If both mothers are pinned, the game
  is a draw at 1:1.
- **Тапа:** while one of your checkers is pinned in your home board, you bear nothing off.
- **Escalating doubles** (гюлбара, челеби) start from each player's 4th roll, and the starter's opening roll counts
  as roll 1.
  - If the roller cannot play even one die of the rolled double, the chain is lost.
  - Otherwise the unplayed rest passes to the opponent, who plays it stage by stage before rolling. Dice the
    opponent cannot play are lost, and nothing passes back.
- **Release:** the owner releases to NuGet. Never create a GitHub release. The one-time setup is written in
  `.github/workflows/publish.yml`.

## Layout

- **`src/Backgammon.Logic`** (the engine package):
  - `BackgammonMatch` drives the flow: openings, rolls, escalation, remainders, passes, scoring, the match.
  - `Rules/` is internal: `Position` (a value type with both seats' counts in their own numbering and тапа pin
    masks), `StepRules` (single steps), `StageGenerator` (every distinct end of a stage, with canonical steps),
    `StageChecker` (validates steps without listing them) and `ViewConverter`.
  - `Views/` holds the plain models; `Moves/` holds `BackgammonStageMoves`.
- **`src/Backgammon.AI`** (the bots package):
  - `BackgammonBot` is the public API.
  - `BotLevels` holds the noise table and the search per level.
  - `Search/Chooser` picks a play: 0-ply, then a look-ahead through the known stages of a chain or remainder, then
    one roll deeper at the best plays.
  - `Evaluation/` holds `BaselineEvaluator`, `MatchEquity` and `Outcome`.
  - `Neural/` holds the network, the encoder, the portable sigmoid, and the embedded weights (`Neural/Weights/*.bin`,
    listed in `Networks.Shipped`).
- **`tools/Backgammon.Arena`**: `vectors`, `arena`, `ladder`, `calibrate`, `timing`.
- **`tools/Backgammon.Trainer`**: `train`, `validate`, `stats`, `bench` (see NEURAL_NETWORK.md).
- Neither tool is in the solution. Run them from a copy of `bin`, because a running tool locks it.

## Conventions

- `src/Directory.Build.props` sets net10.0 and Nullable; with `CI=true`, warnings are errors. StyleCop comes from
  `src/stylecop.json` and `src/.globalconfig`, and versions are managed centrally in `src/Directory.Packages.props`.
  `tools/` imports the same settings.
- Code style:
  - block-scoped namespaces with the usings inside;
  - `this.` on members;
  - XML docs on every public member of the packages (CS1591 is an error in CI).
- The engine has no dependencies. Views and records are plain sealed classes with `init` properties and no
  serializer attributes.
- Internals are shared with the bots, the tests and the tools through `InternalsVisibleTo` items in the csproj.
  The bots refuse to run on a different engine version.
- Hot paths (generation, evaluation) avoid allocation and use loops, not LINQ. There is one `StageGenerator` or
  `Chooser` per thread.
- **Tests:** xUnit v3 on Microsoft Testing Platform. Run them from `src`: `dotnet test --solution Backgammon.slnx`.
  - `BACKGAMMON_LONG=1` switches on the full differential and property runs (100k+ per version). They take about an
    hour on 20 threads. `long-tests.yml` runs them on demand.
  - `test-vectors/` must be reproduced exactly: CI writes them again and diffs them.
- Commit and push after each tested chunk of work. Never add Co-Authored-By or other attribution lines.

## Lessons (do not repeat)

- **Seeds:** never seed with `HashCode.Combine`. .NET seeds it randomly per process, so the training runs and the
  test positions could not be reproduced. Use the fixed mixers (`Training.Seed`, `PositionSources.Seed`).
- **Step size:** TD(λ) with games batched on a frozen network needs a small step per state. With 20 games (about
  2,000 states) summed per update, α = 0.02 saturated the sigmoids: the net played one fixed policy and lost 97% to
  the baseline. α = 0.001 falling to 0.0001 learns well: 80% after 100k games, 90% after 400k.
- **One roll deeper** at level 6's two best plays is worth about +100 Elo in обикновена, for p99 of about 13 ms.
- **Determinism:** the network uses explicit Vector256 arithmetic in a fixed order and its own exp, so the bots
  choose the same play on every machine. A hardware horizontal sum could add the lanes in another order, so the
  lanes are summed one by one.
