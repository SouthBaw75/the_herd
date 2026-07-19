using System.Globalization;

namespace CattleRanch.Play;

/// <summary>Small, dumb formatting helpers. Ugly on purpose; readable on purpose.</summary>
internal static class Render
{
    public static string Money(decimal amount)
    {
        string sign = amount < 0 ? "-" : string.Empty;
        return sign + "$" + System.Math.Abs(amount).ToString("N2", CultureInfo.InvariantCulture);
    }

    /// <summary>A crude bar like [########--] for a 0..1 fraction.</summary>
    public static string Bar(double fraction, int width = 10)
    {
        if (double.IsNaN(fraction))
        {
            fraction = 0;
        }

        fraction = System.Math.Clamp(fraction, 0.0, 1.0);
        int filled = (int)System.Math.Round(fraction * width);
        return "[" + new string('#', filled) + new string('-', width - filled) + "]";
    }

    /// <summary>
    /// A tiny sparkline over recent values, scaled to the window's own
    /// min..max. Flat windows render as a mid row. Ugly is fine.
    /// </summary>
    public static string Spark(IReadOnlyList<decimal> values)
    {
        const string ramp = "▁▂▃▄▅▆▇";
        if (values.Count == 0)
        {
            return "(no history yet)";
        }

        decimal min = values[0];
        decimal max = values[0];
        foreach (decimal v in values)
        {
            if (v < min) { min = v; }
            if (v > max) { max = v; }
        }

        var chars = new char[values.Count];
        for (int i = 0; i < values.Count; i++)
        {
            int idx = max == min
                ? ramp.Length / 2
                : (int)System.Math.Round((double)((values[i] - min) / (max - min)) * (ramp.Length - 1));
            chars[i] = ramp[idx];
        }

        return new string(chars);
    }

    public static string TrendArrow(double delta, double epsilon = 1e-9)
    {
        if (delta > epsilon) { return "▲"; }
        if (delta < -epsilon) { return "▼"; }
        return "→";
    }

    public static string Rule() => new string('─', 62);
}
