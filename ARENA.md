# Arena results

What the arena (`tools/Backgammon.Arena`) measured for the bots of BackgammonGameEngine.Bots 1.0.0. It ran on an
i7-12700K with 20 threads, with the networks of NEURAL_NETWORK.md.

- Every match is to the variant's usual target: 3, or 5 for a среща.
- Matches are played in **duplicate pairs**: the same dice seed twice, with the players swapping seats.
- **Every action of every bot is validated before it is played.** No bot made an illegal play, in all the runs below
  and in the bot tests.

## The levels

`ladder all 300 random L1 L2 L3 L4 L5 L6 baseline` is a full round robin of eight players per variant: 28 pairings of
600 matches, so each level played 4,200 matches per variant and 21,000 in all. The Bradley–Terry ratings are anchored
at level 1 = 800. `baseline` is the hand-written evaluator with level 6's search, and `random` plays uniformly random
legal plays.

| Variant | random | L1 | L2 | L3 | L4 | L5 | L6 | baseline |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Обикновена | 590 | 800 | 1143 | 1484 | 1831 | 2236 | 2571 | 2021 |
| Гюлбара | 591 | 800 | 925 | 1076 | 1208 | 1356 | 1499 | 1233 |
| Тапа | 622 | 800 | 1103 | 1399 | 1704 | 1984 | 2277 | 1898 |
| Челеби | 588 | 800 | 875 | 973 | 1062 | 1149 | 1217 | 1059 |
| Среща | 574 | 800 | 1062 | 1350 | 1757 | 2178 | 2600 | 2103 |

- **The levels are in strict order in every variant.** Within a version the steps are even: about 340 Elo in
  обикновена, 295 in тапа, 140 in гюлбара and 85 in челеби.
  - Escalating doubles compress гюлбара and челеби. A single roll can move 84 pips, so even the best play wins only
    so often.
  - A среща mixes the three versions, so its steps grow towards the top, where обикновена and тапа spread widest.
- **Level 1 is weak but not random.** It wins 77–80% of its matches against random play:

  | Variant | Random play against L1 |
  | --- | --- |
  | Обикновена | 23.2% |
  | Гюлбара | 21.5% |
  | Тапа | 23.5% |
  | Челеби | 23.2% |
  | Среща | 20.3% |

- **Level 6 against its neighbours.** L5 wins 13.8% (обикновена), 32.2% (гюлбара), 17.5% (тапа), 40.3% (челеби) and
  8.7% (среща) of its matches against it. Random play won 0, 3, 0, 12 and 0 of 600.
- **The remainder fix (2026-09-27) leaves these ratings as they are.** They were measured before it. The bots used
  to value a play in the middle of a remainder as if the opponent rolled next, and the fix changes about half of those
  plays, but such plays are rare. Head to head, the fixed bots against the old ones scored:

  | Level | Гюлбара | Челеби |
  | --- | --- | --- |
  | 6 | 50.2% ± 1.0% (10,000 matches) | 50.1% ± 0.6% (30,000) |
  | 3 | 49.7% ± 0.5% (40,000) | 49.4% ± 0.5% (40,000) |

### How the levels are set

- Level 6 is the network with the full search:
  - the stages already known are played on;
  - the two best plays are looked at one roll deeper;
  - the deep search has a hard budget of 3,000 evaluations.
- Levels 1–5 use the same network at 0-ply, with Gaussian noise on each play's match-winning chance
  (`BotLevels.Noise`).

| Version | L1 | L2 | L3 | L4 | L5 |
| --- | --- | --- | --- | --- | --- |
| Обикновена | 0.3283 | 0.1266 | 0.0730 | 0.0384 | 0.0152 |
| Гюлбара | 0.0593 | 0.0334 | 0.0204 | 0.0120 | 0.0048 |
| Тапа | 0.4283 | 0.1106 | 0.0546 | 0.0280 | 0.0130 |
| Челеби | 0.0734 | 0.0444 | 0.0260 | 0.0150 | 0.0046 |

The noises were set in two steps:

1. `calibrate all 300` rates a chain of 18 players per version (random, the network at 16 falling noises, level 6),
   each against its two nearest neighbours. Level 1 is the noise rated 191 Elo above random, and levels 2–5 sit at
   even steps up to level 6.
2. A full round robin stretches the top compared with the chain, so levels 2–5 of обикновена and тапа were
   re-interpolated twice from the ladder's own ratings.

## The networks against the baseline

The gate that decided which networks ship: 2,000 matches each against the hand-written evaluator (1,000 duplicate
pairs), both with level 6's search, from `Backgammon.Trainer validate`. The ladder's own L6-against-baseline pairing
agrees.

| Version | Gate | In the ladder (600 matches) |
| --- | --- | --- |
| Обикновена | 94.15% ± 1.03% | 96.3% ± 1.5% |
| Гюлбара | 81.15% ± 1.71% | 83.2% ± 3.0% |
| Тапа | 90.20% ± 1.30% | 91.2% ± 2.3% |
| Челеби | 69.50% ± 2.02% | 70.2% ± 3.7% |
| Среща | (by version) | 95.3% ± 1.7% |

## Decision time

`timing all 40 L6` plays self-play on **one thread** of an idle machine and times every decision. Times are in
milliseconds:

| Variant | p50 | p90 | p99 | max | mean |
| --- | --- | --- | --- | --- | --- |
| Обикновена | 1.2 | 3.6 | 6.2 | 10.1 | 1.6 |
| Гюлбара | 1.4 | 7.4 | 8.9 | 15.5 | 2.8 |
| Тапа | 1.9 | 5.8 | 8.6 | 11.8 | 2.4 |
| Челеби | 1.3 | 6.3 | 8.0 | 10.1 | 2.2 |
| Среща | 1.6 | 6.6 | 8.6 | 16.4 | 2.4 |

- Every decision of level 6 is under 20 ms, and 99% are under 9 ms. The budget counts evaluations, never the clock,
  so it bounds the time without making decisions depend on the machine's speed.
- Levels 1–5 do no search. Their p99 is about 1 ms and their worst decision 5.5 ms (L1 and L5 measured).

## Match length, for the clocks

How many decisions each player makes in a match, from level 6 self-play. A decision is one stage to play: a roll,
or one group of four of an escalating chain. Stages with no choice are played by the engine and not counted.

| Variant | Games a match | Decisions per player | At 8 s a decision | At 13 s a decision |
| --- | --- | --- | --- | --- |
| Обикновена | 3.6 | 87 | 11.6 min | 18.9 min |
| Гюлбара | 3.1 | 104 | 13.8 min | 22.4 min |
| Тапа | 3.0 | 135 | 18.0 min | 29.2 min |
| Челеби | 2.6 | 42 | 5.6 min | 9.1 min |
| Среща | 5.6 | 186 | 24.8 min | 40.3 min |

Weaker players make more decisions per match: their games last longer, and a match between them has more games.
The arena's head-to-heads report decisions a match for every pairing.

## Reproducing

From a copy of the arena's `bin`:

```
Backgammon.Arena ladder all 300 random L1 L2 L3 L4 L5 L6 baseline
Backgammon.Arena timing all 40 L6
Backgammon.Arena calibrate all 300
```
