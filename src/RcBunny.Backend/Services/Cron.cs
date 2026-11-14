using System.Text.RegularExpressions;

namespace RcBunny.Services;

/// <summary>Cron schedules (minute hour day-of-month month day-of-week) in plain words, and when they next run (ebunny's Cron).</summary>
public static class Cron
{
    /// <summary>The values one field allows (*, */n, a-b, a-b/n, lists), or null when it isn't valid.</summary>
    private static HashSet<int>? Field(string f, int min, int max)
    {
        var set = new HashSet<int>();
        foreach (var part in f.Split(','))
        {
            var m = Regex.Match(part, @"^(\*|(\d+)(?:-(\d+))?)(?:/(\d+))?$");
            if (!m.Success) return null;
            int from = min, to = max, step = m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : 1;
            if (m.Groups[2].Success) { from = int.Parse(m.Groups[2].Value); to = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : m.Groups[4].Success ? max : from; }
            if (step < 1 || from < min || to > max || from > to) return null;
            for (var v = from; v <= to; v += step) set.Add(v);
        }
        return set;
    }

    private static HashSet<int>[]? Parse(string expr)
    {
        var p = Regex.Split(expr.Trim(), @"\s+");
        if (p.Length != 5) return null;
        var fields = new[] { Field(p[0], 0, 59), Field(p[1], 0, 23), Field(p[2], 1, 31), Field(p[3], 1, 12), Field(p[4], 0, 7) };
        if (fields.Any(f => f == null)) return null;
        if (fields[4]!.Remove(7)) fields[4]!.Add(0); // 7 is Sunday too
        return fields!;
    }

    public static bool IsValid(string? expr) => expr != null && Parse(expr) != null;

    /// <summary>The next time it runs after now (local time), or null.</summary>
    public static DateTime? Next(string? expr)
    {
        if (expr == null || Parse(expr) is not { } f) return null;
        var p = Regex.Split(expr.Trim(), @"\s+");
        bool anyDom = p[2] == "*", anyDow = p[4] == "*";
        var t = DateTime.Now.AddSeconds(-DateTime.Now.Second).AddMilliseconds(-DateTime.Now.Millisecond).AddMinutes(1);
        for (var limit = t.AddYears(1); t < limit;)
        {
            // Like standard cron: when both day fields are limited, either one matching is enough
            var dayOk = anyDom && anyDow ? true : anyDom ? f[4].Contains((int)t.DayOfWeek) : anyDow ? f[2].Contains(t.Day) : f[2].Contains(t.Day) || f[4].Contains((int)t.DayOfWeek);
            if (!f[3].Contains(t.Month) || !dayOk) { t = t.Date.AddDays(1); continue; }
            if (!f[1].Contains(t.Hour)) { t = t.Date.AddHours(t.Hour + 1); continue; }
            if (!f[0].Contains(t.Minute)) { t = t.AddMinutes(1); continue; }
            return t;
        }
        return null;
    }

    /// <summary>"Every 15 minutes", "Every hour", "Daily at 6:30am", or the expression itself for anything less common.</summary>
    public static string Describe(string? expr)
    {
        if (expr == null) return "";
        var p = Regex.Split(expr.Trim(), @"\s+");
        if (p.Length != 5) return expr;
        var (min, hour, dom, mon, dow) = (p[0], p[1], p[2], p[3], p[4]);
        var everyDay = dom == "*" && mon == "*" && dow == "*";
        Match m;
        bool Digits(string s) => Regex.IsMatch(s, @"^\d+$");
        if (everyDay && hour == "*" && (m = Regex.Match(min, @"^\*/(\d+)$")).Success) return $"Every {m.Groups[1].Value} minutes";
        if (everyDay && hour == "*" && Digits(min)) return min == "0" ? "Every hour" : $"Every hour at :{min.PadLeft(2, '0')}";
        if (everyDay && Digits(min) && (m = Regex.Match(hour, @"^\*/(\d+)$")).Success) return $"Every {m.Groups[1].Value} hours";
        if (dom == "*" && mon == "*" && Digits(min) && Digits(hour))
        {
            var at = new DateTime(2000, 1, 1, int.Parse(hour) % 24, int.Parse(min) % 60, 0).ToString("h:mmtt").ToLowerInvariant();
            var days = dow switch { "1-5" => "Weekdays", "0" => "Sundays", "1" => "Mondays", "6" => "Saturdays", "*" => "Daily", _ => null };
            return days != null ? $"{days} at {at}" : expr;
        }
        return expr;
    }
}
