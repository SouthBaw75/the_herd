namespace CattleRanch.Sim.Systems
{
    /// <summary>
    /// All tunable constants for the weather model, in one place
    /// (GrazingConfig-style: data, not buried literals). Phase 1 scope is "a
    /// simple weather modifier on grass": mostly-Normal days with
    /// season-dependent chances of an adverse (or beneficial) spell that lasts
    /// a bounded number of days and scales grass growth and animal weight gain.
    /// </summary>
    public sealed class WeatherConfig
    {
        // ------------------------------------------------------------------
        // Spell start chances. Each is the probability, on a NORMAL day, that
        // the given spell begins, indexed by Season {Spring, Summer, Fall,
        // Winter}. Per-season sums must stay &lt;= 1.0 (they are cumulative
        // buckets over a single uniform draw).
        // ------------------------------------------------------------------

        /// <summary>Daily chance a drought starts, by season. A summer hazard.</summary>
        public double[] DroughtDailyChance = { 0.000, 0.020, 0.005, 0.000 };

        /// <summary>Daily chance a heat wave starts, by season. Summer, edges of the shoulder seasons.</summary>
        public double[] HeatWaveDailyChance = { 0.005, 0.030, 0.005, 0.000 };

        /// <summary>Daily chance a blizzard starts, by season. A winter hazard.</summary>
        public double[] BlizzardDailyChance = { 0.000, 0.000, 0.005, 0.040 };

        /// <summary>Daily chance a wet spell starts, by season. A spring boon.</summary>
        public double[] WetSpringDailyChance = { 0.040, 0.000, 0.000, 0.000 };

        // ------------------------------------------------------------------
        // Spell durations (days, inclusive bounds). Drawn uniformly when a
        // spell starts. Droughts are long grinds; blizzards short and sharp.
        // ------------------------------------------------------------------

        public int DroughtMinDays = 10;
        public int DroughtMaxDays = 24;

        public int HeatWaveMinDays = 3;
        public int HeatWaveMaxDays = 7;

        public int BlizzardMinDays = 2;
        public int BlizzardMaxDays = 5;

        public int WetSpringMinDays = 5;
        public int WetSpringMaxDays = 12;

        // ------------------------------------------------------------------
        // Severities. Indexed by WeatherCondition {Normal, Drought, HeatWave,
        // Blizzard, WetSpring}.
        // ------------------------------------------------------------------

        /// <summary>
        /// Multiplier on daily grass regrowth. The engine passes the current
        /// value into the grass update (weather cannot reach into
        /// <see cref="GrazingSystem"/> itself).
        /// </summary>
        public double[] GrassGrowthMultipliers = { 1.0, 0.3, 0.6, 0.0, 1.3 };

        /// <summary>
        /// Multiplier on daily animal weight gain (heat and cold stress burn
        /// energy; a wet spring is pleasant but not fattening by itself).
        /// </summary>
        public double[] AnimalStressMultipliers = { 1.0, 0.85, 0.70, 0.50, 1.0 };

        /// <summary>Chance, on a Normal day in the given season, that <paramref name="condition"/> starts.</summary>
        public double SpellStartChance(WeatherCondition condition, Season season)
        {
            switch (condition)
            {
                case WeatherCondition.Drought: return DroughtDailyChance[(int)season];
                case WeatherCondition.HeatWave: return HeatWaveDailyChance[(int)season];
                case WeatherCondition.Blizzard: return BlizzardDailyChance[(int)season];
                case WeatherCondition.WetSpring: return WetSpringDailyChance[(int)season];
                default: return 0.0; // Normal is the absence of a spell.
            }
        }

        /// <summary>Inclusive minimum spell length in days.</summary>
        public int SpellMinDays(WeatherCondition condition)
        {
            switch (condition)
            {
                case WeatherCondition.Drought: return DroughtMinDays;
                case WeatherCondition.HeatWave: return HeatWaveMinDays;
                case WeatherCondition.Blizzard: return BlizzardMinDays;
                case WeatherCondition.WetSpring: return WetSpringMinDays;
                default: return 0;
            }
        }

        /// <summary>Inclusive maximum spell length in days.</summary>
        public int SpellMaxDays(WeatherCondition condition)
        {
            switch (condition)
            {
                case WeatherCondition.Drought: return DroughtMaxDays;
                case WeatherCondition.HeatWave: return HeatWaveMaxDays;
                case WeatherCondition.Blizzard: return BlizzardMaxDays;
                case WeatherCondition.WetSpring: return WetSpringMaxDays;
                default: return 0;
            }
        }

        /// <summary>Grass-growth multiplier for a condition.</summary>
        public double GrassGrowthMultiplierFor(WeatherCondition condition) =>
            GrassGrowthMultipliers[(int)condition];

        /// <summary>Animal weight-gain multiplier for a condition.</summary>
        public double AnimalStressMultiplierFor(WeatherCondition condition) =>
            AnimalStressMultipliers[(int)condition];

        /// <summary>A fresh config with default values.</summary>
        public static WeatherConfig Default => new WeatherConfig();
    }
}
