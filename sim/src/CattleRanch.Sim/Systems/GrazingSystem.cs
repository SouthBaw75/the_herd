namespace CattleRanch.Sim.Systems
{
    /// <summary>
    /// The daily grass/soil heartbeat. For each paddock, each day:
    /// <list type="number">
    ///   <item>carrying capacity is derived from soil health and area;</item>
    ///   <item>biomass grows on a logistic curve, scaled by soil health and a
    ///         seasonal factor (winter ≈ 0);</item>
    ///   <item>grazing subtracts <c>headCount × dailyIntake</c>, flooring at the
    ///         root residual;</item>
    ///   <item>sustained overstock (biomass held below a low threshold) degrades
    ///         soil health quickly; rest above a higher threshold recovers it
    ///         slowly — fast to strip, slow to heal.</item>
    /// </list>
    /// Pure arithmetic and deterministic: no RNG, no wall-clock.
    /// </summary>
    public sealed class GrazingSystem
    {
        private readonly GrazingConfig _config;

        public GrazingSystem(GrazingConfig? config = null)
        {
            _config = config ?? GrazingConfig.Default;
        }

        public GrazingConfig Config => _config;

        /// <summary>
        /// Advances one paddock by a single day given the number of head grazing
        /// it. <paramref name="weatherGrowthMultiplier"/> is today's
        /// <see cref="WeatherSystem.GrassGrowthMultiplier"/> (1.0 when running
        /// without weather) — it scales regrowth only, never intake.
        /// </summary>
        public void UpdateDaily(Paddock paddock, int headCount, Season season, double weatherGrowthMultiplier = 1.0)
        {
            // 1. Carrying capacity is a function of soil health and area.
            double capacity = _config.MaxCapacityPerHa * paddock.AreaHectares * paddock.SoilHealth;
            double residual = _config.ResidualBiomassPerHa * paddock.AreaHectares;
            double biomass = paddock.GrassBiomass;

            // 2. Logistic regrowth, modulated by soil health and season.
            //    growth = r · soil · seasonal · B · (1 − B/K).
            //    With r < 1 and soil,seasonal ∈ [0,1] the step cannot overshoot K.
            if (capacity > 0.0 && biomass < capacity)
            {
                double seasonal = _config.SeasonalGrowthFactor(season);
                double growth = _config.RegrowthRate
                                * paddock.SoilHealth
                                * seasonal
                                * weatherGrowthMultiplier
                                * biomass
                                * (1.0 - biomass / capacity);
                biomass += growth;
                if (biomass > capacity)
                {
                    biomass = capacity; // defensive clamp against float rounding.
                }
            }

            // 3. Grazing removes intake; roots survive at the residual floor.
            double intake = headCount * _config.DailyIntakeKg;
            biomass -= intake;
            if (biomass < residual)
            {
                biomass = residual;
            }

            // 4. Soil dynamics — the asymmetry. Thresholds are fractions of the
            //    (soil-dependent) capacity.
            double soil = paddock.SoilHealth;
            double overstockThreshold = _config.OverstockThresholdFrac * capacity;
            double recoveryThreshold = _config.RecoveryThresholdFrac * capacity;

            if (biomass < overstockThreshold)
            {
                soil -= _config.SoilDegradeRate;
            }
            else if (biomass > recoveryThreshold)
            {
                soil += _config.SoilRecoverRate;
            }

            if (soil < _config.SoilHealthMin)
            {
                soil = _config.SoilHealthMin;
            }
            else if (soil > _config.SoilHealthMax)
            {
                soil = _config.SoilHealthMax;
            }

            // 5. Commit. Capacity is recomputed from the updated soil so it stays
            //    consistent for readers and for the next day's step.
            paddock.GrassBiomass = biomass;
            paddock.SoilHealth = soil;
            paddock.CarryingCapacity = _config.MaxCapacityPerHa * paddock.AreaHectares * soil;
        }
    }
}
