# The neural networks

Level 6 of every version plays with a TD-Gammon-style value network trained by self-play, if that network clearly
beats the hand-written evaluator. This file covers the networks, how they were trained, how to reproduce them, and
what they achieved.

## The network

- **Inputs:** sparse, from the point of view of the player about to roll ("me"). The layout is the same for every
  version, and inputs a version does not use stay 0. Each weights file records its input layout.
  - **Layout 1** (256 inputs), the гюлбара network:
    - 0..95: my checkers on each of my points 1..24, in TD-Gammon's truncated unary form: at least 1, at least 2, at
      least 3, and (n − 3)/2 beyond.
    - 96..191: the opponent's checkers on each of *my* points, the same way. The same physical point sits at the
      same input for both sides, which in гюлбара keeps the blocking visible.
    - 192..239: тапа pins, mine and theirs, on each of my points.
    - 240..243: the bar (n/2) and borne-off checkers (n/15), mine and theirs.
    - 244..246: pip counts (/200), mine and theirs, and their difference (/100).
    - 247..248: whether my next roll escalates, and whether theirs does (гюлбара, челеби).
    - 249: contact, meaning the checkers can still meet. It is always on in гюлбара.
    - 250..255: тапа mothers: mine alone on my start, theirs alone, my start empty, theirs empty, and whether a pinned
      checker stops me or them from bearing off.
  - **Layout 2** (260 inputs), the обикновена, тапа and челеби networks: layout 1 and four more.
    - 256..257: the share of my 36 rolls that land a checker on a lone checker of theirs (a hit, in тапа a pin), and
      the share of theirs that land on one of mine. Each checker is taken alone: one die, both dice through an open
      point, or up to four steps of a double; checkers on the bar enter first.
    - 258..259: the longest run of points the other side cannot land on, mine and theirs (/6).
- **Hidden layer:** 128 sigmoid units.
- **Outputs:** 3 sigmoids: the chance to win, to win by 2 points (марс or майка), and to lose by 2 points. A тапа
  draw is trained as half a win.
- **Arithmetic:**
  - The first layer adds the weight rows of the inputs that are set.
  - Everything is explicit 8-wide float vectors in a fixed order, with a portable exp; the layout-2 inputs are
    integer bit masks. The same position gives the same bits on every machine, so a bot chooses the same play
    everywhere.
  - An evaluation takes about 2 µs.
- **Weights:** `src/Backgammon.AI/Neural/Weights/{version}.bin`, embedded in the package and loaded once. The file
  starts with a header (magic, format and input layout, sizes, and a description of how it was trained), followed by
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

`tools/Backgammon.Trainer` trains in two steps: TD(λ) self-play from scratch, then refinement.

### TD(λ) self-play (`train`)

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
7. **Checkpoints.** At regular intervals the network plays matches against the hand-written evaluator, both with
   level 6's search and in duplicate pairs (the same dice, seats swapped). They are only measured; the output is the
   final network.

### Refinement (`refine`)

Looking one roll deeper is worth about 100 Elo at level 6, so the network's value one roll deeper is better than its
own. Refinement trains the network towards it, round by round:

1. **Positions** come from the network's own self-play, at the start of each roll: 1M a round.
2. **Targets:** for each of the 21 rolls, the roller's best play by expected points, valued by the network with the
   opponent to roll, averaged by the rolls' chances. An escalating double is played stage by stage; when the roller
   breaks it after playing some of it, the opponent plays the rest greedily, and a chain it could not start is lost.
   A play that ends the game counts exactly.
3. **Training:** plain SGD on the cross-entropy, one position at a time in a shuffled order, α falling from 0.0003
   to 0.00003.

Positions come from fixed slots with fixed seeds, each target is computed on its own, and the training runs on one
thread, so a refined network depends only on the command line and its starting file.

### Reproducing the weights

Run from a copy of the trainer's `bin` (a running trainer locks it), on x64. The starting files' names matter: each
refined network records its starting file's name in its description.

```
set DOTNET_EnableAVX512F=0
Backgammon.Trainer train  --version obiknovena --seed 1 --games 2000000 --layout 2 --eval-every 500000 --eval-pairs 200 --out o-l2.bin
Backgammon.Trainer refine --version obiknovena --init o-l2.bin --positions 1000000 --rounds 8 --alpha 0.0003 --alpha-end 0.00003 --eval-pairs 100 --out obiknovena.bin
Backgammon.Trainer train  --version tapa --seed 1 --games 2000000 --layout 2 --eval-every 500000 --eval-pairs 200 --out tapa-l2.bin
Backgammon.Trainer refine --version tapa --init tapa-l2.bin --positions 1000000 --rounds 10 --alpha 0.0003 --alpha-end 0.00003 --eval-pairs 100 --out tapa.bin
Backgammon.Trainer train  --version chelebi --seed 1 --games 2000000 --layout 2 --eval-every 500000 --eval-pairs 200 --out chelebi-l2.bin
Backgammon.Trainer refine --version chelebi --init chelebi-l2.bin --positions 1000000 --rounds 10 --alpha 0.0003 --alpha-end 0.00003 --eval-pairs 100 --out chelebi.bin
```

The гюлбара network is 1.0's. It was trained before a fix to self-play (2026-09-27), which used to value a play in the
middle of a remainder as if the opponent rolled next; to reproduce it byte for byte, build the trainer at commit
`6d23ae9` and run:

```
Backgammon.Trainer train --version gyulbara --seed 1 --games 2000000 --eval-every 200000 --eval-pairs 400 --out gyulbara.bin
```

The other settings are the defaults (TrainingSettings.cs), and each file records its settings in its description.
`NeuralTests` pins the shipped files' SHA-256. To ship a network, copy it to
`src/Backgammon.AI/Neural/Weights/{version}.bin`, list the version in `Networks.Shipped`, and update the hash in the
test.

## Results

The ship gate is 2,000 matches to 3 (1,000 duplicate pairs) against the hand-written evaluator. Both sides use level
6's search, and the gate requires the lower bound of the 95% interval to be above 55%. Every network passed by a wide
margin. Each new network also played the network it replaced, both with level 6's search, in duplicate pairs.

| Version | Network | Against the baseline | Against 1.0's network | 2-point share in self-play | SHA-256 |
| --- | --- | --- | --- | --- | --- |
| Обикновена | layout 2, refined 8 rounds | **95.15% ± 0.94%** | **58.0% ± 1.0%** (10,000), +56 Elo | 15.4% (марс) | `ae03ac24…a3395f` |
| Гюлбара | 1.0's, layout 1 | **81.15% ± 1.71%** | | 32.0% (марс) | `93f7bb52…033097` |
| Тапа | layout 2, refined 10 rounds | **92.70% ± 1.14%** | **62.4% ± 0.9%** (10,000), +88 Elo | 40.4% (марс 38.4%, майка 1.9%) | `99febb20…8474d6` |
| Челеби | layout 2, refined 10 rounds | **71.90% ± 1.97%** | **53.1% ± 1.0%** (10,000), +22 Elo | 49.7% (марс) | `8e673779…b26619` |

The 2-point shares feed the match equity table (`MatchEquity.Rates`). A 2M-game run takes 15 to 35 minutes on 20
threads, and refinement about 3 minutes a round (7 in гюлбара).

### What helped, and what did not

Each candidate against 1.0's network of its version, both with level 6's search:

| Candidate | Обикновена | Тапа | Челеби | Гюлбара |
| --- | --- | --- | --- | --- |
| 256 hidden units instead of 128 | 51.5% ± 1.4% | | | |
| Refined only | 53.6% ± 1.0% (11 rounds) | | | |
| Layout 2 | 55.6% ± 1.0% | 61.1% ± 1.4% | 51.6% ± 1.4% | 50.1% ± 1.4% |
| Layout 2, refined | **58.0% ± 1.0%** | **62.4% ± 0.9%** | **53.1% ± 1.0%** | (no progress) |
| Layout 2 and which dice each side can move by | | | | 49.4% ± 1.4% |

- **Better inputs helped most,** above all in тапа, where pinning a lone checker is the game. Hitting and blocking are
  what the raw board makes a network work hardest to see.
- **Refinement added 10–20 Elo** on top, and rounds beyond about 10 add little. In гюлбара the network was already
  within 0.0005 of its own one-roll value, and refinement did not move it.
- **Capacity is not the limit.** Doubling the hidden layer gave the least, at twice the evaluation cost.
- **Гюлбара did not improve.** It has no hitting, so of layout 2 only the blocking runs apply, and neither they nor
  inputs for the dice each side can still move by beat 1.0's network. It keeps that network.
- **Челеби is dominated by luck.** Escalating doubles move up to 84 pips in one roll, which caps how far any evaluator
  can pull ahead.
- **The search was left as it is.** Having the mover play the rest of an opponent's broken chain in the one-roll
  look-ahead scored 50.0% ± 1.0% (челеби) and 49.7% ± 1.0% (гюлбара) against the search without it.

### 1.0's learning curves

The share of 800 matches won against the baseline, every 200k games (400k for челеби), for the layout-1 networks of
1.0. Гюлбара still plays with its network.

| Games | Обикновена | Гюлбара | Тапа | Челеби |
| --- | --- | --- | --- | --- |
| 200k | 87.5% | 30.1% | 77.5% | |
| 400k | 90.2% | 43.2% | 84.1% | 67.4% |
| 600k | 89.7% | 66.9% | 88.6% | |
| 800k | 91.5% | 76.9% | 88.5% | 71.1% |
| 1.0M | 94.1% | 77.9% | 87.7% | |
| 1.2M | 93.9% | 78.9% | 90.5% | 68.6% |
| 1.4M | 91.9% | 82.3% | 90.2% | |
| 1.6M | 93.0% | 81.0% | 91.4% | 69.4% |
| 1.8M | 92.4% | 80.0% | 90.5% | |
| 2.0M | 93.0% | 79.2% | 92.9% | 69.3% |

## Lessons

- **α = 0.02 per state saturated the sigmoids.** With about 2,000 states summed per update, the network soon played
  one fixed policy and lost 97% to the baseline. α = 0.001 learned quickly: 80% against the baseline after 100k
  games.
- **`HashCode.Combine` must not seed anything.** .NET seeds it randomly per process, so the first runs could not be
  reproduced.
- **Measure a new network against the one it would replace,** not only against the baseline: the gate against the
  baseline saturates above 90% and hides gains of 50 Elo.
- **Keep Windows from throttling long runs.** Both tools ask for it (`NativeMethods`). Without it a background run
  can land on the efficiency cores: an arena run once had 3 cores busy instead of 16, and decision times measured
  that way were twice the real ones.
