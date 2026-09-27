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
    listed in `Networks.Shipped`). Each weights file records its input layout: обикновена, тапа and челеби use
    layout 2 (with hitting rolls and blocking runs), гюлбара layout 1.
- **`tools/Backgammon.Arena`**: `vectors`, `arena`, `ladder`, `calibrate`, `timing`, `bench`. Players: `L1`..`L6`,
  `random`, `baseline`, `baseline0`, `net:<file>`, `net1:<file>`, `noise:<σ>`, `search:<look-ahead>:<one roll>:<budget>`.
- **`tools/Backgammon.Trainer`**: `train`, `refine`, `validate`, `stats`, `bench` (see NEURAL_NETWORK.md).
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
  - `test-vectors/` must be reproduced exactly: CI writes them again and diffs them. The vector tests look for the
    `test-vectors` folder above the test binaries, so a copy of `bin` run outside the repo fails only those 10 tests.
- Commit and push after each tested chunk of work. Never add Co-Authored-By or other attribution lines.

## Lessons (do not repeat)

- **Seeds:** never seed with `HashCode.Combine`. .NET seeds it randomly per process, so the training runs and the
  test positions could not be reproduced. Use the fixed mixers (`Training.Seed`, `PositionSources.Seed`).
- **Step size:** TD(λ) with games batched on a frozen network needs a small step per state. With 20 games (about
  2,000 states) summed per update, α = 0.02 saturated the sigmoids: the net played one fixed policy and lost 97% to
  the baseline. α = 0.001 falling to 0.0001 learns well: 80% after 100k games, 90% after 400k.
- **One roll deeper** at level 6's two best plays is worth about +100 Elo in обикновена. With the network it is
  worth little in гюлбара (+15, not significant).
- **The evaluation budget must be a hard cap.** When it was only checked between candidates, one candidate of the
  one-roll look-ahead through escalating doubles took up to 37 ms in гюлбара. Now an over-budget candidate keeps its
  0-ply value and the search stops. Level 6 uses 3,000 evaluations: p99 at most 5.4 ms, max 7.5 ms (ARENA.md).
- **Calibrating levels.** Noisy players measured against level 6 directly all lose every match, which gives clamped,
  meaningless Elo. Rate a chain of neighbours instead (arena `calibrate`). A full round robin (arena `ladder`) then
  stretches the top compared with the chain, so re-interpolate the noises from the ladder's own ratings and check
  again.
- **Training.** Networks plateau after about 1M games at 128 hidden units, and 256 units add little. Челеби
  warm-started from обикновена ended where челеби from scratch did. What helped: better inputs (layout 2, +39 to +79
  Elo where hitting or pinning matters) and refinement towards the network's own one-roll look-ahead (+10 to +20
  more). Nothing tried improved гюлбара (NEURAL_NETWORK.md).
- **Judge a new network against the one it replaces,** head to head over 10,000 matches. The gate against the
  baseline saturates above 90% and hides gains of 50 Elo.
- **Measurements.** The arena and trainer measure under contention when other jobs run; time decisions on an idle
  machine. Both tools ask Windows not to throttle them: without it a background run landed on the efficiency cores
  (3 cores busy of 20), and 1.0's decision times were about twice the real ones.
- **Searching the remainder** of an opponent's broken chain in the one-roll look-ahead changed nothing (50.0% and
  49.7% head to head), so the search leaves it out.
- **Determinism:** the network uses explicit Vector256 arithmetic in a fixed order and its own exp, so the bots
  choose the same play on every machine. A hardware horizontal sum could add the lanes in another order, so the
  lanes are summed one by one.
- **Counts are bytes.** `Position` stores counts in bytes, so any reader of outside input (views, position codes,
  network files) must refuse out-of-range values *before* storing them: 256 checkers on the bar used to read as 0
  and pass the 15-checker check.
- **Who rolls after a stage** is `Chooser.OnRollAfter`, shared by the bots and the trainer: the mover after a
  remainder stage (it rolls once the remainder is played), the opponent otherwise. Both used to put the opponent on
  roll in the middle of a remainder.
- **Compare every checker with the naive generator,** not only the generator: `StageChecker` (Validate, Act, the
  helpers) was long checked only against itself. `StageCheckerDifferentialTests` now covers it.
