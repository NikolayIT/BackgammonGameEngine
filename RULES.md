# Rules of табла

This file sets out the rules as the engine plays them. Where the traditional sources disagree or say nothing, it
records the decision the engine makes. The owner agreed the rules from bg.wikipedia, tabla.bg, lan.bg and
favorite-games.com, and settled the open points on 2026-09-27. Anything that ports the rules, such as ednaigra.com's
TypeScript board, should follow this file and check itself against `test-vectors/`.

## 1. Common rules

- Two players, 15 checkers (пулове) each, and two dice.
- Each player numbers the points 1..24 in the direction it moves. Its checkers go from higher to lower numbers, and
  they are borne off below 1. The home board is the player's own points 1..6.
- A player must use the whole roll when it can. If only one die of a roll can be played, the larger one must be
  played when possible.
- A double (чифт) is played four times.
- There is no doubling cube.
- Bearing off starts once all 15 checkers are in the home board.
  - A die equal to a checker's point bears that checker off.
  - A die higher than the highest occupied point bears off a checker from that highest point, and only from there.
- A win is 1 point. **Марс** is 2 points: the loser has borne off no checker.
- **Opening roll:** each player rolls one die, and ties are rolled again. The higher die starts and plays both dice as
  its first roll. Every game of a match starts with an opening roll.

## 2. The versions

### Обикновена (права)

- Start, in each player's own numbering: 2 checkers on 24, 5 on 13, 3 on 8 and 5 on 6. The players move in opposite
  directions, so seat 1's point p is seat 0's 25 − p.
- Two or more checkers hold a point.
- A lone checker that is hit goes to the bar. It must re-enter into the opponent's home board, on the player's own
  point 25 − die, before any other move. A player with a checker on the bar and no entry point cannot move.

### Гюлбара

- All 15 checkers start on the player's own 24 point. The two start points are in diagonally opposite corners, and
  both players move the same way round.
  - Seat 1's point p is seat 0's ((p + 11) mod 24) + 1.
  - So seat 1's start (its 24) is seat 0's 12, and seat 1's home board is seat 0's 13..18.
- There is no hitting. A single checker holds a point, and a checker may never land where the opponent has one.
- Six points in a row (a prime) are allowed.
- Doubles escalate (section 3).

### Тапа

- All 15 checkers start on the player's own 24 point, which is the opponent's 1 point. The players move in opposite
  directions, as in обикновена.
- Landing on a lone opponent checker **pins** it (затапва).
  - A pinned checker cannot move until no opponent checker is left on its point.
  - Two or more opponent checkers block a point.
  - A point that holds the pinner's checkers on top of a pinned checker belongs to the pinner. The pinner may add
    checkers there. The pinned player may not land there: a pinning checker cannot itself be pinned.
- **Bearing off with a pinned checker:** while one of a player's checkers is pinned, that player bears nothing off. A
  pinned checker is either outside the home board, so the player is not yet home, or inside it. In the second case
  bg.wikipedia's rule applies: „Ако има настъпен пул в полето, където се изваждат, докато този пул не се освободи, не
  може да се вадят другите пулове.“ The player may still move checkers inside the home board.
- **Майка:** a player's *mother* is its last checker on its start point (its own 24).
  - The mover wins at once, as марс (2 points), when two things are true together: the opponent's mother is pinned by
    the mover, and the mover has no checkers left on its own start.
  - This is checked after every step. A player who pins the opponent's mother while it still has checkers on its own
    start does not win yet; it wins the moment its last start checker leaves, if the pin still holds. The pinner's
    own mother is still exposed until then, which is why bg.wikipedia makes the exception.
  - Winning is never forced: any legal play is allowed, including one that does not pin.
- **Both mothers pinned:** the game is a draw, and each player scores 1 point.

### Челеби

Обикновена with гюлбара's escalating doubles.

### Среща

The traditional match. Game n is played as обикновена when n mod 3 = 1, гюлбара when n mod 3 = 2 and тапа when
n mod 3 = 0. The version comes from the game number alone, so the rotation goes on after drawn and stuck games.

## 3. Escalating doubles (гюлбара and челеби)

- A double escalates from each player's **4th roll** of the game. For the starter, the opening roll counts as its 1st
  roll. The other player's opening die does not count as a roll of its own.
  - tabla.bg puts it this way: "До третото мятане на всеки играч, чифтовете се играят х2". bg.wikipedia says "в
    повечето варианти … на третия и следващите ходове"; the engine follows tabla.bg.
- An escalating n-n is played as four n's, then four (n+1)'s, and so on up to four 6's. A 6-6 is just four 6's, and a
  1-1 is 24 moves.
- Each group of four is a **stage**. Within a stage the usual rule applies: play as many dice as possible, and the
  most of them. Stages are not optimised across each other: each stage is judged on its own, from the position the
  previous one left.
- **Remainder:** when the roller cannot play a die of the chain, the rest of the chain passes to the opponent. The
  rest is the unplayed dice of the current stage plus all the later stages.
  - The opponent plays the remainder before it rolls, stage by stage. Dice it cannot play in a stage are lost, and it
    goes on with the next stage. Nothing passes back.
  - Playing a remainder is not a roll: it does not count towards the 4th roll.
  - In челеби the usual rules apply during a remainder: a checker on the bar must enter first, and hits happen.
- **An unstarted chain is lost:** if the roller cannot play even one die of the rolled double itself, nothing passes.
  The opponent simply rolls. bg.wikipedia: „Незапочнат чифт (без валиден ход с основната дължина) … не се
  доиграват.“
- In a player's first three rolls, and always in обикновена and тапа, a double is played normally. Dice of it that
  cannot be played are lost.

## 4. Stages and legal plays

A **stage** is what one action plays. It is one of these:

- the two different dice of a roll;
- the four equal dice of a double;
- one group of four of an escalating chain;
- one stage of a remainder, with one to four equal dice.

The legal plays of a stage are defined exactly as follows:

1. A play is a sequence of single legal steps, each with a die of the stage that has not been used yet.
2. A play is **complete** when it uses every die or when its last step **ends the game**. A step ends the game when it
   bears off the last checker, makes a майка, or pins the second mother. No step may follow one that ends the game,
   so the rest of the roll is not played.
3. The legal plays are those of the greatest *effective length*: the number of dice used, where a complete play counts
   as all of them.
4. With two different dice, when only one of them can be played, the larger one must be played if it can be. This
   does not apply when a play ends the game, because that play is complete.
5. When no die can be played, the stage passes by itself.

**Canonical steps.** Plays are ordered step by step: the step from the higher point comes first, the bar (25) being
the highest, and for the same point the larger die comes first. When one play is a prefix of another, the shorter
comes first. Each distinct end position has canonical steps: the first legal play in this order that reaches it. The
engine lists the end positions in the order of their canonical steps, and plays those steps when it plays a stage
itself.

## 5. Turns

- Dice are rolled automatically.
- A stage with no legal move passes by itself and is recorded as an automatic play with no steps.
- With the host option `AutoPlayForcedStages`, a stage whose legal plays all lead to the same position is played by
  the engine, with its canonical steps.
- **Stuck rule:** after 100 rolls in a row, by either side, that move no checker, the game ends as a draw with no
  points.
  - Any checker moved, including in a remainder, resets the count.
  - A position where any die can move something escapes with a probability of at least 11/36 per roll. So in practice
    the rule only ends positions where nothing can move at all, which is possible in гюлбара.

## 6. Scoring and matches

| Result | Points |
| --- | --- |
| Normal win: the loser has borne off at least one checker | 1 : 0 |
| Марс: the loser has borne off none | 2 : 0 |
| Майка (тапа) | 2 : 0 |
| Both mothers pinned (тапа) | 1 : 1 |
| Stuck | 0 : 0 |

- The single versions are played to 3 points, and the среща to 5.
- A match ends after a game in which a player has reached the target **and leads**.
  - A тапа draw at 2:2 in a match to 3 makes it 3:3, and play goes on.
  - A draw can end a match: 2:4 becomes 3:5 in a match to 5.

## 7. Randomness

The host supplies every die through `BackgammonMatchOptions.Dice(n, purpose)`, which returns a uniform integer
0..n−1. n is always 6, and the die is the value + 1. The draws come in exactly this order:

1. Each game starts with `opening` pairs: seat 0's die first, then seat 1's, until the two differ.
2. Every later roll is two `dice` draws, made when that roll starts: after the previous turn, and after any remainder
   played before it.

Escalating chains and remainders draw nothing, the rules use no other randomness, and nothing is drawn after the
match ends. The match record accounts for every draw:

- each game's `Openings`;
- the `Roll` of every play with `Stage == 0` that is neither a remainder nor the opening roll;
- the game's `PendingRoll`, the roll in play that has no play yet (for example in a match stopped mid-turn).

## 8. Numbering and codes

- Steps are `(from, die)` in the mover's own numbering. `from` is 1..24, or 25 for the bar, and the checker goes to
  `from − die`, where 0 or below means borne off.
- Boards in views are in seat 0's numbering. `BackgammonGeometry` converts between the two.
- The test vectors write a position as a text code, `V:p1,…,p24|bar0,bar1|off0,off1`:
  - `V` is `O` (обикновена), `G` (гюлбара), `T` (тапа) or `C` (челеби).
  - The points are in seat 0's numbering. An empty token means an empty point, a positive number is seat 0's
    checkers, and a negative number is seat 1's.
  - On a тапа point with a pinned checker, the token is the pinner's count followed by `*`. `2*` means two of seat
    0's checkers on a pinned seat-1 checker; `-1*` means one of seat 1's on a pinned seat-0 checker.
  - Example, the обикновена start: `O:-2,,,,,5,,3,,,,-5,5,,,,-3,,-5,,,,,2|0,0|0,0`.

## 9. Sources and decisions

- **Майка** is checked after every step; bg.wikipedia and tabla.bg both describe it as a state.
- **No bearing off while pinned in the home board** comes from bg.wikipedia; tabla.bg does not mention it.
- **An unstarted escalating double is lost** comes from bg.wikipedia; tabla.bg and lan.bg only say that the opponent
  finishes what is left.
- **Escalation from the 4th roll** follows tabla.bg; bg.wikipedia says the 3rd.
- **Гюлбара geometry:** the start points are diagonally opposite, as bg.wikipedia describes them ("горния десен ъгъл
  за единия играч и в долния ляв ъгъл за другия"). tabla.bg gives seat 1's start as seat 0's 13; the engine uses 12,
  the diagonal corner.
- **No doubling cube, no беко** (the triple game), and marses are not tripled.
