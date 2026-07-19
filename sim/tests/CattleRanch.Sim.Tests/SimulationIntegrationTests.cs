using System.Collections.Generic;
using CattleRanch.Sim;
using CattleRanch.Sim.Persistence;
using CattleRanch.Sim.Systems;

namespace CattleRanch.Sim.Tests
{
    /// <summary>
    /// Full-pipeline tests: date → weather → growth → grazing → market → upkeep
    /// all running together over multi-year horizons.
    /// </summary>
    public class SimulationIntegrationTests
    {
        private static RanchState NewRanch(long seed)
        {
            var state = new RanchState(seed);
            state.Cash = 20_000m;

            var paddock = new Paddock("P-0", 10.0, 25_000.0, 1.0, 30_000.0);
            state.Paddocks.Add(paddock);

            for (int i = 0; i < 8; i++)
            {
                var genome = new Genome(
                    40f + 3f * i, 50f, 50f, 50f, 50f, 50f, 50f);
                var animal = new Animal(
                    state.AllocateId("A"),
                    string.Empty,
                    i % 4 == 0 ? Sex.Male : Sex.Female,
                    "Angus",
                    ageDays: 400 + 10 * i,
                    weightKg: 320.0 + 5.0 * i,
                    genome);
                state.Herd.Add(animal);
                paddock.OccupantIds.Add(animal.Id);
            }

            return state;
        }

        [Fact]
        public void FiveYears_FullPipeline_StaysSaneAndDeterministic()
        {
            var a = NewRanch(2026);
            var b = NewRanch(2026);
            var engine = new SimulationEngine();

            engine.Advance(a, 5 * GameDate.DaysPerYear);
            new SimulationEngine().Advance(b, 5 * GameDate.DaysPerYear);

            // Sanity: nothing degenerate after 560 days of everything running.
            Assert.Equal(5 * GameDate.DaysPerYear, a.Date.TotalDays);
            foreach (Animal animal in a.Herd)
            {
                Assert.False(double.IsNaN(animal.WeightKg) || double.IsInfinity(animal.WeightKg));
                Assert.True(animal.WeightKg >= 0.0);
                Assert.True(animal.WeightKg < 1200.0, $"weight ran away: {animal.WeightKg}");
            }
            foreach (Paddock paddock in a.Paddocks)
            {
                Assert.False(double.IsNaN(paddock.GrassBiomass));
                Assert.InRange(paddock.SoilHealth, 0.0, 1.0);
                Assert.True(paddock.GrassBiomass >= 0.0);
                Assert.True(paddock.GrassBiomass <= paddock.CarryingCapacity + 1e-6);
            }
            double priceMult = a.Market.CurrentMultiplier(MarketConfig.Default, a.Date.Season);
            Assert.InRange(priceMult, MarketConfig.Default.PriceFloorFrac, MarketConfig.Default.PriceCeilingFrac);

            // Determinism: bit-identical twin runs.
            Assert.Equal(a.Cash, b.Cash);
            Assert.Equal(a.Rng.State, b.Rng.State);
            Assert.Equal(a.Weather.Condition, b.Weather.Condition);
            Assert.Equal(a.Market.DriftMultiplier, b.Market.DriftMultiplier);
            for (int i = 0; i < a.Herd.Count; i++)
            {
                Assert.Equal(a.Herd[i].WeightKg, b.Herd[i].WeightKg);
                Assert.Equal(a.Herd[i].AgeDays, b.Herd[i].AgeDays);
            }
            Assert.Equal(a.Paddocks[0].GrassBiomass, b.Paddocks[0].GrassBiomass);
            Assert.Equal(a.Paddocks[0].SoilHealth, b.Paddocks[0].SoilHealth);
        }

        [Fact]
        public void MidRunSaveLoad_FullPipeline_ContinuesBitIdentically()
        {
            var original = NewRanch(777);
            var engine = new SimulationEngine();

            // Advance into year 2, save, and let both copies run 2 more years.
            engine.Advance(original, 150);
            RanchState restored = RanchSave.FromJson(RanchSave.ToJson(original));

            engine.Advance(original, 2 * GameDate.DaysPerYear);
            new SimulationEngine().Advance(restored, 2 * GameDate.DaysPerYear);

            Assert.Equal(original.Cash, restored.Cash);
            Assert.Equal(original.Rng.State, restored.Rng.State);
            Assert.Equal(original.Weather.Condition, restored.Weather.Condition);
            Assert.Equal(original.Weather.DaysRemaining, restored.Weather.DaysRemaining);
            Assert.Equal(original.Market.DriftMultiplier, restored.Market.DriftMultiplier);
            Assert.Equal(original.Market.ShockOffset, restored.Market.ShockOffset);
            for (int i = 0; i < original.Herd.Count; i++)
            {
                Assert.Equal(original.Herd[i].WeightKg, restored.Herd[i].WeightKg);
            }
            Assert.Equal(original.Paddocks[0].GrassBiomass, restored.Paddocks[0].GrassBiomass);
            Assert.Equal(original.Paddocks[0].SoilHealth, restored.Paddocks[0].SoilHealth);
        }

        [Fact]
        public void SellingDown_ToNothing_GoesBrokeFromUpkeep()
        {
            // A rancher who sells everything still bleeds overhead: the go-broke
            // pressure exists even with zero head (design doc §4.3 — cash-flow
            // pressure is the point).
            var state = NewRanch(9);
            state.Cash = 100m;
            var engine = new SimulationEngine();

            var ids = new List<string>();
            foreach (Animal animal in state.Herd)
            {
                ids.Add(animal.Id);
            }
            foreach (string id in ids)
            {
                Economy.SellAnimal(state, MarketConfig.Default, id);
            }
            Assert.Empty(state.Herd);
            Assert.Empty(state.Paddocks[0].OccupantIds);
            Assert.True(state.Cash > 100m); // sales credited

            int guard = 0;
            while (!Economy.IsBroke(state) && guard < 5000)
            {
                engine.Step(state);
                guard++;
            }

            Assert.True(Economy.IsBroke(state), "overhead alone must eventually break an idle ranch");
        }
    }
}
