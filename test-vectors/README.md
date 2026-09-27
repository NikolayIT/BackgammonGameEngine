# Test vectors

These JSON files pin the rules down, so that a port of the rules (for example ednaigra.com's TypeScript board and
fairness checker) can check itself against the engine:

- `src/Tests/Backgammon.Logic.Tests/Vectors/TestVectorTests.cs` checks that the engine reproduces every file exactly.
- `tools/Backgammon.Arena vectors` writes them from fixed seeds. Running it again must not change a byte.

The rules they follow are in [RULES.md](../RULES.md). `rulesVersion` names that version of the rules and draw order.
Every file is UTF-8 JSON with one entry per line.

## Positions

A position is written as a text code: `V:p1,…,p24|bar0,bar1|off0,off1`.

- `V` is the version: `O` обикновена, `G` гюлбара, `T` тапа, `C` челеби.
- The 24 points are in **seat 0's numbering**. An empty token means nobody is on the point, a positive number is seat
  0's checkers, and a negative number is seat 1's.
- A тапа point with a pinned checker shows the pinner's count followed by `*`. `2*` means two of seat 0's checkers on
  a pinned seat-1 checker. `-1*` means one seat-1 checker on a pinned seat-0 checker. The pinned side always has
  exactly one checker there.
- Seat 1's own point p is seat 0's 25 − p in обикновена, тапа and челеби, and seat 0's ((p + 11) mod 24) + 1 in
  гюлбара.

Steps are `[from, die]` in the **mover's own numbering**. `from` is 1..24, or 25 for the bar, and the checker goes to
`from − die`, where 0 or below means borne off.

## `moves-{version}.json` and `tricky.json`

```json
{"position":"O:…","seat":0,"dice":[6,1],"playableDice":2,"ends":[{"position":"O:…","steps":[[13,6],[7,1]],"end":"none"}, …]}
```

- `dice` is the stage: two different dice, or one to four equal ones. Four equal dice are a double; fewer are the
  rest of an escalating chain.
- `playableDice` is how many dice every legal play uses; a play that ends the game counts as all of them.
- `ends` lists every distinct position the stage can end in, in canonical order. Each has its canonical steps (the
  first legal play reaching it, taking the higher point first and then the larger die) and how the last step ended the
  game: `none`, `borneOff`, `mother` or `bothMothers`.
- With no legal move there is one end, the unchanged position, with no steps.

`moves-*` samples positions from random games. Each position is given with all 21 rolls plus one partial stage.
`tricky.json` adds a `name` and a `description` to hand-picked positions, one rule at an edge each.

## `matches-{variant}.json`

Each entry is a whole match played with random legal plays:

- `options`: the variant, the target, and whether forced stages were auto-played.
- `draws`: every call to the dice source, in order, as `{purpose, n, value}`. `value` is 0..n−1 and the die is
  value + 1.
- `actions`: every action, as `{seat, steps}`. Stages the engine played itself are not actions; they are in the
  record with `auto: true`.
- `checkpoints` (first match of each file only): a summary of seat 0's view after the start and after each action.
- `final`: the final view, with the full record. It is serialized with camelCase names and enums as camelCase
  strings.

To check the dice, walk the final record game by game:

1. Each pair in `openings` is two `opening` draws, seat 0's die first.
2. Each play with `stage == 0` that is neither a remainder (`isRemainder`) nor the opening roll (`isOpeningRoll`) is
   two `dice` draws: its `roll`.
3. The game's `pendingRoll`, if any, is two more `dice` draws.

Those draws must be exactly `draws`.
