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

Every network is trained from scratch. A челеби network warm-started from обикновена's plateaued at the same strength
(51.0% ± 2.2% of 2,000 matches head to head), so челеби too ships its from-scratch network.

### Reproducing the weights

Run from a copy of the trainer's `bin` (a running trainer locks it), on x64:

```
set DOTNET_EnableAVX512F=0
Backgammon.Trainer train --version obiknovena --seed 1 --games 2000000 --eval-every 200000 --eval-pairs 400 --out obiknovena.bin
Backgammon.Trainer train --version tapa       --seed 1 --games 2000000 --eval-every 200000 --eval-pairs 400 --out tapa.bin
Backgammon.Trainer train --version gyulbara   --seed 1 --games 2000000 --eval-every 200000 --eval-pairs 400 --out gyulbara.bin
Backgammon.Trainer train --version chelebi    --seed 1 --games 2000000 --eval-every 400000 --eval-pairs 400 --out chelebi.bin
```

The step sizes, λ, batch and hidden size are the defaults (TrainingSettings.cs). Each file records its settings in its
description. The shipped files are the final networks of these runs. `NeuralTests` pins their SHA-256.

The shipped networks were trained before a fix to self-play (2026-09-27). Self-play used to value a play in the
middle of a remainder as if the opponent rolled next, when the player of a remainder rolls next itself. Only гюлбара
and челеби have remainders. The current trainer still reproduces обикновена and тапа exactly; to reproduce гюлбара
and челеби byte for byte, build the trainer at commit `6d23ae9`. To ship a
network, copy it to `src/Backgammon.AI/Neural/Weights/{version}.bin`, list the version in `Networks.Shipped`, and
update the hash in the test.

## Results

The ship gate is 2,000 matches to 3 (1,000 duplicate pairs) against the hand-written evaluator. Both sides use level
6's search, and the gate requires the lower bound of the 95% interval to be above 55%. Every network passed by a wide
margin.

| Version | Games | Time (20 threads) | Against the baseline | 2-point share in self-play | Rolls a game | SHA-256 |
| --- | --- | --- | --- | --- | --- | --- |
| Обикновена | 2,000,000 | 20.7 min | **94.15% ± 1.03%** | 13.3% (марс) | 47 | `bb7b6ade…4c50f5` |
| Гюлбара | 2,000,000 | 42.7 min | **81.15% ± 1.71%** | 32.0% (марс) | 49 | `93f7bb52…033097` |
| Тапа | 2,000,000 | 20.3 min | **90.20% ± 1.30%** | 38.1% (марс 35.0%, майка 3.0%), draws 0.01% | 92 | `e63b0edf…756a4a` |
| Челеби | 2,000,000 | 6.3 min | **69.50% ± 2.02%** | 49.2% (марс) | 27 | `da6a0fb9…9347f0` |

The 2-point shares feed the match equity table (`MatchEquity.Rates`).

The learning curves, as the share of 800 matches won against the baseline, checked every 200k games (400k for
челеби). This measurement used the trainer build of the time, which had no one-roll look-ahead:

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

Observations:

- **Обикновена plateaued after about 1M games.** Its final network and the 1M checkpoint are level (51.7% ± 3.1%
  head to head). A larger hidden layer would be the next step, at twice the evaluation cost.
- **Гюлбара learned slowly.** Blocking only pays off over many rolls. From 30% at 200k games it climbed to about 80%.
- **Челеби is dominated by luck.** Escalating doubles move up to 84 pips in one roll, which caps how far any evaluator
  can pull ahead. Its TD error stays three times higher than обикновена's.

## Improving the networks (experiments)

Measured on обикновена after 1.0, each candidate against the shipped network, both with level 6's search, in
duplicate pairs:

| Candidate | Against the shipped network |
| --- | --- |
| 256 hidden units instead of 128, TD(λ) from scratch, 2M games | 51.5% ± 1.4% (5,000 matches), +11 Elo, at twice the evaluation cost |
| Refined, 5 rounds of 1M positions (round 4) | 52.9% ± 1.0% (10,000), +20 Elo |
| Refined, 11 rounds | 53.6% ± 1.0% (10,000), +25 Elo |
| Input layout 2, TD(λ) from scratch, 2M games | 55.6% ± 1.0% (10,000), +39 Elo |
| Input layout 2, then refined 8 rounds of 1M positions | **58.0% ± 1.0% (10,000), +56 Elo** |

- **Refinement** (`Backgammon.Trainer refine`) trains a network towards its own value one roll deeper, on positions
  from its own self-play: for each of the 21 rolls the roller's best play, valued by the network. It helps because
  that value is better than the network's own: looking one roll deeper is worth about 100 Elo at level 6. Rounds
  beyond about 10 add little.
- **Input layout 2** adds four inputs to the 256: for each side, the share of its 36 rolls that hit (in тапа, pin) a
  lone checker of the other side, and the longest run of points the other side cannot land on. Blocking and hitting
  are what the raw board inputs make the network work hardest to see. A network records its layout, so layout-1 and
  layout-2 networks can play each other; the shipped networks are all layout 1.
- **Capacity is not the limit.** Doubling the hidden layer gave the least.
- **The cost.** Layout 2 makes a level-6 decision about 1.6 times slower, which the evaluation budget would have to
  absorb to keep every decision under 20 ms.

Reproducing the best candidate:

```
Backgammon.Trainer train  --version obiknovena --seed 1 --games 2000000 --layout 2 --eval-every 500000 --eval-pairs 200 --out o-l2.bin
Backgammon.Trainer refine --version obiknovena --init o-l2.bin --positions 1000000 --rounds 8 --alpha 0.0003 --alpha-end 0.00003 --eval-pairs 100 --out o-l2-r.bin
```

## Lessons

- **α = 0.02 per state saturated the sigmoids.** With about 2,000 states summed per update, the network soon played
  one fixed policy and lost 97% to the baseline. α = 0.001 learned quickly: 80% against the baseline after 100k
  games.
- **`HashCode.Combine` must not seed anything.** .NET seeds it randomly per process, so the first runs could not be
  reproduced.
