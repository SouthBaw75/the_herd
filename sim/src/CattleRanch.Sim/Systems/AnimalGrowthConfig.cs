namespace CattleRanch.Sim.Systems
{
    /// <summary>
    /// All tunable constants for animal growth and aging, in one place
    /// (GrazingConfig-style). The growth model: daily gain = base gain
    /// × genetic factor × distance-to-maturity × forage satisfaction × weather
    /// stress, minus a maintenance shortfall when forage is short.
    /// </summary>
    public sealed class AnimalGrowthConfig
    {
        /// <summary>
        /// Peak potential daily gain (kg) for a young animal at GrowthRate 50 on
        /// full forage. Tapers to 0 as weight approaches the mature asymptote.
        /// </summary>
        public double BaseDailyGainKg = 1.8;

        /// <summary>Genetic gain multiplier at Genome.GrowthRate = 0 (linear map to Max at 100; 1.0 at 50).</summary>
        public double GrowthRateFactorMin = 0.7;

        /// <summary>Genetic gain multiplier at Genome.GrowthRate = 100.</summary>
        public double GrowthRateFactorMax = 1.3;

        /// <summary>Mature-weight asymptote for a cow at GrowthRate 50 (kg).</summary>
        public double MatureWeightCowKg = 600.0;

        /// <summary>Mature-weight asymptote for a bull at GrowthRate 50 (kg).</summary>
        public double MatureWeightBullKg = 900.0;

        /// <summary>Mature-weight genetic scale at GrowthRate 0 (linear map to Max at 100; 1.0 at 50).</summary>
        public double MatureWeightGeneticMin = 0.85;

        /// <summary>Mature-weight genetic scale at GrowthRate 100.</summary>
        public double MatureWeightGeneticMax = 1.15;

        /// <summary>
        /// Daily weight LOST (kg) at zero forage satisfaction — an animal in no
        /// paddock, or on grass grazed down to the root residual, burns body
        /// mass to stay alive. Scales linearly down to 0 at full satisfaction.
        /// </summary>
        public double MaintenanceLossKgPerDay = 0.8;

        /// <summary>
        /// Dry-matter demand per head per day (kg) used to judge forage
        /// satisfaction. Initialized from <see cref="GrazingConfig.DailyIntakeKg"/>
        /// so the two models agree by default; override both together when tuning.
        /// </summary>
        public double DailyIntakeKg = GrazingConfig.Default.DailyIntakeKg;

        /// <summary>
        /// Ungrazeable root residual per hectare (kg DM). Initialized from
        /// <see cref="GrazingConfig.ResidualBiomassPerHa"/> so "grass is gone"
        /// means the same thing to grazing and growth.
        /// </summary>
        public double ResidualBiomassPerHa = GrazingConfig.Default.ResidualBiomassPerHa;

        /// <summary>
        /// Forage satisfaction for an animal assigned to no paddock: fully
        /// maintenance-short (it loses weight until someone pastures it).
        /// </summary>
        public double UnassignedForageSatisfaction = 0.0;

        /// <summary>Hard weight floor (kg); weight never goes negative. (Phase 2: starvation → health/death.)</summary>
        public double MinWeightKg = 0.0;

        /// <summary>Linear map of Genome.GrowthRate (0..100) onto the gain multiplier range.</summary>
        public double GrowthRateFactor(float growthRate) =>
            GrowthRateFactorMin + (GrowthRateFactorMax - GrowthRateFactorMin) * (growthRate / 100.0);

        /// <summary>Genome-scaled mature-weight asymptote for an animal (kg).</summary>
        public double MatureWeightKg(Sex sex, float growthRate)
        {
            double baseWeight = sex == Sex.Male ? MatureWeightBullKg : MatureWeightCowKg;
            double geneticScale = MatureWeightGeneticMin
                + (MatureWeightGeneticMax - MatureWeightGeneticMin) * (growthRate / 100.0);
            return baseWeight * geneticScale;
        }

        /// <summary>A fresh config with default values.</summary>
        public static AnimalGrowthConfig Default => new AnimalGrowthConfig();
    }
}
