namespace CattleRanch.Sim.Systems
{
    /// <summary>
    /// Drives a <see cref="RanchState"/> forward one day at a time. The fixed
    /// daily update order is load-bearing for determinism (D10: every RNG draw
    /// happens in this order) — do not reorder without a decisions-log entry:
    /// <list type="number">
    ///   <item>date advances;</item>
    ///   <item>weather rolls (consumes RNG; multipliers for the day are fixed);</item>
    ///   <item>animals age and gain/lose weight reading the morning's grass
    ///         (before grazing depletes it) under weather stress;</item>
    ///   <item>breeding: conception rolls, gestation, births (consumes RNG at
    ///         this fixed point; after growth so ages are today's and newborns
    ///         are not growth-updated on their birth day, before grazing so a
    ///         calf joins its paddock before headcount-based intake);</item>
    ///   <item>each paddock's grass is grazed and regrows (list order), with the
    ///         weather multiplier on regrowth;</item>
    ///   <item>the market drifts/shocks (consumes RNG);</item>
    ///   <item>daily upkeep is debited.</item>
    /// </list>
    /// Go-broke (<see cref="Economy.IsBroke"/>) is checked by the caller, not
    /// the engine — game-over is a presentation decision.
    /// </summary>
    public sealed class SimulationEngine
    {
        private readonly SimConfig _config;
        private readonly GrazingSystem _grazing;
        private readonly WeatherSystem _weather;
        private readonly AnimalGrowthSystem _growth;
        private readonly BreedingSystem _breeding;

        public SimulationEngine(SimConfig? config = null)
        {
            _config = config ?? SimConfig.Default;
            _grazing = new GrazingSystem(_config.Grazing);
            _weather = new WeatherSystem(_config.Weather);
            _growth = new AnimalGrowthSystem(_config.Growth);
            _breeding = new BreedingSystem(_config.Breeding);
        }

        /// <summary>
        /// Convenience overload for grass-only scenarios/tests: everything else
        /// takes defaults.
        /// </summary>
        public SimulationEngine(GrazingConfig? grazingConfig)
            : this(new SimConfig { Grazing = grazingConfig ?? GrazingConfig.Default })
        {
        }

        public SimConfig Config => _config;

        /// <summary>
        /// Advances the ranch exactly one day, in the fixed order above.
        /// Returns today's breeding report (conceptions/births) so callers can
        /// announce them; callers that don't care simply ignore it.
        /// </summary>
        public BreedingReport Step(RanchState state)
        {
            // 1. Date.
            state.Date = state.Date.AddDays(1);
            Season season = state.Date.Season;

            // 2. Weather (RNG). Multipliers are fixed for the rest of the day.
            _weather.DailyUpdate(state);
            double grassMultiplier = _weather.GrassGrowthMultiplier(state);
            double stressMultiplier = _weather.AnimalStressMultiplier(state);

            // 3. Animals age/grow off the morning's grass (no RNG).
            _growth.DailyUpdate(state, stressMultiplier);

            // 3½. Breeding: conceptions, gestation, births (RNG at this fixed
            //     point — see order rationale in the class doc).
            BreedingReport breedingReport = _breeding.DailyUpdate(state);

            // 4. Grass: graze + regrow per paddock, list order (no RNG).
            for (int i = 0; i < state.Paddocks.Count; i++)
            {
                Paddock paddock = state.Paddocks[i];
                _grazing.UpdateDaily(paddock, paddock.OccupantIds.Count, season, grassMultiplier);
            }

            // 5. Market (RNG).
            state.Market.DailyUpdate(state, _config.Market);

            // 6. Costs.
            Economy.DailyUpkeep(state, _config.Market);

            return breedingReport;
        }

        /// <summary>Advances the ranch <paramref name="days"/> days.</summary>
        public void Advance(RanchState state, int days)
        {
            if (days < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(days), "Cannot advance a negative number of days.");
            }

            for (int i = 0; i < days; i++)
            {
                Step(state);
            }
        }
    }
}
