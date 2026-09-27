namespace Backgammon.Logic.Rules
{
    using System;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// A canonical text form of a position, used by the test vectors and in messages. The format is
    /// <c>V:p1,p2,...,p24|bar0,bar1|off0,off1</c>, where:
    /// <list type="bullet">
    /// <item><c>V</c> is the version: O (обикновена), G (гюлбара), T (тапа) or C (челеби).</item>
    /// <item>The 24 points are in seat 0's numbering. A point is empty text when nobody is on it, a positive count for
    /// seat 0's checkers, and a negative count for seat 1's.</item>
    /// <item>A тапа point where one checker is pinned shows the pinner's count followed by <c>*</c>. For example, <c>2*</c>
    /// means two of seat 0's checkers on top of a pinned seat-1 checker, and <c>-1*</c> means one seat-1 checker
    /// pinning a seat-0 checker. The pinned side always has exactly one checker there.</item>
    /// </list>
    /// </summary>
    internal static class PositionCode
    {
        private const string Letters = "OGTC";

        public static string Format(in Position position)
        {
            var text = new StringBuilder(96);
            text.Append(Letters[(int)position.Version]).Append(':');
            for (var point = 1; point <= 24; point++)
            {
                if (point > 1)
                {
                    text.Append(',');
                }

                var other = Geometry.Other(position.Version, point);
                var seat0 = position.Count(0, point);
                var seat1 = position.Count(1, other);
                if (position.IsPinned(1, other))
                {
                    text.Append(seat0.ToString(CultureInfo.InvariantCulture)).Append('*');
                }
                else if (position.IsPinned(0, point))
                {
                    text.Append((-seat1).ToString(CultureInfo.InvariantCulture)).Append('*');
                }
                else if (seat0 > 0)
                {
                    text.Append(seat0.ToString(CultureInfo.InvariantCulture));
                }
                else if (seat1 > 0)
                {
                    text.Append((-seat1).ToString(CultureInfo.InvariantCulture));
                }
            }

            text.Append('|').Append(position.Count(0, Geometry.Bar).ToString(CultureInfo.InvariantCulture))
                .Append(',').Append(position.Count(1, Geometry.Bar).ToString(CultureInfo.InvariantCulture))
                .Append('|').Append(position.Count(0, Geometry.Off).ToString(CultureInfo.InvariantCulture))
                .Append(',').Append(position.Count(1, Geometry.Off).ToString(CultureInfo.InvariantCulture));
            return text.ToString();
        }

        public static Position Parse(string code)
        {
            ArgumentNullException.ThrowIfNull(code);
            var fail = new FormatException($"'{code}' is not a position code.");
            if (code.Length < 3 || code[1] != ':')
            {
                throw fail;
            }

            var versionIndex = Letters.IndexOf(code[0], StringComparison.Ordinal);
            if (versionIndex < 0)
            {
                throw fail;
            }

            var version = (BackgammonVersion)versionIndex;
            var parts = code[2..].Split('|');
            if (parts.Length != 3)
            {
                throw fail;
            }

            var points = parts[0].Split(',');
            var bar = parts[1].Split(',');
            var off = parts[2].Split(',');
            if (points.Length != 24 || bar.Length != 2 || off.Length != 2)
            {
                throw fail;
            }

            var position = new Position { Version = version };
            for (var point = 1; point <= 24; point++)
            {
                var token = points[point - 1];
                if (token.Length == 0)
                {
                    continue;
                }

                var pinned = token.EndsWith('*');
                if (!int.TryParse(pinned ? token[..^1] : token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var count)
                    || count == 0 || count < -Geometry.Checkers || count > Geometry.Checkers
                    || (pinned && version != BackgammonVersion.Tapa))
                {
                    throw fail;
                }

                var other = Geometry.Other(version, point);
                if (count > 0)
                {
                    position.Set(0, point, count);
                    if (pinned)
                    {
                        position.Set(1, other, 1);
                        position.SetPinned(1, other, true);
                    }
                }
                else
                {
                    position.Set(1, other, -count);
                    if (pinned)
                    {
                        position.Set(0, point, 1);
                        position.SetPinned(0, point, true);
                    }
                }
            }

            for (var seat = 0; seat < 2; seat++)
            {
                if (!TryCount(bar[seat], out var onBar) || !TryCount(off[seat], out var borneOff) || (onBar > 0 && !Geometry.HasBar(version)))
                {
                    throw fail;
                }

                position.Set(seat, Geometry.Bar, onBar);
                position.Set(seat, Geometry.Off, borneOff);
                var total = 0;
                for (var point = Geometry.Off; point <= Geometry.Bar; point++)
                {
                    total += position.Count(seat, point);
                }

                if (total != Geometry.Checkers)
                {
                    throw fail;
                }
            }

            return position;
        }

        private static bool TryCount(string text, out int count) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out count) && count <= Geometry.Checkers;
    }
}
