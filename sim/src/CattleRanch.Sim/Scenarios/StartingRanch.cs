using CattleRanch.Sim.Systems;

namespace CattleRanch.Sim.Scenarios
{
    /// <summary>
    /// THE canonical new-game starting scenario, shared by every frontend
    /// (console, web, Unity) so the same seed is the same ranch everywhere
    /// (D12 — no frontend re-implements this). $15,000 cash, one healthy
    /// 12-ha paddock near capacity, and a founding herd of 10: a mature bull,
    /// six cows, three weaned calves, all drawn from <c>state.Rng</c>.
    /// <para>
    /// The RNG call order in this file is load-bearing: changing it changes
    /// what every seed means. A regression test pins the seed-1701 herd.
    /// </para>
    /// </summary>
    public static class StartingRanch
    {
        public const decimal StartingCash = 15000m;

        /// <summary>The shared default seed, so playtesters can compare identical runs.</summary>
        public const long DefaultSeed = 1701;

        public static RanchState Create(long seed)
        {
            var state = new RanchState(seed, StartingCash);

            // One 12-ha paddock: healthy soil, grass near its ceiling. The
            // grazing system recomputes capacity as maxPerHa * area * soil
            // daily; the starting values just need to agree with that formula.
            const double area = 12.0;
            const double soil = 0.90;
            double capacity = GrazingConfig.Default.MaxCapacityPerHa * area * soil; // 32,400 kg DM
            var paddock = new Paddock("P-0", area, capacity * 0.92, soil, capacity);
            state.Paddocks.Add(paddock);

            // Founding herd. Ages in seasons (28 days each).
            AddAnimal(state, paddock, Sex.Male, ageSeasons: 16, weightKg: 800 + state.Rng.NextInt(0, 50)); // the bull
            for (int i = 0; i < 6; i++)
            {
                AddAnimal(state, paddock, Sex.Female,
                    ageSeasons: 10 + state.Rng.NextInt(0, 9),
                    weightKg: 450 + state.Rng.NextDouble() * 50.0);
            }

            for (int i = 0; i < 3; i++)
            {
                AddAnimal(state, paddock, i == 0 ? Sex.Male : Sex.Female,
                    ageSeasons: 2,
                    weightKg: 240 + state.Rng.NextDouble() * 25.0);
            }

            return state;
        }

        private static void AddAnimal(RanchState state, Paddock paddock, Sex sex, int ageSeasons, double weightKg)
        {
            var genome = new Genome(
                RollTrait(state), RollTrait(state), RollTrait(state), RollTrait(state),
                RollTrait(state), RollTrait(state), RollTrait(state));
            string breed = state.Rng.NextDouble() < 0.5 ? "Hereford" : "Angus";
            string id = state.AllocateId("A");
            var animal = new Animal(
                id,
                name: string.Empty,
                sex,
                breed,
                ageDays: ageSeasons * GameDate.DaysPerSeason,
                weightKg: System.Math.Round(weightKg, 1),
                genome);
            state.Herd.Add(animal);
            paddock.OccupantIds.Add(id);
        }

        /// <summary>Traits in 25..75 — room to breed up OR down later.</summary>
        private static float RollTrait(RanchState state) => 25f + state.Rng.NextInt(0, 51);
    }
}
