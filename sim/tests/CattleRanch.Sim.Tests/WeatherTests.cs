using System.Collections.Generic;
using CattleRanch.Sim;
using CattleRanch.Sim.Persistence;
using CattleRanch.Sim.Systems;

namespace CattleRanch.Sim.Tests
{
    public class WeatherTests
    {
        /// <summary>Advances the calendar one day, then the weather — the engine's order.</summary>
        private static void StepWeatherDay(RanchState state, WeatherSystem weather)
        {
            state.Date = state.Date.AddDays(1);
            weather.DailyUpdate(state);
        }

        /// <summary>A config where only one condition can occur, with certainty, every season.</summary>
        private static WeatherConfig CertainSpellConfig(
            WeatherCondition condition, int minDays, int maxDays)
        {
            var cfg = new WeatherConfig
            {
                DroughtDailyChance = new[] { 0.0, 0.0, 0.0, 0.0 },
                HeatWaveDailyChance = new[] { 0.0, 0.0, 0.0, 0.0 },
                BlizzardDailyChance = new[] { 0.0, 0.0, 0.0, 0.0 },
                WetSpringDailyChance = new[] { 0.0, 0.0, 0.0, 0.0 },
            };
            double[] certain = { 1.0, 1.0, 1.0, 1.0 };
            switch (condition)
            {
                case WeatherCondition.Drought:
                    cfg.DroughtDailyChance = certain;
                    cfg.DroughtMinDays = minDays;
                    cfg.DroughtMaxDays = maxDays;
                    break;
                case WeatherCondition.HeatWave:
                    cfg.HeatWaveDailyChance = certain;
                    cfg.HeatWaveMinDays = minDays;
                    cfg.HeatWaveMaxDays = maxDays;
                    break;
                case WeatherCondition.Blizzard:
                    cfg.BlizzardDailyChance = certain;
                    cfg.BlizzardMinDays = minDays;
                    cfg.BlizzardMaxDays = maxDays;
                    break;
                case WeatherCondition.WetSpring:
                    cfg.WetSpringDailyChance = certain;
                    cfg.WetSpringMinDays = minDays;
                    cfg.WetSpringMaxDays = maxDays;
                    break;
            }

            return cfg;
        }

        [Fact]
        public void SameSeed_IdenticalWeatherSequence_Over500Days()
        {
            var a = new RanchState(20260719L);
            var b = new RanchState(20260719L);
            var weather = new WeatherSystem(WeatherConfig.Default);

            int spellDays = 0;
            for (int day = 0; day < 500; day++)
            {
                StepWeatherDay(a, weather);
                StepWeatherDay(b, weather);

                Assert.Equal(a.Weather.Condition, b.Weather.Condition);
                Assert.Equal(a.Weather.DaysRemaining, b.Weather.DaysRemaining);
                Assert.Equal(a.Rng.State, b.Rng.State);

                if (a.Weather.Condition != WeatherCondition.Normal)
                {
                    spellDays++;
                }
            }

            // Sanity: the default config actually produces weather over ~4.5
            // years — the determinism assertion above must not be vacuous.
            Assert.True(spellDays > 0, "expected at least one adverse spell in 500 days");
            Assert.True(spellDays < 500, "weather must be mostly Normal, not a permanent spell");
        }

        [Fact]
        public void CertainDroughtConfig_StartsDrought_WithConfiguredDuration()
        {
            var cfg = CertainSpellConfig(WeatherCondition.Drought, minDays: 6, maxDays: 6);
            var weather = new WeatherSystem(cfg);
            var state = new RanchState(42);

            StepWeatherDay(state, weather);

            Assert.Equal(WeatherCondition.Drought, state.Weather.Condition);
            Assert.Equal(6, state.Weather.DaysRemaining);

            // The spell holds for exactly its 6 days (no RNG consumed while it runs)...
            ulong rngStateDuringSpell = state.Rng.State;
            for (int day = 2; day <= 6; day++)
            {
                StepWeatherDay(state, weather);
                Assert.Equal(WeatherCondition.Drought, state.Weather.Condition);
            }

            Assert.Equal(rngStateDuringSpell, state.Rng.State);

            // ...then breaks to exactly one Normal day before the next roll.
            StepWeatherDay(state, weather);
            Assert.Equal(WeatherCondition.Normal, state.Weather.Condition);
            Assert.Equal(0, state.Weather.DaysRemaining);
        }

        [Fact]
        public void CertainBlizzardConfig_ProducesBlizzard_InAnySeason()
        {
            // Chance arrays are per-season; with certainty in all four the spell
            // kind must come back as configured regardless of the calendar.
            var cfg = CertainSpellConfig(WeatherCondition.Blizzard, 3, 3);
            var weather = new WeatherSystem(cfg);
            var state = new RanchState(7);
            state.Date = new GameDate(3 * GameDate.DaysPerSeason - 1); // last Fall day; step lands on Winter day 1

            StepWeatherDay(state, weather);

            Assert.Equal(Season.Winter, state.Date.Season);
            Assert.Equal(WeatherCondition.Blizzard, state.Weather.Condition);
            Assert.Equal(3, state.Weather.DaysRemaining);
        }

        [Fact]
        public void ZeroChanceConfig_StaysNormalForever()
        {
            var cfg = new WeatherConfig
            {
                DroughtDailyChance = new[] { 0.0, 0.0, 0.0, 0.0 },
                HeatWaveDailyChance = new[] { 0.0, 0.0, 0.0, 0.0 },
                BlizzardDailyChance = new[] { 0.0, 0.0, 0.0, 0.0 },
                WetSpringDailyChance = new[] { 0.0, 0.0, 0.0, 0.0 },
            };
            var weather = new WeatherSystem(cfg);
            var state = new RanchState(1);

            for (int day = 0; day < 3 * GameDate.DaysPerYear; day++)
            {
                StepWeatherDay(state, weather);
                Assert.Equal(WeatherCondition.Normal, state.Weather.Condition);
                Assert.Equal(0, state.Weather.DaysRemaining);
            }
        }

        [Fact]
        public void Multipliers_ComeBackPerConfig_ForEveryCondition()
        {
            var cfg = new WeatherConfig
            {
                GrassGrowthMultipliers = new[] { 1.0, 0.25, 0.55, 0.05, 1.4 },
                AnimalStressMultipliers = new[] { 1.0, 0.8, 0.65, 0.45, 0.95 },
            };
            var weather = new WeatherSystem(cfg);
            var state = new RanchState(1);

            var conditions = new[]
            {
                WeatherCondition.Normal, WeatherCondition.Drought, WeatherCondition.HeatWave,
                WeatherCondition.Blizzard, WeatherCondition.WetSpring,
            };
            foreach (WeatherCondition condition in conditions)
            {
                state.Weather.Condition = condition;
                Assert.Equal(
                    cfg.GrassGrowthMultipliers[(int)condition],
                    weather.GrassGrowthMultiplier(state));
                Assert.Equal(
                    cfg.AnimalStressMultipliers[(int)condition],
                    weather.AnimalStressMultiplier(state));
            }
        }

        [Fact]
        public void SaveLoad_MidSpell_ContinuesIdentically()
        {
            // A duration RANGE (not min==max) so the duration int draw is part
            // of the stream the save must preserve.
            var cfg = CertainSpellConfig(WeatherCondition.Drought, minDays: 10, maxDays: 24);
            var weather = new WeatherSystem(cfg);
            var original = new RanchState(555);

            // Enter the spell and burn partway through it.
            for (int day = 0; day < 4; day++)
            {
                StepWeatherDay(original, weather);
            }

            Assert.Equal(WeatherCondition.Drought, original.Weather.Condition); // genuinely mid-spell
            Assert.True(original.Weather.DaysRemaining > 0);

            RanchState restored = RanchSave.FromJson(RanchSave.ToJson(original));

            // Weather state round-trips (non-default values, so this proves the
            // POCO actually serializes rather than falling back to defaults).
            Assert.Equal(original.Weather.Condition, restored.Weather.Condition);
            Assert.Equal(original.Weather.DaysRemaining, restored.Weather.DaysRemaining);

            // Post-load, both runs produce the identical day-by-day sequence,
            // across the spell's end, the Normal break, and the next spell's
            // fresh RNG draws.
            for (int day = 0; day < 60; day++)
            {
                StepWeatherDay(original, weather);
                StepWeatherDay(restored, weather);

                Assert.Equal(original.Weather.Condition, restored.Weather.Condition);
                Assert.Equal(original.Weather.DaysRemaining, restored.Weather.DaysRemaining);
                Assert.Equal(original.Rng.State, restored.Rng.State);
            }
        }

        [Fact]
        public void NewState_StartsWithNormalWeather()
        {
            var state = new RanchState(9);
            Assert.NotNull(state.Weather);
            Assert.Equal(WeatherCondition.Normal, state.Weather.Condition);
            Assert.Equal(0, state.Weather.DaysRemaining);
        }
    }
}
