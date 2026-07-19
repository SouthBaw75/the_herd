namespace CattleRanch.Sim.Systems
{
    /// <summary>
    /// The current weather regime. Values index into the multiplier/duration
    /// arrays in <see cref="WeatherConfig"/> — keep the order in sync.
    /// </summary>
    public enum WeatherCondition
    {
        Normal = 0,
        Drought = 1,
        HeatWave = 2,
        Blizzard = 3,
        WetSpring = 4,
    }

    /// <summary>
    /// Serializable weather state owned by <see cref="RanchState.Weather"/>.
    /// Plain POCO, public-get / internal-set, no serialization attributes —
    /// persistence policy lives entirely in <c>RanchSave</c> (D8). Invariant:
    /// <see cref="Condition"/> is Normal iff <see cref="DaysRemaining"/> is 0.
    /// </summary>
    public sealed class WeatherState
    {
        /// <summary>Parameterless constructor for serialization (Newtonsoft-friendly).</summary>
        public WeatherState()
        {
            Condition = WeatherCondition.Normal;
        }

        /// <summary>The active condition (Normal when no spell is running).</summary>
        public WeatherCondition Condition { get; internal set; }

        /// <summary>Days left in the active spell, counting today; 0 when Normal.</summary>
        public int DaysRemaining { get; internal set; }
    }

    /// <summary>
    /// Daily weather driver. On a Normal day it makes exactly one uniform draw
    /// from <c>state.Rng</c> (plus one int draw if a spell starts) to decide
    /// whether a season-dependent spell begins; while a spell runs it only
    /// counts the spell down (no draws). All draws come from the single shared
    /// <see cref="RanchState.Rng"/> stream, in this fixed order, per D10 —
    /// the system never constructs its own RNG — so runs are reproducible and
    /// a save loaded mid-spell continues bit-identically.
    /// <para>
    /// The system does not touch grass or animals itself. The engine reads
    /// <see cref="GrassGrowthMultiplier"/> and passes it into the grass update,
    /// and passes <see cref="AnimalStressMultiplier"/> into
    /// <see cref="AnimalGrowthSystem.DailyUpdate"/>. Call order per day:
    /// weather first, then the systems that consume its multipliers.
    /// </para>
    /// </summary>
    public sealed class WeatherSystem
    {
        /// <summary>Spell kinds in the fixed order the daily roll walks them (cumulative buckets).</summary>
        private static readonly WeatherCondition[] SpellKinds =
        {
            WeatherCondition.Drought,
            WeatherCondition.HeatWave,
            WeatherCondition.Blizzard,
            WeatherCondition.WetSpring,
        };

        private readonly WeatherConfig _config;

        public WeatherSystem(WeatherConfig? config = null)
        {
            _config = config ?? WeatherConfig.Default;
        }

        public WeatherConfig Config => _config;

        /// <summary>
        /// Advances the weather one day. Call once per tick, after the date has
        /// advanced (season chances use <c>state.Date.Season</c>) and before any
        /// system that consumes the multipliers.
        /// </summary>
        public void DailyUpdate(RanchState state)
        {
            WeatherState weather = state.Weather;

            // A running spell just counts down — no RNG is consumed, so the
            // draw sequence is a pure function of state (save/load safe).
            if (weather.Condition != WeatherCondition.Normal)
            {
                weather.DaysRemaining -= 1;
                if (weather.DaysRemaining <= 0)
                {
                    weather.DaysRemaining = 0;
                    weather.Condition = WeatherCondition.Normal;
                }

                return;
            }

            // Normal day: one uniform draw, walked through the season's spell
            // chances as cumulative buckets in fixed SpellKinds order.
            Season season = state.Date.Season;
            double roll = state.Rng.NextDouble();
            double cumulative = 0.0;

            for (int i = 0; i < SpellKinds.Length; i++)
            {
                WeatherCondition kind = SpellKinds[i];
                cumulative += _config.SpellStartChance(kind, season);
                if (roll < cumulative)
                {
                    weather.Condition = kind;
                    weather.DaysRemaining = state.Rng.NextInt(
                        _config.SpellMinDays(kind), _config.SpellMaxDays(kind) + 1);
                    return;
                }
            }

            // No bucket hit: stays Normal.
        }

        /// <summary>
        /// Today's multiplier on grass regrowth. The engine passes this into the
        /// grass update at integration time (weather does not call
        /// <see cref="GrazingSystem"/> itself).
        /// </summary>
        public double GrassGrowthMultiplier(RanchState state) =>
            _config.GrassGrowthMultiplierFor(state.Weather.Condition);

        /// <summary>
        /// Today's multiplier on animal weight gain. The engine passes this into
        /// <see cref="AnimalGrowthSystem.DailyUpdate"/>.
        /// </summary>
        public double AnimalStressMultiplier(RanchState state) =>
            _config.AnimalStressMultiplierFor(state.Weather.Condition);
    }
}
