# Arena results

What the arena (`tools/Backgammon.Arena`) measured for the bots of BackgammonGameEngine.Bots 1.0.0. It ran on an
i7-12700K with 20 threads, with the networks of NEURAL_NETWORK.md: the refined layout-2 networks of обикновена, тапа
and челеби, and 1.0's гюлбара network.

- Every match is to the variant's usual target: 3, or 5 for a среща.
- Matches are played in **duplicate pairs**: the same dice seed twice, with the players swapping seats.
- **Every action of every bot is validated before it is played.** No bot made an illegal play, in all the runs below
  and in the bot tests.

## The levels

`ladder all 1000 random L1 L2 L3 L4 L5 L6 baseline` is a full round robin of eight players per variant: 28 pairings of
2,000 matches, so each level played 14,000 matches per variant and 70,000 in all. The Bradley–Terry ratings are
anchored at level 1 = 800. `baseline` is the hand-written evaluator with level 6's search, and `random` plays
uniformly random legal plays.

| Variant | random | L1 | L2 | L3 | L4 | L5 | L6 | baseline |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Обикновена | 607 | 800 | 1195 | 1590 | 1962 | 2358 | 2694 | 2179 |
| Гюлбара | 568 | 800 | 934 | 1073 | 1220 | 1362 | 1504 | 1232 |
| Тапа | 629 | 800 | 1033 | 1282 | 1539 | 1801 | 2021 | 1614 |
| Челеби | 606 | 800 | 899 | 992 | 1082 | 1170 | 1263 | 1093 |
| Среща | 605 | 800 | 1073 | 1396 | 1725 | 2137 | 2544 | 2019 |

- **The levels are in strict order in every variant.** Within a version the steps are even: about 380 Elo in
  обикновена, 245 in тапа, 140 in гюлбара and 93 in челеби.
  - Escalating doubles compress гюлбара and челеби. A single roll can move 84 pips, so even the best play wins only
    so often.
  - A среща mixes the three versions, so its steps grow towards the top, where обикновена and тапа spread widest.
- **Level 1 is weak but not random.** It wins 75–79% of its matches against random play:

  | Variant | Random play against L1 |
  | --- | --- |
  | Обикновена | 24.7% |
  | Гюлбара | 20.9% |
  | Тапа | 23.4% |
  | Челеби | 24.0% |
  | Среща | 23.8% |

- **Level 6 against its neighbours.** L5 wins 13.5% (обикновена), 32.0% (гюлбара), 25.0% (тапа), 35.4% (челеби) and
  9.1% (среща) of its matches against it. Random play won 0, 8, 0, 42 and 0 of 2,000.

### How the levels are set

- Level 6 is the network with the full search:
  - the stages already known are played on;
  - the two best plays are looked at one roll deeper;
  - the deep search has a hard budget of 3,000 evaluations.
- Levels 1–5 use the same network at 0-ply, with Gaussian noise on each play's match-winning chance
  (`BotLevels.Noise`).

| Version | L1 | L2 | L3 | L4 | L5 |
| --- | --- | --- | --- | --- | --- |
| Обикновена | 0.4323 | 0.1396 | 0.0751 | 0.0425 | 0.0181 |
| Гюлбара | 0.0593 | 0.0334 | 0.0204 | 0.0120 | 0.0048 |
| Тапа | 0.3876 | 0.0941 | 0.0423 | 0.0224 | 0.0091 |
| Челеби | 0.0778 | 0.0457 | 0.0289 | 0.0173 | 0.0075 |

The noises were set in two steps:

1. `calibrate <version> 300` rates a chain of 18 players per version (random, the network at 16 falling noises, level
   6), each against its two nearest neighbours. Level 1 is the noise rated 191 Elo above random, and levels 2–5 sit
   at even steps up to level 6.
2. A full round robin rates the levels differently from the chain, so `ladder <version> 300 random L1 … L6 baseline`
   was run with those noises, and levels 2–5 took the noises it suggests for even steps between its own level 1 and
   level 6. Once its suggestions only moved within the measuring noise, the last two sets were averaged.

Гюлбара kept 1.0's network, so it kept 1.0's noises; the round robin above confirms its even steps.

## The networks against the baseline

The gate that decided which networks ship: 2,000 matches each against the hand-written evaluator (1,000 duplicate
pairs), both with level 6's search, from `Backgammon.Trainer validate`. The round robin's own L6-against-baseline
pairing agrees.

| Version | Gate | In the round robin (2,000 matches) |
| --- | --- | --- |
| Обикновена | 95.15% ± 0.94% | 95.3% ± 0.9% |
| Гюлбара | 81.15% ± 1.71% (1.0) | 81.6% ± 1.7% |
| Тапа | 92.70% ± 1.14% | 93.4% ± 1.1% |
| Челеби | 71.90% ± 1.97% | 69.6% ± 2.0% |
| Среща | (by version) | 95.5% ± 0.9% |

Against 1.0's networks, both with level 6's search, the new ones won 58.0% (обикновена), 62.4% (тапа) and 53.1%
(челеби) of 10,000 matches (NEURAL_NETWORK.md).

## Decision time

`timing all 40 L6` plays self-play on **one thread** of an idle machine and times every decision. Times are in
milliseconds:

| Variant | p50 | p90 | p99 | max | mean |
| --- | --- | --- | --- | --- | --- |
| Обикновена | 0.79 | 2.49 | 4.30 | 5.12 | 1.07 |
| Гюлбара | 0.74 | 3.79 | 4.40 | 5.82 | 1.48 |
| Тапа | 1.29 | 3.98 | 5.38 | 7.49 | 1.65 |
| Челеби | 0.76 | 4.19 | 4.86 | 6.16 | 1.38 |
| Среща | 0.93 | 3.67 | 5.16 | 6.35 | 1.40 |

- Every decision of level 6 is under 8 ms, and 99% are under 5.5 ms. The budget counts evaluations, never the clock,
  so it bounds the time without making decisions depend on the machine's speed.
- Levels 1–5 do no search. Their p99 is about 1 ms and their worst decision 4.9 ms (L1 and L5 measured).
- The tools ask Windows not to throttle them. 1.0's timings (a worst decision of 16.4 ms) were most likely taken on
  the efficiency cores.

## Match length, for the clocks

How many decisions each player makes in a match, from level 6 self-play. A decision is one stage to play: a roll,
or one group of four of an escalating chain. Stages with no choice are played by the engine and not counted.

| Variant | Games a match | Decisions per player | At 8 s a decision | At 13 s a decision |
| --- | --- | --- | --- | --- |
| Обикновена | 3.9 | 92 | 12.2 min | 19.8 min |
| Гюлбара | 3.1 | 100 | 13.4 min | 21.7 min |
| Тапа | 3.0 | 134 | 17.9 min | 29.1 min |
| Челеби | 3.0 | 51 | 6.7 min | 10.9 min |
| Среща | 5.6 | 185 | 24.6 min | 40.0 min |

Weaker players make more decisions per match: their games last longer, and a match between them has more games.
The arena's head-to-heads report decisions a match for every pairing.

## Reproducing

From a copy of the arena's `bin`:

```
Backgammon.Arena ladder all 1000 random L1 L2 L3 L4 L5 L6 baseline
Backgammon.Arena timing all 40 L6
Backgammon.Arena calibrate obiknovena 300
```
