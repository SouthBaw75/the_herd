namespace CattleRanch.Sim.Systems
{
    /// <summary>
    /// All tunable constants for the grass/soil model, in one place. Data, not
    /// buried literals — balance can be tuned without touching logic. Defaults
    /// are Phase-0 starting values; see field comments for units and rationale.
    /// </summary>
    public sealed class GrazingConfig
    {
        /// <summary>Dry-matter intake per head per day (kg). ~2.5% of a 480 kg cow.</summary>
        public double DailyIntakeKg = 12.0;

        /// <summary>
        /// Logistic intrinsic regrowth rate (per day). Kept &lt; 1 so the discrete
        /// step never overshoots <see cref="Paddock.CarryingCapacity"/>.
        /// </summary>
        public double RegrowthRate = 0.40;

        /// <summary>Potential biomass ceiling per hectare at full soil health (kg DM).</summary>
        public double MaxCapacityPerHa = 3000.0;

        /// <summary>Roots always survive: biomass never falls below this per hectare (kg DM).</summary>
        public double ResidualBiomassPerHa = 50.0;

        /// <summary>Below this fraction of capacity, sustained grazing degrades soil.</summary>
        public double OverstockThresholdFrac = 0.25;

        /// <summary>Above this fraction of capacity, rested ground slowly recovers soil.</summary>
        public double RecoveryThresholdFrac = 0.50;

        /// <summary>Soil health lost per overstocked day (fast to strip).</summary>
        public double SoilDegradeRate = 0.010;

        /// <summary>Soil health regained per rested day (slow to heal — the core asymmetry).</summary>
        public double SoilRecoverRate = 0.002;

        /// <summary>Soil health floor.</summary>
        public double SoilHealthMin = 0.10;

        /// <summary>Soil health ceiling.</summary>
        public double SoilHealthMax = 1.0;

        /// <summary>
        /// Seasonal growth multipliers indexed by <see cref="Season"/>:
        /// Spring high, Summer moderate, Fall low, Winter ~0.
        /// </summary>
        public double[] SeasonalGrowthFactors = { 1.0, 0.6, 0.3, 0.0 };

        /// <summary>Growth multiplier for a season.</summary>
        public double SeasonalGrowthFactor(Season season) => SeasonalGrowthFactors[(int)season];

        /// <summary>A fresh config with default values.</summary>
        public static GrazingConfig Default => new GrazingConfig();
    }
}
