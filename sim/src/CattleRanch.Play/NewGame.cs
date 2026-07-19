using CattleRanch.Sim;

namespace CattleRanch.Play;

/// <summary>
/// Builds the fixed starting scenario: $15,000, one healthy 12-ha paddock near
/// capacity, and a founding herd of 10 (1 mature bull, 6 cows, 3 weaned
/// calves). Genome/weight variety is drawn from <c>state.Rng</c>, so the whole
/// start is a pure function of the seed — same seed, same ranch.
/// </summary>
internal static class NewGame
{
    public const decimal StartingCash = 15000m;

    public static RanchState Create(long seed)
    {
        var state = new RanchState(seed, StartingCash);

        // One 12-ha paddock: healthy soil, grass near its ceiling. The grazing
        // system recomputes capacity as maxPerHa * area * soil daily; the
        // starting values just need to agree with that formula.
        const double area = 12.0;
        const double soil = 0.90;
        double capacity = Sim.Systems.GrazingConfig.Default.MaxCapacityPerHa * area * soil; // 32,400 kg DM
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
