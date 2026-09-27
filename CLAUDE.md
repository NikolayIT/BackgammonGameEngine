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
- **Release:** the owner releases to NuGet. Never create a GitHub release.

## Conventions

- `src/Directory.Build.props` sets net10.0 and Nullable; with `CI=true`, warnings are errors. StyleCop comes from
  `src/stylecop.json` and `src/.globalconfig`, and versions are managed centrally in `src/Directory.Packages.props`.
- Code style:
  - block-scoped namespaces with the usings inside;
  - `this.` on members;
  - XML docs on every public member of the packages (CS1591 is an error in CI).
- The engine has no dependencies. Views and records are plain sealed classes with `init` properties and no
  serializer attributes.
- Internals are shared with the bots, the tests and the tools through `InternalsVisibleTo` items in the csproj.
- Hot paths (move generation, evaluation) stay allocation-free and use loops, not LINQ.
- Tests are xUnit v3 on Microsoft Testing Platform. Run them from `src`: `dotnet test --solution Backgammon.slnx`.
  `BACKGAMMON_LONG=1` switches on the full differential and property runs.
- Commit and push after each tested chunk of work. Never add Co-Authored-By or other attribution lines.
