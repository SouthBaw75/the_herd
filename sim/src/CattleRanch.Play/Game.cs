using CattleRanch.Sim;
using CattleRanch.Sim.Persistence;
using CattleRanch.Sim.Systems;

namespace CattleRanch.Play;

/// <summary>
/// One playthrough: the weekly micro-loop from design doc §3. Status panel →
/// commands until 'go' → 7 engine steps → week report → repeat, until the
/// player quits or <see cref="Economy.IsBroke"/>. All sim access goes through
/// the public sim API; everything here is presentation and bookkeeping.
/// </summary>
public sealed class Game
{
    private const string SavePath = "ranch-save.json";
    private const int PriceWindowWeeks = 4;
    private const int DaysPerWeek = 7;

    private readonly SimulationEngine _engine = new SimulationEngine();
    private RanchState _state;

    // Play-layer trackers only — deliberately NOT sim state, reset on load.
    private readonly List<decimal> _priceHistory = new();
    private int _lastSampledDay = -1;

    private enum CommandOutcome
    {
        Advance,
        Quit,
        Redraw,
    }

    public Game(long seed)
    {
        _state = NewGame.Create(seed);
    }

    private MarketConfig MarketCfg => _engine.Config.Market;

    private decimal PricePerKg => _state.Market.PricePerKg(MarketCfg, _state.Date.Season);

    public GameResult Run()
    {
        Console.WriteLine("You inherit a run-down ranch: one paddock, ten head, and "
            + Render.Money(NewGame.StartingCash) + " in the bank.");
        Console.WriteLine("Type 'help' for commands. 'go' advances one week.");
        Console.WriteLine();

        while (true)
        {
            SamplePrice();
            RenderStatus();

            CommandOutcome outcome = CommandLoop();
            if (outcome == CommandOutcome.Quit)
            {
                return GameResult.Quit;
            }

            if (outcome == CommandOutcome.Redraw)
            {
                if (Economy.IsBroke(_state))
                {
                    RenderGameOver();
                    return GameResult.GameOver;
                }

                continue;
            }

            bool broke = AdvanceWeek();
            if (broke)
            {
                RenderGameOver();
                return GameResult.GameOver;
            }
        }
    }

    // ------------------------------------------------------------------
    // Status panel
    // ------------------------------------------------------------------

    private void RenderStatus()
    {
        GameDate date = _state.Date;
        int week = date.TotalDays / DaysPerWeek + 1;

        Console.WriteLine(Render.Rule());
        Console.WriteLine($" WEEK {week} | Year {date.Year}, {date.Season} — day {date.DayOfSeason} of {GameDate.DaysPerSeason}");
        Console.WriteLine(" Weather: " + WeatherLine());
        Console.WriteLine($" Cash: {Render.Money(_state.Cash)} | Herd: {_state.Herd.Count} head");

        foreach (Paddock p in _state.Paddocks)
        {
            double frac = p.CarryingCapacity > 0 ? p.GrassBiomass / p.CarryingCapacity : 0.0;
            Console.WriteLine(
                $" Paddock {p.Id}: grass {Render.Bar(frac)} {frac * 100:F0}% of capacity | soil {p.SoilHealth * 100:F0}%");
        }

        List<decimal> window = PriceWindow();
        Console.WriteLine(
            $" Market: {Render.Money(PricePerKg)}/kg | last {window.Count}wk: {Render.Spark(window)} | {MarketHint()}");
        Console.WriteLine(" " + RunwayLine());
        Console.WriteLine(Render.Rule());
        Console.WriteLine(" Commands: herd, look <#>, sell <# ...>, name <#> <name>, save, load, go, quit, help");
    }

    private string WeatherLine()
    {
        WeatherState w = _state.Weather;
        (string name, string flavor) = w.Condition switch
        {
            WeatherCondition.Drought => ("Drought", "(grass barely grows)"),
            WeatherCondition.HeatWave => ("Heat wave", "(cattle stressed — slow gains)"),
            WeatherCondition.Blizzard => ("Blizzard", "(no growth; cattle burn weight)"),
            WeatherCondition.WetSpring => ("Wet spell", "(grass loves it)"),
            _ => ("Fair", "(no complaints)"),
        };

        if (w.Condition == WeatherCondition.Normal)
        {
            return $"{name} {flavor}";
        }

        // TotalSpellDays lives in the sim state now, so this works even from a
        // freshly loaded save.
        int dayOfSpell = w.TotalSpellDays - w.DaysRemaining + 1;
        return $"{name}, day {dayOfSpell} of ~{w.TotalSpellDays} {flavor}";
    }

    private string MarketHint()
    {
        double shock = _state.Market.ShockOffset;
        if (shock < -0.05)
        {
            return "market crashed — shock in effect";
        }

        if (shock > 0.05)
        {
            return "market spiking — shock in effect";
        }

        string seasonal = _state.Date.Season switch
        {
            Season.Spring => "prices strong for spring",
            Season.Summer => "steady summer market",
            Season.Fall => "fall glut — the seasonal low",
            _ => "modest winter premium",
        };

        double drift = _state.Market.DriftMultiplier;
        if (drift > 1.04)
        {
            return seasonal + ", running hot";
        }

        if (drift < 0.96)
        {
            return seasonal + ", running soft";
        }

        return seasonal;
    }

    private string RunwayLine()
    {
        decimal weeklyBurn = Economy.DailyBurn(_state, MarketCfg) * DaysPerWeek;
        if (weeklyBurn <= 0)
        {
            return "Runway: no burn. Suspiciously comfortable.";
        }

        int weeks = (int)(_state.Cash / weeklyBurn);
        string unit = weeks == 1 ? "week" : "weeks";
        return $"Runway: upkeep {Render.Money(weeklyBurn)}/week — with no sales, cash lasts ~{weeks} {unit}";
    }

    private List<decimal> PriceWindow()
    {
        int count = Math.Min(_priceHistory.Count, PriceWindowWeeks);
        return _priceHistory.GetRange(_priceHistory.Count - count, count);
    }

    private void SamplePrice()
    {
        if (_state.Date.TotalDays == _lastSampledDay)
        {
            return;
        }

        _lastSampledDay = _state.Date.TotalDays;
        _priceHistory.Add(PricePerKg);
        if (_priceHistory.Count > 16)
        {
            _priceHistory.RemoveAt(0);
        }
    }

    // ------------------------------------------------------------------
    // Commands
    // ------------------------------------------------------------------

    private CommandOutcome CommandLoop()
    {
        while (true)
        {
            Console.Write("> ");
            string? line = Program.ReadEchoedLine();
            if (line == null)
            {
                Console.WriteLine();
                return CommandOutcome.Quit; // EOF: scripted run ended.
            }

            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            switch (parts[0].ToLowerInvariant())
            {
                case "go":
                    return CommandOutcome.Advance;
                case "quit":
                case "exit":
                    return CommandOutcome.Quit;
                case "help":
                    PrintHelp();
                    break;
                case "herd":
                    PrintHerd();
                    break;
                case "look":
                    Look(parts);
                    break;
                case "sell":
                    Sell(parts);
                    break;
                case "name":
                    NameAnimal(parts);
                    break;
                case "save":
                    Save();
                    break;
                case "load":
                    if (Load())
                    {
                        return CommandOutcome.Redraw;
                    }

                    break;
                default:
                    Console.WriteLine($" Unknown command '{parts[0]}'. Type 'help'.");
                    break;
            }
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine(" herd              — list every animal with today's sale quote");
        Console.WriteLine(" look <#>          — one animal's genetics, up close");
        Console.WriteLine(" sell <# # ...>    — sell animals at today's quote (asks first)");
        Console.WriteLine(" name <#> <name>   — name an animal (you will get attached)");
        Console.WriteLine(" save / load       — write/read ranch-save.json in this directory");
        Console.WriteLine(" go                — advance one week and see what the world did");
        Console.WriteLine(" quit              — leave the ranch");
    }

    private void PrintHerd()
    {
        if (_state.Herd.Count == 0)
        {
            Console.WriteLine(" The herd is empty. The paddock is very quiet.");
            return;
        }

        Console.WriteLine("   #  ID     NAME          SEX     AGE      WEIGHT     QUOTE");
        decimal total = 0m;
        for (int i = 0; i < _state.Herd.Count; i++)
        {
            Animal a = _state.Herd[i];
            decimal quote = _state.Market.QuoteFor(a, MarketCfg, _state.Date.Season);
            total += quote;
            int seasons = a.AgeDays / GameDate.DaysPerSeason;
            Console.WriteLine(
                $"  {i + 1,2}  {a.Id,-5}  {DisplayName(a),-12}  {SexLabel(a),-6}  {seasons,3} sns  {a.WeightKg,5:F0} kg  {Render.Money(quote),10}");
        }

        Console.WriteLine($"  Whole herd at today's prices: {Render.Money(total)}");
    }

    private void Look(string[] parts)
    {
        if (parts.Length < 2 || !TryGetAnimal(parts[1], out Animal a))
        {
            if (parts.Length < 2)
            {
                Console.WriteLine(" Usage: look <#>   (numbers from the 'herd' table)");
            }

            return;
        }

        decimal quote = _state.Market.QuoteFor(a, MarketCfg, _state.Date.Season);
        int seasons = a.AgeDays / GameDate.DaysPerSeason;
        string named = string.IsNullOrEmpty(a.Name) ? string.Empty : $" '{a.Name}'";
        Console.WriteLine(
            $" {a.Id}{named} — {SexLabel(a)}, {a.Breed}, {seasons} seasons old, {a.WeightKg:F0} kg, quote {Render.Money(quote)}");
        PrintTrait("Growth rate", a.Genome.GrowthRate);
        PrintTrait("Calving ease", a.Genome.CalvingEase);
        PrintTrait("Marbling", a.Genome.Marbling);
        PrintTrait("Mothering", a.Genome.Mothering);
        PrintTrait("Heat tolerance", a.Genome.HeatTolerance);
        PrintTrait("Disease resist", a.Genome.DiseaseResistance);
        PrintTrait("Fertility", a.Genome.Fertility);
    }

    private static void PrintTrait(string label, float value)
    {
        Console.WriteLine($"   {label,-15} {Render.Bar(value / 100.0)} {value:F0}");
    }

    private void Sell(string[] parts)
    {
        if (parts.Length < 2)
        {
            Console.WriteLine(" Usage: sell <# # ...>   (numbers from the 'herd' table)");
            return;
        }

        var picks = new List<Animal>();
        for (int i = 1; i < parts.Length; i++)
        {
            if (!TryGetAnimal(parts[i], out Animal a))
            {
                return; // message already printed; sell nothing on a bad list.
            }

            if (!picks.Contains(a))
            {
                picks.Add(a);
            }
        }

        decimal total = 0m;
        Console.WriteLine(" To the sale barn:");
        foreach (Animal a in picks)
        {
            decimal quote = _state.Market.QuoteFor(a, MarketCfg, _state.Date.Season);
            total += quote;
            string named = string.IsNullOrEmpty(a.Name) ? string.Empty : $" '{a.Name}'";
            Console.WriteLine($"   {a.Id}{named} ({SexLabel(a)}, {a.WeightKg:F0} kg) — {Render.Money(quote)}");
        }

        Console.Write($" Sell {picks.Count} head for {Render.Money(total)}? (y/n) > ");
        string? answer = Program.ReadEchoedLine();
        if (answer == null || !answer.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(" Kept them. The question doesn't go away, though.");
            return;
        }

        decimal proceeds = 0m;
        foreach (Animal a in picks)
        {
            proceeds += Economy.SellAnimal(_state, MarketCfg, a.Id);
        }

        Console.WriteLine(
            $" Sold {picks.Count} head. {Render.Money(proceeds)} banked. Cash is now {Render.Money(_state.Cash)}.");
    }

    private void NameAnimal(string[] parts)
    {
        if (parts.Length < 3)
        {
            Console.WriteLine(" Usage: name <#> <name>");
            return;
        }

        if (!TryGetAnimal(parts[1], out Animal a))
        {
            return;
        }

        string name = string.Join(' ', parts, 2, parts.Length - 2);
        a.Name = name;
        Console.WriteLine($" {a.Id} is now '{name}'. Try not to get attached. (You will.)");
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(SavePath, RanchSave.ToJson(_state));
            Console.WriteLine($" Saved to {SavePath}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine(" Could not write the save: " + ex.Message);
        }
    }

    private bool Load()
    {
        if (!File.Exists(SavePath))
        {
            Console.WriteLine($" No {SavePath} here to load.");
            return false;
        }

        try
        {
            _state = RanchSave.FromJson(File.ReadAllText(SavePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or ArgumentException)
        {
            Console.WriteLine(" Could not load the save: " + ex.Message);
            return false;
        }

        // Play-layer trackers do not live in the save; start them over.
        _priceHistory.Clear();
        _lastSampledDay = -1;
        Console.WriteLine($" Loaded {SavePath}. Where were we...");
        Console.WriteLine();
        return true;
    }

    private bool TryGetAnimal(string token, out Animal animal)
    {
        if (int.TryParse(token, out int n) && n >= 1 && n <= _state.Herd.Count)
        {
            animal = _state.Herd[n - 1];
            return true;
        }

        Console.WriteLine($" '{token}' isn't a herd number (1..{_state.Herd.Count}). See 'herd'.");
        animal = null!;
        return false;
    }

    // ------------------------------------------------------------------
    // Advancing time
    // ------------------------------------------------------------------

    /// <summary>Runs 7 daily steps, reports the week, returns true if broke.</summary>
    private bool AdvanceWeek()
    {
        var preIds = new HashSet<string>();
        foreach (Animal a in _state.Herd)
        {
            preIds.Add(a.Id);
        }

        double preAvgWeight = AverageWeight();
        double preGrassFrac = GrassFraction();
        decimal prePrice = PricePerKg;
        decimal preCash = _state.Cash;

        var events = new List<string>();
        WeatherCondition prevCond = _state.Weather.Condition;
        Season prevSeason = _state.Date.Season;
        bool broke = false;
        int daysRun = 0;

        for (int d = 1; d <= DaysPerWeek; d++)
        {
            _engine.Step(_state);
            daysRun = d;

            WeatherCondition cond = _state.Weather.Condition;
            if (cond != prevCond)
            {
                if (cond != WeatherCondition.Normal)
                {
                    events.Add($"Day {d}: {WeatherName(cond)} set in — should last ~{_state.Weather.TotalSpellDays} days.");
                }
                else
                {
                    events.Add($"Day {d}: the {WeatherName(prevCond).ToLowerInvariant()} broke. Back to fair weather.");
                }

                prevCond = cond;
            }

            if (_state.Date.Season != prevSeason)
            {
                events.Add($"Day {d}: the season turned — it is now {_state.Date.Season}, Year {_state.Date.Year}.");
                if (_state.Date.Season == Season.Winter)
                {
                    events.Add("       WINTER WARNING: grass stops growing until spring; upkeep does not."
                        + " What's standing in the paddock is the feed you've got.");
                }

                prevSeason = _state.Date.Season;
            }

            if (Economy.IsBroke(_state))
            {
                broke = true;
                break;
            }
        }

        Console.WriteLine();
        Console.WriteLine($" ── Week report ({daysRun} day{(daysRun == 1 ? string.Empty : "s")}) ──────────────────────────────");
        foreach (string e in events)
        {
            Console.WriteLine("  • " + e);
        }

        if (events.Count == 0)
        {
            Console.WriteLine("  • Quiet week. The weather held.");
        }

        // New arrivals (births, once the lead wires breeding into Step): any id
        // we didn't have when the week started.
        foreach (Animal a in _state.Herd)
        {
            if (!preIds.Contains(a.Id))
            {
                Console.WriteLine($"  • 🐄 A calf was born! {a.Id} joins the herd.");
            }
        }

        if (_state.Herd.Count > 0)
        {
            double avg = AverageWeight();
            double delta = avg - preAvgWeight;
            string sign = delta >= 0 ? "+" : string.Empty;
            Console.WriteLine($"  • Herd: avg {avg:F0} kg ({sign}{delta:F1} kg this week) {Render.TrendArrow(delta, 0.05)}");
        }
        else
        {
            Console.WriteLine("  • Herd: empty. The overhead bills arrive anyway.");
        }

        double grassFrac = GrassFraction();
        Console.WriteLine(
            $"  • Grass: {grassFrac * 100:F0}% of capacity {Render.TrendArrow(grassFrac - preGrassFrac, 0.001)} (was {preGrassFrac * 100:F0}%)");

        decimal price = PricePerKg;
        Console.WriteLine(
            $"  • Price: {Render.Money(prePrice)}/kg → {Render.Money(price)}/kg {Render.TrendArrow((double)(price - prePrice), 0.001)}");

        decimal cashDelta = _state.Cash - preCash;
        Console.WriteLine(
            $"  • Cash: {Render.Money(preCash)} → {Render.Money(_state.Cash)} ({Render.Money(cashDelta)} — upkeep drains daily)");
        Console.WriteLine();

        return broke;
    }

    private void RenderGameOver()
    {
        int totalDays = _state.Date.TotalDays;
        int years = totalDays / GameDate.DaysPerYear;
        int weeks = totalDays % GameDate.DaysPerYear / DaysPerWeek;

        Console.WriteLine();
        Console.WriteLine("==================================================");
        Console.WriteLine("            THE BANK HAS FORECLOSED");
        Console.WriteLine("==================================================");
        Console.WriteLine($" The ranch lasted {years} full year(s) and {weeks} week(s).");
        Console.WriteLine($" Final herd: {_state.Herd.Count} head, sold at the gate for less than they were worth.");
        Console.WriteLine($" Final cash: {Render.Money(_state.Cash)}.");
        Console.WriteLine(" The land will recover. It always does.");
        Console.WriteLine();
    }

    // ------------------------------------------------------------------
    // Small readers
    // ------------------------------------------------------------------

    private double AverageWeight()
    {
        if (_state.Herd.Count == 0)
        {
            return 0.0;
        }

        double sum = 0.0;
        foreach (Animal a in _state.Herd)
        {
            sum += a.WeightKg;
        }

        return sum / _state.Herd.Count;
    }

    private double GrassFraction()
    {
        double biomass = 0.0;
        double capacity = 0.0;
        foreach (Paddock p in _state.Paddocks)
        {
            biomass += p.GrassBiomass;
            capacity += p.CarryingCapacity;
        }

        return capacity > 0 ? biomass / capacity : 0.0;
    }

    private static string WeatherName(WeatherCondition condition) => condition switch
    {
        WeatherCondition.Drought => "Drought",
        WeatherCondition.HeatWave => "Heat wave",
        WeatherCondition.Blizzard => "Blizzard",
        WeatherCondition.WetSpring => "Wet spell",
        _ => "Fair weather",
    };

    private static string SexLabel(Animal a)
    {
        bool calf = a.AgeDays < 6 * GameDate.DaysPerSeason;
        if (calf)
        {
            return a.Sex == Sex.Male ? "calf-m" : "calf-f";
        }

        return a.Sex == Sex.Male ? "bull" : "cow";
    }

    private static string DisplayName(Animal a)
    {
        if (string.IsNullOrEmpty(a.Name))
        {
            return "—";
        }

        return a.Name.Length <= 12 ? a.Name : a.Name.Substring(0, 12);
    }
}
