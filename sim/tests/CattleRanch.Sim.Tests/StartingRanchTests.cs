using CattleRanch.Sim;
using CattleRanch.Sim.Scenarios;

namespace CattleRanch.Sim.Tests
{
    public class StartingRanchTests
    {
        [Fact]
        public void Composition_IsOneBullSixCowsThreeCalves_AllPastured()
        {
            RanchState state = StartingRanch.Create(42);

            Assert.Equal(15000m, state.Cash);
            Assert.Equal(10, state.Herd.Count);
            Assert.Single(state.Paddocks);
            Assert.Equal(10, state.Paddocks[0].OccupantIds.Count);
            Assert.Equal(2, System.Linq.Enumerable.Count(state.Herd, a => a.Sex == Sex.Male));
        }

        [Fact]
        public void Seed1701_ProducesThePinnedFoundingHerd()
        {
            // Pins the load-bearing RNG draw order (see StartingRanch doc).
            // These exact values were verified identical across the console
            // and web frontends before the scenario was centralized; if this
            // test breaks, every existing seed changes meaning — don't
            // "fix the test", fix the draw order.
            RanchState state = StartingRanch.Create(StartingRanch.DefaultSeed);

            Assert.Equal("A-0", state.Herd[0].Id);
            Assert.Equal(Sex.Male, state.Herd[0].Sex);
            Assert.Equal(840.0, state.Herd[0].WeightKg);

            Assert.Equal("A-9", state.Herd[9].Id);
            Assert.Equal(242.0, state.Herd[9].WeightKg, precision: 0);

            // Determinism: same seed, same ranch, bit for bit.
            RanchState twin = StartingRanch.Create(StartingRanch.DefaultSeed);
            for (int i = 0; i < state.Herd.Count; i++)
            {
                Assert.Equal(state.Herd[i].WeightKg, twin.Herd[i].WeightKg);
                Assert.Equal(state.Herd[i].Genome.Fertility, twin.Herd[i].Genome.Fertility);
            }
            Assert.Equal(state.Rng.State, twin.Rng.State);
        }
    }
}
