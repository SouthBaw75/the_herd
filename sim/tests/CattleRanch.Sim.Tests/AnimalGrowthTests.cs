using CattleRanch.Sim;
using CattleRanch.Sim.Systems;

namespace CattleRanch.Sim.Tests
{
    public class AnimalGrowthTests
    {
        private static Genome GenomeWithGrowth(float growthRate) =>
            new Genome(growthRate, 50f, 50f, 50f, 50f, 50f, 50f);

        private static Animal MakeAnimal(
            string id, Sex sex, int ageDays, double weightKg, float growthRate) =>
            new Animal(id, id, sex, "Angus", ageDays, weightKg, GenomeWithGrowth(growthRate));

        /// <summary>A 10 ha paddock at full biomass under default grazing tuning (30 000 kg DM).</summary>
        private static Paddock AbundantPaddock(string id)
        {
            double cap = GrazingConfig.Default.MaxCapacityPerHa * 10.0;
            return new Paddock(id, 10.0, cap, 1.0, cap);
        }

        /// <summary>A 10 ha paddock grazed down to the bare root residual — nothing edible left.</summary>
        private static Paddock ResidualPaddock(string id)
        {
            double residual = GrazingConfig.Default.ResidualBiomassPerHa * 10.0;
            return new Paddock(id, 10.0, residual, 1.0, GrazingConfig.Default.MaxCapacityPerHa * 10.0);
        }

        private static RanchState StateWith(Paddock? paddock, params Animal[] animals)
        {
            var state = new RanchState(1);
            foreach (Animal a in animals)
            {
                state.Herd.Add(a);
                paddock?.OccupantIds.Add(a.Id);
            }

            if (paddock != null)
            {
                state.Paddocks.Add(paddock);
            }

            return state;
        }

        [Fact]
        public void CalfOnAbundantGrass_GainsWeightEveryDay()
        {
            var calf = MakeAnimal("A-0", Sex.Female, 30, 45.0, 50f);
            var state = StateWith(AbundantPaddock("P"), calf);
            var growth = new AnimalGrowthSystem();

            double previous = calf.WeightKg;
            for (int day = 0; day < 60; day++)
            {
                growth.DailyUpdate(state);
                Assert.True(calf.WeightKg > previous,
                    $"calf should gain weight every day on abundant grass (day {day})");
                previous = calf.WeightKg;
            }

            // Roughly plausible daily gains, not a runaway balloon: ~1.5-1.8 kg/day here.
            Assert.InRange(calf.WeightKg, 45.0 + 60 * 0.5, 45.0 + 60 * 2.5);
        }

        [Fact]
        public void Aging_IncrementsExactlyOneDayPerUpdate()
        {
            var cow = MakeAnimal("A-0", Sex.Female, 730, 480.0, 50f);
            var unborn = MakeAnimal("A-1", Sex.Male, 0, 40.0, 50f);
            var state = StateWith(AbundantPaddock("P"), cow, unborn);
            var growth = new AnimalGrowthSystem();

            for (int day = 0; day < 137; day++)
            {
                growth.DailyUpdate(state);
            }

            Assert.Equal(730 + 137, cow.AgeDays);
            Assert.Equal(137, unborn.AgeDays);
        }

        [Fact]
        public void HigherGrowthRateTrait_EndsHeavier_Over200Days()
        {
            var lowTrait = MakeAnimal("A-0", Sex.Female, 30, 45.0, 30f);
            var highTrait = MakeAnimal("A-1", Sex.Female, 30, 45.0, 80f);
            // One big paddock; grass vastly exceeds the pair's demand, so both
            // grow at full forage satisfaction and only genetics differ.
            var state = StateWith(AbundantPaddock("P"), lowTrait, highTrait);
            var growth = new AnimalGrowthSystem();

            for (int day = 0; day < 200; day++)
            {
                growth.DailyUpdate(state);
            }

            Assert.True(highTrait.WeightKg > lowTrait.WeightKg,
                $"GrowthRate 80 calf ({highTrait.WeightKg:F1} kg) should out-gain GrowthRate 30 calf ({lowTrait.WeightKg:F1} kg)");
            Assert.True(lowTrait.WeightKg > 45.0, "even the low-trait calf gains on good grass");
            // Meaningfully different, not float-noise different.
            Assert.True(highTrait.WeightKg - lowTrait.WeightKg > 20.0);
        }

        [Fact]
        public void AnimalOnResidualGrassPaddock_LosesWeightEveryDay()
        {
            var steer = MakeAnimal("A-0", Sex.Male, 400, 400.0, 50f);
            var state = StateWith(ResidualPaddock("P"), steer);
            var growth = new AnimalGrowthSystem();

            double previous = steer.WeightKg;
            for (int day = 0; day < 30; day++)
            {
                growth.DailyUpdate(state);
                Assert.True(steer.WeightKg < previous,
                    $"no edible grass above the residual: weight must fall (day {day})");
                previous = steer.WeightKg;
            }
        }

        [Fact]
        public void AnimalInNoPaddock_LosesWeight_AndWeightNeverGoesNegative()
        {
            var stray = MakeAnimal("A-0", Sex.Female, 60, 3.0, 50f);
            var state = StateWith(paddock: null, stray);
            var growth = new AnimalGrowthSystem();

            for (int day = 0; day < 50; day++)
            {
                growth.DailyUpdate(state);
                Assert.True(stray.WeightKg >= 0.0, "weight must never go negative");
            }

            // 3 kg at 0.8 kg/day loss bottoms out on the floor well within 50 days.
            Assert.Equal(0.0, stray.WeightKg);
        }

        [Fact]
        public void Weight_ApproachesMatureAsymptote_WithoutOvershooting()
        {
            var cfg = AnimalGrowthConfig.Default;
            var cow = MakeAnimal("A-0", Sex.Female, 0, 40.0, 50f);
            var bull = MakeAnimal("A-1", Sex.Male, 0, 42.0, 50f);
            var state = StateWith(AbundantPaddock("P"), cow, bull);
            var growth = new AnimalGrowthSystem(cfg);

            double cowMature = cfg.MatureWeightKg(Sex.Female, 50f);   // 600 at GrowthRate 50
            double bullMature = cfg.MatureWeightKg(Sex.Male, 50f);    // 900 at GrowthRate 50
            Assert.Equal(600.0, cowMature, precision: 9);
            Assert.Equal(900.0, bullMature, precision: 9);

            for (int day = 0; day < 600; day++)
            {
                growth.DailyUpdate(state);
            }

            // A 600-day-old on good grass is near its cap — not 2x over it.
            Assert.InRange(cow.WeightKg, 0.75 * cowMature, cowMature);
            Assert.InRange(bull.WeightKg, 0.60 * bullMature, bullMature);

            // Much later the weight has converged onto the asymptote and stays
            // pinned there without ever exceeding it.
            for (int day = 0; day < 2400; day++)
            {
                growth.DailyUpdate(state);
                Assert.True(cow.WeightKg <= cowMature, "cow must never overshoot her mature weight");
                Assert.True(bull.WeightKg <= bullMature, "bull must never overshoot his mature weight");
            }

            Assert.InRange(cow.WeightKg, 0.98 * cowMature, cowMature);
            Assert.InRange(bull.WeightKg, 0.98 * bullMature, bullMature);
        }

        [Fact]
        public void WeatherStressMultiplier_SlowsWeightGain()
        {
            var comfortable = MakeAnimal("A-0", Sex.Female, 30, 45.0, 50f);
            var stateA = StateWith(AbundantPaddock("P"), comfortable);
            var stressed = MakeAnimal("A-0", Sex.Female, 30, 45.0, 50f);
            var stateB = StateWith(AbundantPaddock("P"), stressed);
            var growth = new AnimalGrowthSystem();

            for (int day = 0; day < 60; day++)
            {
                growth.DailyUpdate(stateA, animalStressMultiplier: 1.0);
                growth.DailyUpdate(stateB, animalStressMultiplier: 0.5); // blizzard-grade stress
            }

            Assert.True(stressed.WeightKg > 45.0, "stress slows gain on full forage; it does not starve");
            Assert.True(comfortable.WeightKg - stressed.WeightKg > 10.0,
                $"sustained stress must cost real weight (comfortable {comfortable.WeightKg:F1} vs stressed {stressed.WeightKg:F1})");
        }

        [Fact]
        public void FullRun_SameSeed_ProducesBitIdenticalHerd()
        {
            RanchState Build()
            {
                var state = new RanchState(424242L);
                var north = AbundantPaddock("north");
                var south = ResidualPaddock("south");
                state.Paddocks.Add(north);
                state.Paddocks.Add(south);

                var cow = MakeAnimal("A-0", Sex.Female, 900, 480.0, 61.5f);
                var calf = MakeAnimal("A-1", Sex.Female, 20, 42.0, 74.25f);
                var steer = MakeAnimal("A-2", Sex.Male, 500, 410.0, 33f);
                state.Herd.Add(cow);
                state.Herd.Add(calf);
                state.Herd.Add(steer);
                north.OccupantIds.Add(cow.Id);
                north.OccupantIds.Add(calf.Id);
                // The steer is deliberately unpastured (in no paddock): it stays
                // maintenance-short all run, while the empty south paddock
                // regrows from residual — both paths exercised.
                return state;
            }

            // Full Phase-1 daily loop, run twice from the same seed: date, then
            // weather, then growth (fed by weather), then grazing depleting the
            // paddocks so forage satisfaction genuinely varies over the run.
            void Run(RanchState state, int days)
            {
                var weather = new WeatherSystem();
                var growth = new AnimalGrowthSystem();
                var grazing = new GrazingSystem();
                for (int day = 0; day < days; day++)
                {
                    state.Date = state.Date.AddDays(1);
                    weather.DailyUpdate(state);
                    growth.DailyUpdate(state, weather.AnimalStressMultiplier(state));
                    for (int p = 0; p < state.Paddocks.Count; p++)
                    {
                        grazing.UpdateDaily(
                            state.Paddocks[p], state.Paddocks[p].OccupantIds.Count, state.Date.Season);
                    }
                }
            }

            RanchState a = Build();
            RanchState b = Build();
            Run(a, 200);
            Run(b, 200);

            Assert.Equal(a.Rng.State, b.Rng.State);
            Assert.Equal(a.Weather.Condition, b.Weather.Condition);
            Assert.Equal(a.Weather.DaysRemaining, b.Weather.DaysRemaining);
            for (int i = 0; i < a.Herd.Count; i++)
            {
                // Bit-identical doubles, not approximate equality.
                Assert.Equal(a.Herd[i].WeightKg, b.Herd[i].WeightKg);
                Assert.Equal(a.Herd[i].AgeDays, b.Herd[i].AgeDays);
            }

            // And the run did something: the well-fed calf grew, the unpastured
            // steer shrank, the rested south paddock regrew off its residual.
            Assert.True(a.Herd[1].WeightKg > 42.0);
            Assert.True(a.Herd[2].WeightKg < 410.0);
            Assert.True(a.Paddocks[1].GrassBiomass > GrazingConfig.Default.ResidualBiomassPerHa * 10.0);
        }
    }
}
