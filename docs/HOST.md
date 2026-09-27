# Plugging the engine into a host

How ednaigra.com (or any game server) plugs in BackgammonGameEngine and BackgammonGameEngine.Bots. It follows the
way ednaigra.com hosts SantaseGameEngine (`src/Games/EdnaIgra.Games.Santase`).

## Game and variants

- Game key `tabla` (the client splits preset ids on `-`, so the key has none), one rating pool for all versions.
- Variants and their `BackgammonMatchOptions.Variant`:

| Variant id | Variant | Target |
| --- | --- | --- |
| `tabla-sreshta` | `Sreshta` | 5 |
| `tabla-obiknovena` | `Obiknovena` | 3 |
| `tabla-gyulbara` | `Gyulbara` | 3 |
| `tabla-tapa` | `Tapa` | 3 |
| `tabla-chelebi` | `Chelebi` | 3 |

- The target can be a preset parameter (`target`), passed as `TargetPoints`.
- A match can only be won, never drawn. A game can be drawn (тапа) or stuck, but play goes on until a player reaches
  the target with a lead.
- The platform outcome is `Win(match.Winner)`, or whatever the host decides after `Stop()` (resignation, timeout).

## Driving the match (`IGameRules`)

- **`CreateInitial(preset, fair)`**: create the match and call `Start()`:

  ```csharp
  var match = new BackgammonMatch(new BackgammonMatchOptions
  {
      Variant = variant,
      TargetPoints = target,
      Dice = (n, purpose) => fair.NextInt(n, purpose),
  });
  match.Start();
  ```

- **`GetTurn`**: sequential, with `ActiveSeats = [match.ToMove]`.
- **`Validate`**: `match.Validate(seat, action)`. Map `NotYourTurn` to `not_your_turn`, `Illegal` to `illegal`, and
  `MatchFinished` to `game_over`.
- **`Apply`**: `match.Act(seat, action)`, which must return `Ok` after a successful `Validate`.
  - One action can set off automatic plays: passes, auto-played forced stages, a remainder handed over, the end of a
    game and the next game's opening.
  - Compare the view's `Ply` before and after. The new plays are this game's `Plays` (and `LastGame.Plays` if a game
    ended) with a higher `Ply`. Derive the `Pause` for animation from them.
- **`CreateView(state, seat)`**: map `match.GetView(seat)` to the host's view record. The engine's view is already
  a plain JSON model and can be embedded as it is.
- **`CreateFinalView`**: call `Stop()` if the match is not finished, then map `match.GetFinalView()`, whose `Record`
  holds every game.
- **`CreateTimeoutAction`**: null (a timeout loses the match), as for Santase.
- **Actions** are one stage each, in the mover's own numbering, with at most 4 steps, far below the 4 KB message
  limit. A `BackgammonAction` serializes as `{"steps":[{"from":13,"die":6},{"from":7,"die":1}]}`. (The test vectors
  write steps as `[from, die]` pairs, which is not the action's own JSON.)
- **`AutoPlayForcedStages`** saves a person the clicks when every legal play gives the same position.

## Fairness

Add these rows to `docs/fairness-v1.md`, "What each game draws":

| Purpose | n | When |
| --- | --- | --- |
| `opening` | 6 | At the start of every game, pairs (seat 0's die, then seat 1's) until they differ. |
| `dice` | 6 | Two per roll, when the roll starts. |

The client's `verifyExtra` rebuilds the draws from the final view's record:

1. For each game, its `openings` (two draws a pair).
2. Then the `roll` of every play with `stage == 0` that is neither `isRemainder` nor `isOpeningRoll`.
3. Then the game's `pendingRoll`.

It checks them against the published draws (purpose, n, value + 1 = die). `test-vectors/matches-*.json` have whole
matches with their draws, to test the verifier.

## Bots (`IBotStrategy`)

- **`Choose(view, level, random)`**: map the host's view back to a `BackgammonSeatView`, then call
  `BackgammonBot.Choose`. The history fields may be left empty; the bot only reads the position and the dice in play.
- **`Complexity(view)`**: `BackgammonBot.Complexity`. It is 0 when forced, about 1 for a typical roll, and up to 3.
- **Levels:** 1..6.
- **Speed:** a decision takes well under 20 ms on one core (see ARENA.md).
- **Determinism:** the bot is deterministic for a view, level and `Random`, and thread-safe.

## Clocks

ARENA.md lists the decisions a player makes in an average match of each variant. Divide the clock's total by those
decisions to get the average time a turn.

## The TypeScript board

- The board can port the move rules for tap-to-move: `BackgammonStageMoves` in C#, RULES.md sections 1–4.
- It must reproduce `test-vectors/moves-*.json` and `tricky.json` exactly, including the canonical steps.
- `test-vectors/README.md` describes the formats.
