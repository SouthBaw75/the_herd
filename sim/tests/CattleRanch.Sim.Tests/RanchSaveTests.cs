using System;
using CattleRanch.Sim;
using CattleRanch.Sim.Persistence;
using CattleRanch.Sim.Systems;

namespace CattleRanch.Sim.Tests
{
    public class RanchSaveTests
    {
        /// <summary>
        /// Builds a ranch with deliberately non-default values in every
        /// internal-set field the defect zeroed out, then advances it 100 days so
        /// Date/GrassBiomass/SoilHealth carry simulated (non-initial) values.
        /// </summary>
        private static RanchState BuildAdvancedState(GrazingConfig cfg)
        {
            var state = new RanchState(987654321L);
            state.Cash = 12345.67m;

            var cow = new Animal(
                state.AllocateId("A"), "Bessie", Sex.Female, "Angus", 730, 481.5,
                new Genome(61.5f, 42.25f, 73.125f, 55.5f, 38.75f, 66.0f, 49.5f));
            cow.Health = HealthState.Parasites;
            cow.Pregnancy = new PregnancyState { IsPregnant = true, DaysPregnant = 41 };

            var bull = new Animal(
                state.AllocateId("A"), "Ferdinand", Sex.Male, "Hereford", 1460, 812.25,
                new Genome(70.5f, 30.25f, 80.125f, 10.5f, 45.75f, 58.0f, 62.5f));

            state.Herd.Add(cow);
            state.Herd.Add(bull);

            var paddock = new Paddock(
                "north", 4.0, cfg.MaxCapacityPerHa * 4.0, 0.9, cfg.MaxCapacityPerHa * 4.0 * 0.9);
            paddock.OccupantIds.Add(cow.Id);
            paddock.OccupantIds.Add(bull.Id);
            state.Paddocks.Add(paddock);

            new SimulationEngine(cfg).Advance(state, 100);
            return state;
        }

        [Fact]
        public void RoundTrip_RestoresEveryField()
        {
            var cfg = GrazingConfig.Default;
            var original = BuildAdvancedState(cfg);

            string json = RanchSave.ToJson(original);
            RanchState restored = RanchSave.FromJson(json);

            Assert.Equal(original.Seed, restored.Seed);
            Assert.Equal(original.Date.TotalDays, restored.Date.TotalDays);
            Assert.Equal(100, restored.Date.TotalDays); // proves it isn't a default 0
            Assert.Equal(original.NextEntityId, restored.NextEntityId);
            Assert.Equal(original.Cash, restored.Cash);

            Assert.Equal(original.Herd.Count, restored.Herd.Count);
            for (int i = 0; i < original.Herd.Count; i++)
            {
                Animal a = original.Herd[i];
                Animal r = restored.Herd[i];
                Assert.Equal(a.Id, r.Id);
                Assert.Equal(a.Name, r.Name);
                Assert.Equal(a.Sex, r.Sex);
                Assert.Equal(a.Breed, r.Breed);
                Assert.Equal(a.AgeDays, r.AgeDays);
                Assert.Equal(a.WeightKg, r.WeightKg);
                Assert.Equal(a.Genome.GrowthRate, r.Genome.GrowthRate);
                Assert.Equal(a.Genome.CalvingEase, r.Genome.CalvingEase);
                Assert.Equal(a.Genome.Marbling, r.Genome.Marbling);
                Assert.Equal(a.Genome.Mothering, r.Genome.Mothering);
                Assert.Equal(a.Genome.HeatTolerance, r.Genome.HeatTolerance);
                Assert.Equal(a.Genome.DiseaseResistance, r.Genome.DiseaseResistance);
                Assert.Equal(a.Genome.Fertility, r.Genome.Fertility);
                Assert.Equal(a.Health, r.Health);
                Assert.Equal(a.Pregnancy.IsPregnant, r.Pregnancy.IsPregnant);
                Assert.Equal(a.Pregnancy.DaysPregnant, r.Pregnancy.DaysPregnant);
            }

            Assert.Equal(original.Paddocks.Count, restored.Paddocks.Count);
            for (int i = 0; i < original.Paddocks.Count; i++)
            {
                Paddock p = original.Paddocks[i];
                Paddock r = restored.Paddocks[i];
                Assert.Equal(p.Id, r.Id);
                Assert.Equal(p.AreaHectares, r.AreaHectares);
                Assert.Equal(p.GrassBiomass, r.GrassBiomass);
                Assert.Equal(p.SoilHealth, r.SoilHealth);
                Assert.Equal(p.CarryingCapacity, r.CarryingCapacity);
                Assert.Equal(p.OccupantIds, r.OccupantIds);
            }
        }

        [Fact]
        public void RoundTrip_RestoredState_SimulatesIdenticallyToOriginal()
        {
            var cfg = GrazingConfig.Default;
            var original = BuildAdvancedState(cfg);

            RanchState restored = RanchSave.FromJson(RanchSave.ToJson(original));

            // Post-restore determinism: fresh engines, same further trajectory.
            new SimulationEngine(cfg).Advance(original, 100);
            new SimulationEngine(cfg).Advance(restored, 100);

            Assert.Equal(original.Date.TotalDays, restored.Date.TotalDays);
            Assert.Equal(original.Paddocks.Count, restored.Paddocks.Count);
            for (int i = 0; i < original.Paddocks.Count; i++)
            {
                // Bit-identical doubles, not approximate equality.
                Assert.Equal(original.Paddocks[i].GrassBiomass, restored.Paddocks[i].GrassBiomass);
                Assert.Equal(original.Paddocks[i].SoilHealth, restored.Paddocks[i].SoilHealth);
                Assert.Equal(original.Paddocks[i].CarryingCapacity, restored.Paddocks[i].CarryingCapacity);
            }
        }

        [Fact]
        public void FromJson_Garbage_ThrowsClearException()
        {
            var ex = Assert.Throws<FormatException>(() => RanchSave.FromJson("not json at all {{{"));
            Assert.Contains("RanchState", ex.Message);
        }

        [Fact]
        public void FromJson_NullLiteralJson_Throws()
        {
            var ex = Assert.Throws<FormatException>(() => RanchSave.FromJson("null"));
            Assert.Contains("RanchState", ex.Message);
        }

        [Fact]
        public void FromJson_EmptyOrNullInput_Throws()
        {
            Assert.Throws<ArgumentException>(() => RanchSave.FromJson(""));
            Assert.Throws<ArgumentException>(() => RanchSave.FromJson("   "));
            Assert.Throws<ArgumentException>(() => RanchSave.FromJson(null!));
        }

        [Fact]
        public void ToJson_NullState_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => RanchSave.ToJson(null!));
        }
    }
}
