namespace CattleRanch.Sim.Systems
{
    /// <summary>
    /// All tunable constants for breeding, gestation, and calving, in one place
    /// (GrazingConfig-style). Design doc §4.1: offspring traits = blended
    /// parent averages ± random variance, with a small chance of a standout
    /// ("hybrid vigor") or a dud. §3 macro loop: cows are bred in fall and
    /// calve the following spring.
    /// </summary>
    public sealed class BreedingConfig
    {
        /// <summary>The season during which conception rolls happen at all.</summary>
        public Season BreedingSeason = Season.Fall;

        /// <summary>
        /// Daily conception chance for an open cow with a bull, at Fertility 50
        /// (genetic factor 1.0). At 0.08/day a cow left with a bull for the
        /// whole 28-day fall fails to conceive with probability
        /// 0.92^28 ≈ 10% — most healthy cows settle within the season, but a
        /// miss is possible, which is the intended drama.
        /// </summary>
        public double BaseConceptionChancePerDay = 0.08;

        /// <summary>Conception-chance multiplier at Genome.Fertility = 0 (linear map to Max at 100; 1.0 at 50).</summary>
        public double FertilityFactorMin = 0.5;

        /// <summary>Conception-chance multiplier at Genome.Fertility = 100.</summary>
        public double FertilityFactorMax = 1.5;

        /// <summary>
        /// Full gestation length in game days. Real-world bovine gestation is
        /// ~283 days ≈ 0.78 of a real year; scaled to our 112-day game year
        /// (D6) that is 0.78 × 112 ≈ 87 days — a fall conception calves in
        /// spring, matching the design doc's macro loop.
        /// </summary>
        public int GestationDays = 87;

        /// <summary>
        /// Minimum age (game days) for both cows and bulls to breed. Real
        /// heifers are first bred at ~15 months ≈ 1.25 real years; scaled to
        /// the 112-day game year that is 1.25 × 112 = 140 game days. (A
        /// separate bull minimum could be split out later if tuning wants it.)
        /// </summary>
        public int MinBreedingAgeDays = 140;

        /// <summary>Mean birth weight of a calf (kg).</summary>
        public double NewbornWeightKg = 35.0;

        /// <summary>Gaussian std-dev of birth weight (kg).</summary>
        public double NewbornWeightStdDevKg = 2.0;

        /// <summary>Hard floor on birth weight (kg); keeps the Gaussian tail from producing absurd or non-positive calves.</summary>
        public double NewbornWeightFloorKg = 25.0;

        /// <summary>
        /// Gaussian std-dev (trait points) added to each calf trait around the
        /// parents' mean. The engine of generational drift — and of the
        /// occasional pleasant surprise.
        /// </summary>
        public double TraitBlendStdDev = 6.0;

        /// <summary>Chance a calf is a standout: ALL traits get <see cref="HybridVigorBoost"/>.</summary>
        public double HybridVigorChance = 0.03;

        /// <summary>Trait points added to every trait of a hybrid-vigor calf (capped at 100).</summary>
        public double HybridVigorBoost = 8.0;

        /// <summary>Chance a calf is a dud: ALL traits lose <see cref="DudPenalty"/>. Mutually exclusive with vigor (vigor wins the shared roll).</summary>
        public double DudChance = 0.03;

        /// <summary>Trait points subtracted from every trait of a dud calf (floored at 0).</summary>
        public double DudPenalty = 8.0;

        /// <summary>Linear map of Genome.Fertility (0..100) onto the conception-chance multiplier range.</summary>
        public double FertilityFactor(float fertility) =>
            FertilityFactorMin + (FertilityFactorMax - FertilityFactorMin) * (fertility / 100.0);

        /// <summary>A fresh config with default values.</summary>
        public static BreedingConfig Default => new BreedingConfig();
    }
}
