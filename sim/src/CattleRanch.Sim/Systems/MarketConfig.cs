namespace CattleRanch.Sim.Systems
{
    /// <summary>
    /// All tunable constants for the market/economy model, in one place
    /// (GrazingConfig style). Data, not buried literals — balance can be tuned
    /// without touching logic. Money fields are <c>decimal</c> (matching
    /// <see cref="RanchState.Cash"/>); dimensionless model parameters are
    /// <c>double</c> per the architecture's sim-math rule.
    /// </summary>
    public sealed class MarketConfig
    {
        // ---- Price level -----------------------------------------------------

        /// <summary>Base sale price per kg liveweight ($/kg) before any multiplier.</summary>
        public decimal BasePricePerKg = 2.60m;

        /// <summary>
        /// Seasonal price multipliers indexed by <see cref="Season"/>:
        /// scarcity premium in Spring, fall glut cheapest — the classic cow-calf
        /// cycle (everyone weans and sells in Fall, restockers buy in Spring).
        /// Order: Spring, Summer, Fall, Winter.
        /// </summary>
        public double[] SeasonalPriceMultipliers = { 1.15, 1.00, 0.85, 1.05 };

        /// <summary>Seasonal price multiplier for a season.</summary>
        public double SeasonalPriceMultiplier(Season season) => SeasonalPriceMultipliers[(int)season];

        // ---- Drift (mean-reverting random walk) ------------------------------

        /// <summary>Std-dev of the daily Gaussian step applied to the drift multiplier.</summary>
        public double DriftStdDev = 0.010;

        /// <summary>
        /// Fraction of the gap back to the seasonal baseline closed per day
        /// (AR(1) pull). Keeps the drift multiplier stationary around 1.0.
        /// </summary>
        public double MeanReversionRate = 0.05;

        // ---- Shocks (rare jolts that decay away) -----------------------------

        /// <summary>Probability per day of a market shock (~1 every 250 days at 0.004).</summary>
        public double ShockChancePerDay = 0.004;

        /// <summary>Smallest absolute shock jolt to the price multiplier.</summary>
        public double ShockMagnitudeMin = 0.15;

        /// <summary>Largest absolute shock jolt to the price multiplier.</summary>
        public double ShockMagnitudeMax = 0.40;

        /// <summary>
        /// Fraction of the outstanding shock offset that decays each day.
        /// At 0.15/day a shock has a half-life of ~4 days and is negligible
        /// (&lt;1% of its size) within ~30 days.
        /// </summary>
        public double ShockDecayRate = 0.15;

        // ---- Bounds ----------------------------------------------------------

        /// <summary>Effective price floor as a fraction of <see cref="BasePricePerKg"/>.</summary>
        public double PriceFloorFrac = 0.50;

        /// <summary>Effective price ceiling as a fraction of <see cref="BasePricePerKg"/>.</summary>
        public double PriceCeilingFrac = 1.60;

        // ---- Daily costs -----------------------------------------------------

        /// <summary>
        /// Aggregate daily cost per head ($): feed, vet, fuel rolled into one
        /// Phase 1 tunable.
        /// </summary>
        public decimal UpkeepPerHeadPerDay = 1.50m;

        /// <summary>Fixed ranch overhead per day ($), independent of herd size.</summary>
        public decimal OverheadPerDay = 20.00m;

        /// <summary>A fresh config with default values.</summary>
        public static MarketConfig Default => new MarketConfig();
    }
}
