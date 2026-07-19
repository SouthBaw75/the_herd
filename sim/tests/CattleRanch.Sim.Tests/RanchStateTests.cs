using System.Collections.Generic;
using CattleRanch.Sim;
using CattleRanch.Sim.Systems;

namespace CattleRanch.Sim.Tests
{
    public class RanchStateTests
    {
        private static RanchState NewRanch(long seed, GrazingConfig cfg)
        {
            var state = new RanchState(seed);

            // A small paddock stocked with a herd via occupant ids. 4 ha carrying
            // ~12000 kg vs 120 head (1440 kg/day intake) is a genuine overstock:
            // intake exceeds the ~1200 kg/day peak regrowth, so it degrades.
            var paddock = new Paddock("north", 4.0, cfg.MaxCapacityPerHa * 4.0, 1.0, cfg.MaxCapacityPerHa * 4.0);
            for (int i = 0; i < 120; i++)
            {
                string id = state.AllocateId("A");
                paddock.OccupantIds.Add(id);
                state.Herd.Add(new Animal(id, $"cow{i}", Sex.Female, "Angus", 730, 480.0,
                    new Genome(50, 50, 50, 50, 50, 50, 50)));
            }
            state.Paddocks.Add(paddock);

            // A second, rested paddock to exercise recovery in the same run.
            state.Paddocks.Add(new Paddock("south", 4.0, 2000.0, 0.8, cfg.MaxCapacityPerHa * 4.0 * 0.8));

            return state;
        }

        [Fact]
        public void FullRun_SameSeed_YieldsIdenticalBiomassAndSoil()
        {
            var cfg = GrazingConfig.Default;
            var engine = new SimulationEngine(cfg);

            var a = NewRanch(12345, cfg);
            var b = NewRanch(12345, cfg);

            engine.Advance(a, 300);
            engine.Advance(b, 300);

            Assert.Equal(a.Date.TotalDays, b.Date.TotalDays);
            Assert.Equal(a.Paddocks.Count, b.Paddocks.Count);
            for (int i = 0; i < a.Paddocks.Count; i++)
            {
                // Deterministic arithmetic => bit-for-bit identical doubles.
                Assert.Equal(a.Paddocks[i].GrassBiomass, b.Paddocks[i].GrassBiomass);
                Assert.Equal(a.Paddocks[i].SoilHealth, b.Paddocks[i].SoilHealth);
                Assert.Equal(a.Paddocks[i].CarryingCapacity, b.Paddocks[i].CarryingCapacity);
            }
        }

        [Fact]
        public void FullRun_AdvanceEqualsRepeatedStep()
        {
            var cfg = GrazingConfig.Default;
            var engine = new SimulationEngine(cfg);

            var bulk = NewRanch(7, cfg);
            var stepped = NewRanch(7, cfg);

            engine.Advance(bulk, 200);
            for (int i = 0; i < 200; i++)
            {
                engine.Step(stepped);
            }

            for (int i = 0; i < bulk.Paddocks.Count; i++)
            {
                Assert.Equal(bulk.Paddocks[i].GrassBiomass, stepped.Paddocks[i].GrassBiomass);
                Assert.Equal(bulk.Paddocks[i].SoilHealth, stepped.Paddocks[i].SoilHealth);
            }
            Assert.Equal(bulk.Date, stepped.Date);
        }

        [Fact]
        public void OverstockedPaddock_InFullRun_LosesSoil_WhileRestedOneHolds()
        {
            var cfg = GrazingConfig.Default;
            var engine = new SimulationEngine(cfg);
            var state = NewRanch(1, cfg);

            double northSoil0 = state.Paddocks[0].SoilHealth; // stocked with 120 head on 8 ha
            double southSoil0 = state.Paddocks[1].SoilHealth; // no occupants

            engine.Advance(state, 200);

            // The heavily stocked paddock degrades; the empty one does not lose soil.
            Assert.True(state.Paddocks[0].SoilHealth < northSoil0,
                "overstocked paddock should lose soil health over a long run");
            Assert.True(state.Paddocks[1].SoilHealth >= southSoil0,
                "rested paddock should not lose soil health");
        }

        [Fact]
        public void AllocateId_IsDeterministicAndSequential()
        {
            var state = new RanchState(0);
            Assert.Equal("A-0", state.AllocateId("A"));
            Assert.Equal("A-1", state.AllocateId("A"));
            Assert.Equal("B-2", state.AllocateId("B"));
            Assert.Equal(3, state.NextEntityId);
        }

        [Fact]
        public void NewRanchState_HasSaneDefaults()
        {
            var state = new RanchState(555);
            Assert.Equal(555, state.Seed);
            Assert.Equal(GameDate.Start, state.Date);
            Assert.Equal(0m, state.Cash);
            Assert.NotNull(state.Herd);
            Assert.NotNull(state.Paddocks);
            Assert.Empty(state.Herd);
            Assert.Empty(state.Paddocks);
        }
    }
}
