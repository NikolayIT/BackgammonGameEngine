# The neural networks

Level 6 of every version plays with a TD-Gammon-style value network trained by self-play, if that network clearly
beats the hand-written evaluator. This file covers the networks, how they were trained, how to reproduce them, and
what they achieved.

## The network

- **Inputs:** 256, sparse, from the point of view of the player about to roll ("me"). The layout is the same for
  every version, and inputs a version does not use stay 0. Layout version 1 is written into every weights file.
  - 0..95: my checkers on each of my points 1..24, in TD-Gammon's truncated unary form: at least 1, at least 2, at
    least 3, and (n − 3)/2 beyond.
  - 96..191: the opponent's checkers on each of *my* points, the same way. The same physical point sits at the same
    input for both sides, which in гюлбара keeps the blocking visible.
  - 192..239: тапа pins, mine and theirs, on each of my points.
  - 240..243: the bar (n/2) and borne-off checkers (n/15), mine and theirs.
  - 244..246: pip counts (/200), mine and theirs, and their difference (/100).
  - 247..248: whether my next roll escalates, and whether theirs does (гюлбара, челеби).
  - 249: contact, meaning the checkers can still meet. It is always on in гюлбара.
  - 250..255: тапа mothers: mine alone on my start, theirs alone, my start empty, theirs empty, and whether a pinned
    checker stops me or them from bearing off.
- **Hidden layer:** 128 sigmoid units.
- **Outputs:** 3 sigmoids: the chance to win, to win by 2 points (марс or майка), and to lose by 2 points. A тапа
  draw is trained as half a win.
- **Arithmetic:**
  - The first layer adds the weight rows of the inputs that are set.
  - Everything is explicit 8-wide float vectors in a fixed order, with a portable exp. The same position gives the
    same bits on every machine, so a bot chooses the same play everywhere.
  - An evaluation takes about 3 µs.
- **Weights:** `src/Backgammon.AI/Neural/Weights/{version}.bin`, embedded in the package and loaded once. The file
  starts with a header (magic, format and layout version, sizes, and a description of how it was trained), followed by
  float32 weights. It is about 130 KB.

## How a bot uses it

- The value of each distinct end of the stage is turned into a **match-winning chance** through a match equity table
  for the variant and target. The table follows the lead rule, тапа's draws and the среща rotation, with the share of
  2-point results per version measured in self-play. So a марс counts only when it matters to the match.
- When the turn goes on after the stage, the few best plays are played out through the stages that are already known:
  the rest of the bot's own escalating chain, or a remainder handed to the opponent, who answers for itself.
- When the turn ends, the two best plays are looked at one roll deeper. The value is the opponent's best reply to
  each of its 21 rolls, averaged by their chances; an escalating double is played out stage by stage.
- Levels 1..5 skip that search and add calibrated Gaussian noise to the values (see ARENA.md).

## Training

`tools/Backgammon.Trainer` trains by TD(λ) self-play:

1. **Self-play.** Every game goes through the engine's own `BackgammonMatch`, so the rules can never drift. Each play
   is the one with the best expected points for the mover, from the network's value of every distinct end. A win is
   counted exactly.
2. **Training states.** These are the positions at the start of every roll, with the roller about to roll.
3. **Targets.** After the game, the λ-returns are built backwards from the result. Each state's target mixes the next
   state's value (weight 1 − λ) with the next state's own target (weight λ), both seen from this state's roller.
4. **Update.** Each state's three outputs are pushed towards their target with the cross-entropy gradient: target
   minus output.
5. **Batches.** Games are played in batches of 20 fixed slots, all with the same frozen network. Each slot's seed comes
   from (seed, batch, slot) through a fixed mixer. The slots' changes are summed in slot order and applied. This is
   offline TD(λ), and the weights depend only on the command line: not on the thread count or timing. Two runs with 3
   and 16 threads gave byte-identical weights.
6. **Step size.** It falls geometrically from α = 0.001 to 0.0001 per state; λ = 0.7.
7. **Checkpoints.** Every 200k games the network plays 800 matches against the hand-written evaluator, both with
   level 6's search and in duplicate pairs (the same dice, seats swapped). The best checkpoint is kept.

Челеби starts from the trained обикновена network, which knows everything but the escalation.

### Reproducing the weights

Run from a copy of the trainer's `bin` (a running trainer locks it), on x64:

```
set DOTNET_EnableAVX512F=0
Backgammon.Trainer train --version obiknovena --seed 1 --games 2000000 --eval-every 200000 --eval-pairs 400 --out obiknovena.bin
Backgammon.Trainer train --version tapa       --seed 1 --games 2000000 --eval-every 200000 --eval-pairs 400 --out tapa.bin
Backgammon.Trainer train --version gyulbara   --seed 1 --games 2000000 --eval-every 200000 --eval-pairs 400 --out gyulbara.bin
Backgammon.Trainer train --version chelebi    --seed 1 --games 1000000 --eval-every 200000 --eval-pairs 400 --init obiknovena.bin --out chelebi.bin
```

The step sizes, λ, batch and hidden size are the defaults (TrainingSettings.cs). Each file records its command line
in its description. Copy the final (or best) file to `src/Backgammon.AI/Neural/Weights/{version}.bin` and list the
version in `Networks.Shipped`.

## Lessons

- **α = 0.02 per state saturated the sigmoids.** With about 2,000 states summed per update, the network soon played
  one fixed policy and lost 97% to the baseline. α = 0.001 learned quickly: 80% against the baseline after 100k
  games.
- **`HashCode.Combine` must not seed anything.** .NET seeds it randomly per process, so the first runs could not be
  reproduced.
