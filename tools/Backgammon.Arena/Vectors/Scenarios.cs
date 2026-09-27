namespace Backgammon.Arena
{
    using System.Collections.Generic;

    using Backgammon.Logic;

    /// <summary>
    /// The tricky positions of test-vectors/tricky.json. Each shows one rule of RULES.md at an edge. Seat 1's own point
    /// p is seat 0's 25 - p in обикновена, тапа and челеби, and ((p + 11) mod 24) + 1 in гюлбара.
    /// </summary>
    internal static class Scenarios
    {
        private const BackgammonVersion O = BackgammonVersion.Obiknovena;
        private const BackgammonVersion G = BackgammonVersion.Gyulbara;
        private const BackgammonVersion T = BackgammonVersion.Tapa;
        private const BackgammonVersion C = BackgammonVersion.Chelebi;

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("both-dice-by-order", "13/6 is blocked, but 13/5 then 8/6 plays both dice, so that is the only play.", O, 0, 6, 5).With(0, 13, 1).With(1, 18, 2);
            yield return new Scenario("larger-die", "Either die alone can be played but not both: the larger (6) must be played.", O, 0, 6, 5).With(0, 13, 1).With(1, 23, 2);
            yield return new Scenario("smaller-die-only", "Only the 5 can be played at all, so it is played.", O, 0, 6, 5).With(0, 13, 1).With(1, 18, 2).With(1, 23, 2);
            yield return new Scenario("double-partly-playable", "Only one 4 of the double can be played.", O, 0, 4, 4, 4, 4).With(0, 13, 1).With(1, 20, 2);
            yield return new Scenario("bear-off-exact", "Exact dice bear off; a 3 cannot bear off from the 2 while the 3 and 6 are occupied.", O, 0, 6, 3).With(0, 6, 1).With(0, 3, 1).With(0, 2, 1).With(1, 20, 12);
            yield return new Scenario("bear-off-higher-die", "A die higher than the highest point bears off from the highest point only.", O, 0, 6, 5).With(0, 4, 1).With(0, 2, 1).With(1, 20, 12);
            yield return new Scenario("no-bear-off-outside-home", "A checker on the 7 keeps everyone from bearing off.", O, 0, 2, 1).With(0, 7, 1).With(0, 2, 1).With(1, 20, 12);
            yield return new Scenario("last-checker-mid-roll", "The last checker borne off ends the game; the other die is not played.", O, 0, 6, 1).With(0, 2, 1).With(1, 20, 12);
            yield return new Scenario("last-checker-double", "The last checker off with the first of four 3s ends the game.", O, 0, 3, 3, 3, 3).With(0, 1, 1).With(1, 20, 12);
            yield return new Scenario("bar-first", "A checker on the bar enters before anything else moves.", O, 0, 6, 5).With(0, 25, 1).With(0, 13, 5).With(1, 19, 5);
            yield return new Scenario("bar-one-enters", "Two on the bar and the 6-point entry held: only the 5 can be played.", O, 0, 6, 5).With(0, 25, 2).With(0, 13, 5).With(1, 6, 2);
            yield return new Scenario("closed-board", "A checker on the bar against a closed board: no move.", O, 0, 6, 5).With(0, 25, 1).With(0, 13, 5).With(1, 1, 2).With(1, 2, 2).With(1, 3, 2).With(1, 4, 2).With(1, 5, 2).With(1, 6, 2);
            yield return new Scenario("bar-double", "Two checkers enter with a double, then the rest of it is free.", O, 0, 2, 2, 2, 2).With(0, 25, 2).With(0, 13, 5).With(1, 13, 5);
            yield return new Scenario("hit", "Landing on a lone checker sends it to the bar.", O, 0, 3, 1).With(0, 13, 2).With(1, 15, 1).With(1, 24, 5);
            yield return new Scenario("pin", "Тапа: landing on a lone checker pins it; it stays on the board.", T, 0, 3, 1).With(0, 13, 2).With(1, 15, 1).With(1, 24, 13);
            yield return new Scenario("pinner-holds-the-point", "Тапа: seat 1 may not land where its own checker is pinned.", T, 1, 2, 1).Pinned(1, 15).With(0, 13, 1).With(1, 17, 1).With(1, 24, 12);
            yield return new Scenario("pinned-cannot-move", "Тапа: the pinned checker cannot move; only the others can.", T, 1, 6, 5).Pinned(1, 15).With(1, 24, 13).With(0, 24, 5);
            yield return new Scenario("mother-empty-start", "Тапа: pinning the opponent's last start checker with an empty own start wins (майка).", T, 0, 2, 1).With(0, 3, 1).With(0, 13, 5).With(1, 24, 1).With(1, 10, 5);
            yield return new Scenario("mother-when-start-empties", "Тапа: 3/2 pins the mother while seat 0 still has a checker home; 24/5 then empties the start and wins.", T, 0, 5, 2).With(0, 24, 1).With(0, 3, 1).With(1, 24, 1).With(1, 10, 5);
            yield return new Scenario("both-mothers", "Тапа: pinning the second mother is a draw.", T, 1, 2, 1).Pinned(1, 24).With(0, 24, 1).With(0, 13, 5).With(1, 3, 1).With(1, 10, 5);
            yield return new Scenario("pinned-at-home-no-bear-off", "Тапа: a checker pinned in the home board stops all bearing off.", T, 0, 6, 5).With(0, 6, 2).With(0, 3, 1).Pinned(0, 2).With(1, 10, 5);
            yield return new Scenario("mother-many-ways", "Тапа: several plays win at once; all are complete.", T, 0, 6, 2).With(0, 3, 1).With(0, 9, 1).With(1, 24, 1).With(1, 10, 5);
            yield return new Scenario("gyulbara-single-holds", "Гюлбара: one opponent checker holds a point.", G, 0, 3, 2).With(0, 13, 1).With(1, 22, 1);
            yield return new Scenario("gyulbara-start-66", "Гюлбара: from the start a 6 cannot reach the opponent's start (seat 0's 12).", G, 0, 6, 6, 6, 6).With(0, 24, 15).With(1, 24, 15);
            yield return new Scenario("gyulbara-prime", "Гюлбара: seat 1 on its start behind seat 0's six-point prime (seat 0's 6..11) cannot move.", G, 1, 6, 5).With(1, 24, 3).With(0, 6, 2).With(0, 7, 2).With(0, 8, 2).With(0, 9, 2).With(0, 10, 2).With(0, 11, 2).With(0, 24, 3);
            yield return new Scenario("remainder-with-bar", "Челеби remainder of three 5s: the checker on the bar enters first.", C, 1, 5, 5, 5).With(1, 25, 1).With(1, 13, 5).With(0, 13, 5).With(0, 6, 5);
            yield return new Scenario("remainder-two-sixes", "Челеби remainder of two 6s.", C, 1, 6, 6).With(1, 24, 13).With(1, 19, 2).With(0, 12, 1);
        }
    }
}
