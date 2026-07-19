using System.Collections.Generic;
using CattleRanch.Sim;
using CattleRanch.Sim.Persistence;
using CattleRanch.Sim.Systems;

namespace CattleRanch.Sim.Tests
{
    public class BreedingTests
    {
        // Stepping from day 55 lands the first step on TotalDays 56 = Fall day 1;
        // 28 steps cover exactly the whole fall.
        private const int FallEveTotalDays = 55;

        // Stepping from day 83 (Fall day 28) lands the first step on Winter day 1.
        private const int FallLastDayTotalDays = 83;

        private static readonly Genome MidGenome = new Genome(50f, 50f, 50f, 50f, 50f, 50f, 50f);

        /// <summary>Advances the date one day then updates breeding (mirrors the engine's date-first order).</summary>
        private static BreedingReport StepBreeding(RanchState state, BreedingSystem system)
        {
            state.Date = state.Date.AddDays(1);
            return system.DailyUpdate(state);
        }

        private static Animal MakeCow(RanchState state, string name, Genome genome, int ageDays = 500)
        {
            var cow = new Animal(state.AllocateId("A"), name, Sex.Female, "Angus", ageDays, 500.0, genome);
            state.Herd.Add(cow);
            return cow;
        }

        private static Animal MakeBull(RanchState state, string name, Genome genome, int ageDays = 500)
        {
            var bull = new Animal(state.AllocateId("A"), name, Sex.Male, "Angus", ageDays, 800.0, genome);
            state.Herd.Add(bull);
            return bull;
        }

        private static Paddock AddPaddock(RanchState state, params Animal[] occupants)
        {
            var paddock = new Paddock("p" + state.Paddocks.Count, 4.0, 10_000.0, 1.0, 12_000.0);
            foreach (Animal animal in occupants)
            {
                paddock.OccupantIds.Add(animal.Id);
            }

            state.Paddocks.Add(paddock);
            return paddock;
        }

        /// <summary>A config with every stochastic knob zeroed (exact-blend calves).</summary>
        private static BreedingConfig ExactConfig()
        {
            var cfg = BreedingConfig.Default;
            cfg.TraitBlendStdDev = 0.0;
            cfg.HybridVigorChance = 0.0;
            cfg.DudChance = 0.0;
            cfg.NewbornWeightStdDevKg = 0.0;
            return cfg;
        }

        private static void AssertTraitsEqual(Genome expected, Genome actual)
        {
            Assert.Equal(expected.GrowthRate, actual.GrowthRate);
            Assert.Equal(expected.CalvingEase, actual.CalvingEase);
            Assert.Equal(expected.Marbling, actual.Marbling);
            Assert.Equal(expected.Mothering, actual.Mothering);
            Assert.Equal(expected.HeatTolerance, actual.HeatTolerance);
            Assert.Equal(expected.DiseaseResistance, actual.DiseaseResistance);
            Assert.Equal(expected.Fertility, actual.Fertility);
        }

        private static float[] Traits(Genome g) => new[]
        {
            g.GrowthRate, g.CalvingEase, g.Marbling, g.Mothering,
            g.HeatTolerance, g.DiseaseResistance, g.Fertility,
        };

        // ---- Conception ------------------------------------------------------

        [Fact]
        public void Fall_CowAndBullSharingPaddock_ConceivesBeforeWinter()
        {
            var state = new RanchState(42);
            state.Date = new GameDate(FallEveTotalDays);
            Animal cow = MakeCow(state, "Bessie", MidGenome);
            Animal bull = MakeBull(state, "Ferdinand", new Genome(70f, 60f, 55f, 45f, 40f, 65f, 80f));
            AddPaddock(state, cow, bull);

            var system = new BreedingSystem();
            for (int day = 0; day < GameDate.DaysPerSeason; day++)
            {
                StepBreeding(state, system);
            }

            Assert.Equal(Season.Fall, state.Date.Season); // still fall on day 28
            Assert.True(cow.Pregnancy.IsPregnant, "Cow with a bull all fall did not conceive (seed 42).");
            // The pregnancy carries the SIRE's genome so the calf can blend both
            // parents even if the bull is sold before calving.
            AssertTraitsEqual(bull.Genome, cow.Pregnancy.SireGenome);
        }

        [Fact]
        public void OutsideBreedingSeason_NeverConceives()
        {
            var state = new RanchState(42);
            Animal cow = MakeCow(state, "Bessie", MidGenome);
            Animal bull = MakeBull(state, "Ferdinand", MidGenome);
            AddPaddock(state, cow, bull);

            var system = new BreedingSystem();

            // All of spring and summer (days 1..55)...
            for (int day = 0; day < 55; day++)
            {
                StepBreeding(state, system);
                Assert.False(cow.Pregnancy.IsPregnant);
            }

            // ...and all of winter (days 84..111).
            state.Date = new GameDate(FallLastDayTotalDays);
            for (int day = 0; day < GameDate.DaysPerSeason; day++)
            {
                StepBreeding(state, system);
                Assert.Equal(Season.Winter, state.Date.Season);
                Assert.False(cow.Pregnancy.IsPregnant);
            }
        }

        [Fact]
        public void NoBullInHerPaddock_NeverConceives()
        {
            var state = new RanchState(42);
            state.Date = new GameDate(FallEveTotalDays);
            Animal cow = MakeCow(state, "Bessie", MidGenome);
            Animal bull = MakeBull(state, "Ferdinand", MidGenome);
            AddPaddock(state, cow);   // cow alone in the north paddock
            AddPaddock(state, bull);  // bull fenced off in the south paddock

            var system = new BreedingSystem();
            for (int day = 0; day < GameDate.DaysPerSeason; day++)
            {
                StepBreeding(state, system);
                Assert.False(cow.Pregnancy.IsPregnant);
            }
        }

        [Fact]
        public void TooYoungHeifer_NeverConceives()
        {
            var cfg = BreedingConfig.Default;
            var state = new RanchState(42);
            state.Date = new GameDate(FallEveTotalDays);
            Animal heifer = MakeCow(state, "Junior", MidGenome, ageDays: cfg.MinBreedingAgeDays - 1);
            Animal bull = MakeBull(state, "Ferdinand", MidGenome);
            AddPaddock(state, heifer, bull);

            var system = new BreedingSystem(cfg);
            for (int day = 0; day < GameDate.DaysPerSeason; day++)
            {
                // Breeding does not age animals, so she stays under-age all fall.
                StepBreeding(state, system);
                Assert.False(heifer.Pregnancy.IsPregnant);
            }
        }

        // ---- Gestation & birth -----------------------------------------------

        [Fact]
        public void PregnantCow_CalvesExactlyOnceAtTerm()
        {
            var cfg = BreedingConfig.Default;
            var state = new RanchState(7);
            state.Date = new GameDate(FallLastDayTotalDays); // start stepping into winter
            Animal dam = MakeCow(state, "Bessie", MidGenome);
            var sireGenome = new Genome(50f, 50f, 50f, 50f, 50f, 50f, 50f);
            dam.Pregnancy = new PregnancyState
            {
                IsPregnant = true,
                DaysPregnant = 0,
                SireGenome = sireGenome,
            };
            Paddock paddock = AddPaddock(state, dam);

            var system = new BreedingSystem(cfg);
            var born = new List<Animal>();

            for (int day = 1; day <= cfg.GestationDays; day++)
            {
                BreedingReport report = StepBreeding(state, system);
                born.AddRange(report.BornToday);

                if (day < cfg.GestationDays)
                {
                    Assert.Empty(born);
                    Assert.True(dam.Pregnancy.IsPregnant);
                    Assert.Equal(day, dam.Pregnancy.DaysPregnant);
                }
            }

            // Exactly one calf, on day 87 and no earlier.
            Animal calf = Assert.Single(born);
            Assert.Equal(2, state.Herd.Count);
            Assert.Contains(calf.Id, paddock.OccupantIds);
            Assert.False(dam.Pregnancy.IsPregnant); // dam is open again
            Assert.Equal(0, dam.Pregnancy.DaysPregnant);

            Assert.Equal(0, calf.AgeDays);
            Assert.Equal(dam.Breed, calf.Breed);
            Assert.Equal(string.Empty, calf.Name);
            Assert.True(calf.WeightKg >= cfg.NewbornWeightFloorKg);

            // Traits clamped to scale and within a plausible band of the
            // parents' mean (both parents 50): 4 sigma of blend variance plus
            // the possible vigor/dud shift.
            double band = 4.0 * cfg.TraitBlendStdDev + cfg.HybridVigorBoost;
            foreach (float trait in Traits(calf.Genome))
            {
                Assert.InRange(trait, 0f, 100f);
                Assert.InRange(trait, 50.0 - band, 50.0 + band);
            }
        }

        [Fact]
        public void DamInNoPaddock_CalfIsBornInNoPaddock()
        {
            var cfg = BreedingConfig.Default;
            var state = new RanchState(7);
            state.Date = new GameDate(FallLastDayTotalDays);
            Animal dam = MakeCow(state, "Stray", MidGenome);
            dam.Pregnancy = new PregnancyState
            {
                IsPregnant = true,
                DaysPregnant = cfg.GestationDays - 1,
                SireGenome = MidGenome,
            };
            Paddock unrelated = AddPaddock(state); // exists, but the dam is not in it

            BreedingReport report = StepBreeding(state, new BreedingSystem(cfg));

            Animal calf = Assert.Single(report.BornToday);
            Assert.Contains(calf, state.Herd);
            Assert.Empty(unrelated.OccupantIds);
        }

        // ---- Trait blending --------------------------------------------------

        [Fact]
        public void WithAllVarianceZeroed_CalfTraitsAreExactParentMeans()
        {
            var cfg = ExactConfig();
            var state = new RanchState(11);
            state.Date = new GameDate(FallLastDayTotalDays);
            var damGenome = new Genome(60f, 40f, 80f, 20f, 55f, 70f, 45f);
            var sireGenome = new Genome(30f, 90f, 10f, 60f, 45f, 50f, 85f);
            Animal dam = MakeCow(state, "Bessie", damGenome);
            dam.Pregnancy = new PregnancyState
            {
                IsPregnant = true,
                DaysPregnant = cfg.GestationDays - 1,
                SireGenome = sireGenome,
            };
            AddPaddock(state, dam);

            BreedingReport report = StepBreeding(state, new BreedingSystem(cfg));

            Animal calf = Assert.Single(report.BornToday);
            var expected = new Genome(45f, 65f, 45f, 40f, 50f, 60f, 65f);
            AssertTraitsEqual(expected, calf.Genome);
            Assert.Equal(cfg.NewbornWeightKg, calf.WeightKg); // stddev 0 → exact mean
        }

        [Fact]
        public void ForcedHybridVigor_BoostsAllTraits_CappedAt100()
        {
            var cfg = ExactConfig();
            cfg.HybridVigorChance = 1.0;
            var state = new RanchState(11);
            state.Date = new GameDate(FallLastDayTotalDays);
            // GrowthRate means 98 → capped at 100; the rest boost by +8.
            var damGenome = new Genome(100f, 40f, 80f, 20f, 55f, 70f, 45f);
            var sireGenome = new Genome(96f, 90f, 10f, 60f, 45f, 50f, 85f);
            Animal dam = MakeCow(state, "Bessie", damGenome);
            dam.Pregnancy = new PregnancyState
            {
                IsPregnant = true,
                DaysPregnant = cfg.GestationDays - 1,
                SireGenome = sireGenome,
            };
            AddPaddock(state, dam);

            BreedingReport report = StepBreeding(state, new BreedingSystem(cfg));

            Animal calf = Assert.Single(report.BornToday);
            var expected = new Genome(100f, 73f, 53f, 48f, 58f, 68f, 73f);
            AssertTraitsEqual(expected, calf.Genome);
        }

        [Fact]
        public void ForcedDud_PenalizesAllTraits_FlooredAt0()
        {
            var cfg = ExactConfig();
            cfg.DudChance = 1.0;
            var state = new RanchState(11);
            state.Date = new GameDate(FallLastDayTotalDays);
            // Mothering means 4 → floored at 0; the rest lose 8.
            var damGenome = new Genome(60f, 40f, 80f, 4f, 55f, 70f, 45f);
            var sireGenome = new Genome(30f, 90f, 10f, 4f, 45f, 50f, 85f);
            Animal dam = MakeCow(state, "Bessie", damGenome);
            dam.Pregnancy = new PregnancyState
            {
                IsPregnant = true,
                DaysPregnant = cfg.GestationDays - 1,
                SireGenome = sireGenome,
            };
            AddPaddock(state, dam);

            BreedingReport report = StepBreeding(state, new BreedingSystem(cfg));

            Animal calf = Assert.Single(report.BornToday);
            var expected = new Genome(37f, 57f, 37f, 0f, 42f, 52f, 57f);
            AssertTraitsEqual(expected, calf.Genome);
        }

        // ---- Determinism -----------------------------------------------------

        private static RanchState MakeBreedingScenario(long seed)
        {
            var state = new RanchState(seed);
            state.Date = new GameDate(FallEveTotalDays);
            Animal cowA = MakeCow(state, "Bessie", new Genome(60f, 40f, 80f, 20f, 55f, 70f, 45f));
            Animal cowB = MakeCow(state, "Clover", new Genome(35f, 65f, 45f, 75f, 60f, 40f, 90f));
            Animal bull = MakeBull(state, "Ferdinand", new Genome(70f, 60f, 55f, 45f, 40f, 65f, 80f));
            AddPaddock(state, cowA, cowB, bull);
            return state;
        }

        private static void AssertHerdsIdentical(RanchState a, RanchState b)
        {
            Assert.Equal(a.Herd.Count, b.Herd.Count);
            for (int i = 0; i < a.Herd.Count; i++)
            {
                Assert.Equal(a.Herd[i].Id, b.Herd[i].Id);
                Assert.Equal(a.Herd[i].Sex, b.Herd[i].Sex);
                Assert.Equal(a.Herd[i].Breed, b.Herd[i].Breed);
                Assert.Equal(a.Herd[i].WeightKg, b.Herd[i].WeightKg); // bit-identical
                AssertTraitsEqual(a.Herd[i].Genome, b.Herd[i].Genome);
            }
        }

        [Fact]
        public void SameSeed_ProducesIdenticalCalvingOutcomes()
        {
            RanchState a = MakeBreedingScenario(20260719);
            RanchState b = MakeBreedingScenario(20260719);
            var systemA = new BreedingSystem();
            var systemB = new BreedingSystem();

            // Fall conception window plus a full gestation: every conception
            // (latest possible: fall day 28) has calved within 28 + 87 days.
            for (int day = 0; day < 28 + BreedingConfig.Default.GestationDays; day++)
            {
                BreedingReport reportA = StepBreeding(a, systemA);
                BreedingReport reportB = StepBreeding(b, systemB);
                Assert.Equal(reportA.Conceptions.Count, reportB.Conceptions.Count);
                Assert.Equal(reportA.BornToday.Count, reportB.BornToday.Count);
            }

            Assert.True(a.Herd.Count > 3, "Expected at least one calf from two cows bred all fall (seed 20260719).");
            AssertHerdsIdentical(a, b);
            Assert.Equal(a.Rng.State, b.Rng.State);
        }

        [Fact]
        public void SaveRoundTrip_MidPregnancy_ContinuesIdentically()
        {
            RanchState original = MakeBreedingScenario(42);
            var system = new BreedingSystem();

            // Breed through fall, then part-way into gestation.
            for (int day = 0; day < 40; day++)
            {
                StepBreeding(original, system);
            }

            Assert.Contains(original.Herd, x => x.Pregnancy.IsPregnant); // mid-pregnancy save point

            RanchState restored = RanchSave.FromJson(RanchSave.ToJson(original));

            // The pregnancy — including the sire's genome — survives the save.
            for (int i = 0; i < original.Herd.Count; i++)
            {
                Assert.Equal(original.Herd[i].Pregnancy.IsPregnant, restored.Herd[i].Pregnancy.IsPregnant);
                Assert.Equal(original.Herd[i].Pregnancy.DaysPregnant, restored.Herd[i].Pregnancy.DaysPregnant);
                AssertTraitsEqual(original.Herd[i].Pregnancy.SireGenome, restored.Herd[i].Pregnancy.SireGenome);
            }

            // Both continue through calving bit-identically (fresh system for
            // the restored run — all state lives in RanchState).
            var restoredSystem = new BreedingSystem();
            for (int day = 0; day < BreedingConfig.Default.GestationDays; day++)
            {
                StepBreeding(original, system);
                StepBreeding(restored, restoredSystem);
            }

            Assert.True(original.Herd.Count > 3, "Expected at least one calf by now (seed 42).");
            AssertHerdsIdentical(original, restored);
            Assert.Equal(original.Rng.State, restored.Rng.State);
            Assert.Equal(original.NextEntityId, restored.NextEntityId);
        }
    }
}
