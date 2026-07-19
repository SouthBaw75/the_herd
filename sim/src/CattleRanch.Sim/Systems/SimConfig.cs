namespace CattleRanch.Sim.Systems
{
    /// <summary>
    /// Aggregate of every system's tunables — one object to pass around, one
    /// place balance lives. In Unity these become ScriptableObject-authored
    /// data deserialized into these same POCOs (design doc §6); the sim never
    /// knows the difference.
    /// </summary>
    public sealed class SimConfig
    {
        public GrazingConfig Grazing = GrazingConfig.Default;
        public WeatherConfig Weather = WeatherConfig.Default;
        public AnimalGrowthConfig Growth = AnimalGrowthConfig.Default;
        public MarketConfig Market = MarketConfig.Default;
        public BreedingConfig Breeding = BreedingConfig.Default;

        /// <summary>A fresh config with default values for every system.</summary>
        public static SimConfig Default => new SimConfig();
    }
}
