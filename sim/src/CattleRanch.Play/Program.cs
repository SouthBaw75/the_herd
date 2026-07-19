using System.Globalization;

namespace CattleRanch.Play;

/// <summary>
/// Cattle Ranch (ugly prototype) — the Phase 1 fun gate (design doc §7, D9).
/// Plain Console.WriteLine/ReadLine, no TUI library. The whole point is to
/// play five in-game years and feel a real "should I sell now?" decision.
/// </summary>
public static class Program
{
    public const long DefaultSeed = 1701;

    public static int Main()
    {
        // Deterministic, shareable output regardless of the host locale.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch (IOException)
        {
            // Redirected/odd terminals: live with the default encoding.
        }

        Console.WriteLine("==================================================");
        Console.WriteLine("  CATTLE RANCH  (ugly prototype)");
        Console.WriteLine("  Five years. One herd. One recurring question:");
        Console.WriteLine("  should you sell now?");
        Console.WriteLine("==================================================");
        Console.WriteLine();

        while (true)
        {
            long seed = PromptSeed();
            var game = new Game(seed);
            GameResult result = game.Run();

            if (result == GameResult.Quit)
            {
                break;
            }

            // Game over — offer a fresh start.
            Console.Write("Start a new game? (y/n) > ");
            string? answer = ReadEchoedLine();
            Console.WriteLine();
            if (answer == null || !answer.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        Console.WriteLine("Thanks for ranching. The gate opens both ways.");
        return 0;
    }

    private static long PromptSeed()
    {
        Console.Write($"Seed for this run (blank = shared default {DefaultSeed}) > ");
        string? line = ReadEchoedLine();
        if (line != null && long.TryParse(line.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long seed))
        {
            Console.WriteLine($"Seeding the world with {seed}.");
            Console.WriteLine();
            return seed;
        }

        Console.WriteLine($"Using the shared default seed, {DefaultSeed} — runs are comparable.");
        Console.WriteLine();
        return DefaultSeed;
    }

    /// <summary>
    /// ReadLine, but when input is piped (scripted playthroughs) the line is
    /// echoed so the transcript reads like an interactive session.
    /// </summary>
    internal static string? ReadEchoedLine()
    {
        string? line = Console.ReadLine();
        if (line != null && Console.IsInputRedirected)
        {
            Console.WriteLine(line);
        }

        return line;
    }
}

public enum GameResult
{
    /// <summary>Player typed quit (or input ended).</summary>
    Quit,

    /// <summary>Economy.IsBroke — the bank foreclosed.</summary>
    GameOver,
}
