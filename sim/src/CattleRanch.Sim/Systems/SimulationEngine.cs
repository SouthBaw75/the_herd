namespace CattleRanch.Sim.Systems
{
    /// <summary>
    /// Drives a <see cref="RanchState"/> forward one day at a time. Phase 0 wires
    /// the clock to the grass heartbeat only (no market/weather/breeding). Each
    /// day: advance the date, then update every paddock's grass/soil from the
    /// head count grazing it. Deterministic — paddocks are iterated in list order.
    /// </summary>
    public sealed class SimulationEngine
    {
        private readonly GrazingSystem _grazing;

        public SimulationEngine(GrazingConfig? config = null)
        {
            _grazing = new GrazingSystem(config ?? GrazingConfig.Default);
        }

        public GrazingConfig Config => _grazing.Config;

        /// <summary>Advances the ranch exactly one day.</summary>
        public void Step(RanchState state)
        {
            state.Date = state.Date.AddDays(1);
            Season season = state.Date.Season;

            // Deterministic iteration: list order.
            for (int i = 0; i < state.Paddocks.Count; i++)
            {
                Paddock paddock = state.Paddocks[i];
                _grazing.UpdateDaily(paddock, paddock.OccupantIds.Count, season);
            }
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
