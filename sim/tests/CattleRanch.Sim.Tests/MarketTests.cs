using System;
using System.Collections.Generic;
using CattleRanch.Sim;
using CattleRanch.Sim.Persistence;
using CattleRanch.Sim.Systems;

namespace CattleRanch.Sim.Tests
{
    public class MarketTests
    {
        /// <summary>A config with drift and shocks fully disabled (pure seasonal baseline).</summary>
        private static MarketConfig QuietConfig()
        {
            var cfg = MarketConfig.Default;
            cfg.DriftStdDev = 0.0;
            cfg.ShockChancePerDay = 0.0;
            return cfg;
        }

        /// <summary>Advances date by one day then updates the market (the engine's order).</summary>
        private static void StepMarket(RanchState state, MarketConfig cfg)
        {
            state.Date = state.Date.AddDays(1);
            state.Market.DailyUpdate(state, cfg);
        }

        private static Animal MakeAnimal(RanchState state, string name, double weightKg)
        {
            return new Animal(
                state.AllocateId("A"), name, Sex.Female, "Angus", 730, weightKg,
                new Genome(50f, 50f, 50f, 50f, 50f, 50f, 50f));
        }

        // ---- Determinism -----------------------------------------------------

        [Fact]
        public void SameSeed_ProducesIdenticalPriceSeries()
        {
            var cfg = MarketConfig.Default;
            var a = new RanchState(42);
            var b = new RanchState(42);

            for (int day = 0; day < 300; day++)
            {
                StepMarket(a, cfg);
                StepMarket(b, cfg);
                // Bit-identical, not approximate.
                Assert.Equal(
                    a.Market.PricePerKg(cfg, a.Date.Season),
                    b.Market.PricePerKg(cfg, b.Date.Season));
            }

            Assert.Equal(a.Market.DriftMultiplier, b.Market.DriftMultiplier);
            Assert.Equal(a.Market.ShockOffset, b.Market.ShockOffset);
        }

        [Fact]
        public void DifferentSeeds_Diverge()
        {
            var cfg = MarketConfig.Default;
            var a = new RanchState(42);
            var b = new RanchState(43);

            bool anyDifferent = false;
            for (int day = 0; day < 300; day++)
            {
                StepMarket(a, cfg);
                StepMarket(b, cfg);
                if (a.Market.PricePerKg(cfg, a.Date.Season) != b.Market.PricePerKg(cfg, b.Date.Season))
                {
                    anyDifferent = true;
                }
            }

            Assert.True(anyDifferent, "Different seeds produced an identical 300-day price series.");
        }

        // ---- Seasonality -----------------------------------------------------

        [Fact]
        public void WithDriftAndShocksDisabled_FallIsCheaperThanSpring()
        {
            var cfg = QuietConfig();
            var state = new RanchState(7);

            // Run a full year of quiet updates; the drift multiplier must stay
            // pinned at 1.0 so price is the pure seasonal baseline.
            for (int day = 0; day < GameDate.DaysPerYear; day++)
            {
                StepMarket(state, cfg);
            }

            Assert.Equal(1.0, state.Market.DriftMultiplier);
            Assert.Equal(0.0, state.Market.ShockOffset);

            decimal spring = state.Market.PricePerKg(cfg, Season.Spring);
            decimal fall = state.Market.PricePerKg(cfg, Season.Fall);
            Assert.True(fall < spring, $"Expected Fall ({fall}) < Spring ({spring}).");

            // And the levels are exactly baseline × seasonal multiplier.
            Assert.Equal(cfg.BasePricePerKg * (decimal)cfg.SeasonalPriceMultiplier(Season.Spring), spring);
            Assert.Equal(cfg.BasePricePerKg * (decimal)cfg.SeasonalPriceMultiplier(Season.Fall), fall);
        }

        // ---- Mean reversion --------------------------------------------------

        [Fact]
        public void Drift_StaysBounded_AndAveragesNearSeasonalBaseline()
        {
            var cfg = MarketConfig.Default;
            cfg.ShockChancePerDay = 0.0; // isolate the drift walk.
            var state = new RanchState(123456);

            // Hold the date fixed (Spring) so the baseline is constant.
            double baseline = cfg.SeasonalPriceMultiplier(Season.Spring);
            double sum = 0.0;
            const int days = 10_000;

            for (int day = 0; day < days; day++)
            {
                state.Market.DailyUpdate(state, cfg);
                double multiplier = state.Market.CurrentMultiplier(cfg, Season.Spring);

                Assert.InRange(multiplier, cfg.PriceFloorFrac, cfg.PriceCeilingFrac);
                sum += multiplier;
            }

            double average = sum / days;
            // Loose bounds: the AR(1) walk's stationary mean is the baseline.
            Assert.InRange(average, baseline - 0.05, baseline + 0.05);
        }

        // ---- Shocks ----------------------------------------------------------

        [Fact]
        public void ForcedShock_JoltsPrice_ThenDecaysBackToBaseline()
        {
            var cfg = QuietConfig(); // drift off so only the shock moves price.
            var state = new RanchState(99);
            double baseline = cfg.SeasonalPriceMultiplier(state.Date.Season);

            Assert.Equal(baseline, state.Market.CurrentMultiplier(cfg, state.Date.Season));

            // Force exactly one shock.
            cfg.ShockChancePerDay = 1.0;
            state.Market.DailyUpdate(state, cfg);
            cfg.ShockChancePerDay = 0.0;

            double jolted = state.Market.CurrentMultiplier(cfg, state.Date.Season);
            Assert.True(
                System.Math.Abs(jolted - baseline) >= cfg.ShockMagnitudeMin - 1e-12,
                $"Shock moved price by {System.Math.Abs(jolted - baseline)}, " +
                $"expected at least {cfg.ShockMagnitudeMin}.");

            // Decay horizon: at 0.15/day the offset is < 1% of its (max 0.40)
            // size within ~30 days; assert it is back to within 0.01 by day 40.
            double previousDistance = System.Math.Abs(state.Market.ShockOffset);
            for (int day = 0; day < 40; day++)
            {
                state.Market.DailyUpdate(state, cfg);
                double distance = System.Math.Abs(state.Market.ShockOffset);
                Assert.True(distance < previousDistance, "Shock offset must decay monotonically.");
                previousDistance = distance;
            }

            double settled = state.Market.CurrentMultiplier(cfg, state.Date.Season);
            Assert.InRange(settled, baseline - 0.01, baseline + 0.01);
        }

        // ---- SellAnimal ------------------------------------------------------

        [Fact]
        public void SellAnimal_CreditsQuote_AndRemovesFromHerdAndPaddock()
        {
            var cfg = MarketConfig.Default;
            var state = new RanchState(5);
            state.Cash = 1000m;

            Animal keep = MakeAnimal(state, "Keep", 400.0);
            Animal sell = MakeAnimal(state, "Sell", 512.5);
            state.Herd.Add(keep);
            state.Herd.Add(sell);

            var paddock = new Paddock("north", 4.0, 10_000.0, 1.0, 12_000.0);
            paddock.OccupantIds.Add(keep.Id);
            paddock.OccupantIds.Add(sell.Id);
            state.Paddocks.Add(paddock);

            decimal expectedQuote = state.Market.QuoteFor(sell, cfg, state.Date.Season);
            decimal cashBefore = state.Cash;

            decimal proceeds = Economy.SellAnimal(state, cfg, sell.Id);

            Assert.Equal(expectedQuote, proceeds);
            Assert.Equal(cashBefore + expectedQuote, state.Cash);
            Assert.DoesNotContain(state.Herd, a => a.Id == sell.Id);
            Assert.DoesNotContain(sell.Id, paddock.OccupantIds);
            // The other animal is untouched.
            Assert.Contains(state.Herd, a => a.Id == keep.Id);
            Assert.Contains(keep.Id, paddock.OccupantIds);
        }

        [Fact]
        public void SellAnimal_UnknownId_Throws()
        {
            var state = new RanchState(5);
            state.Herd.Add(MakeAnimal(state, "Bessie", 480.0));

            var ex = Assert.Throws<ArgumentException>(
                () => Economy.SellAnimal(state, MarketConfig.Default, "A-999"));
            Assert.Contains("A-999", ex.Message);
        }

        [Fact]
        public void QuoteFor_IsPriceTimesWeight_RoundedToCents()
        {
            var cfg = QuietConfig();
            var state = new RanchState(5);
            Animal cow = MakeAnimal(state, "Bessie", 481.5);

            decimal price = state.Market.PricePerKg(cfg, Season.Summer);
            decimal expected = decimal.Round(price * 481.5m, 2, MidpointRounding.AwayFromZero);
            Assert.Equal(expected, state.Market.QuoteFor(cow, cfg, Season.Summer));
        }

        // ---- Upkeep & broke --------------------------------------------------

        [Fact]
        public void DailyUpkeep_DebitsHeadsTimesPerHeadPlusOverhead()
        {
            var cfg = MarketConfig.Default;
            var state = new RanchState(5);
            state.Cash = 500m;
            state.Herd.Add(MakeAnimal(state, "A", 400.0));
            state.Herd.Add(MakeAnimal(state, "B", 450.0));
            state.Herd.Add(MakeAnimal(state, "C", 500.0));

            Economy.DailyUpkeep(state, cfg);

            decimal expected = 500m - (3 * cfg.UpkeepPerHeadPerDay + cfg.OverheadPerDay);
            Assert.Equal(expected, state.Cash);
        }

        [Fact]
        public void IsBroke_FlipsWhenCashCrossesZero()
        {
            var cfg = MarketConfig.Default;
            var state = new RanchState(5);
            state.Herd.Add(MakeAnimal(state, "A", 400.0));

            decimal dailyCost = cfg.UpkeepPerHeadPerDay + cfg.OverheadPerDay;
            state.Cash = dailyCost; // exactly one day's costs in the bank.

            Assert.False(Economy.IsBroke(state));

            Economy.DailyUpkeep(state, cfg);
            Assert.Equal(0m, state.Cash);
            Assert.False(Economy.IsBroke(state)); // zero is broke-adjacent, not broke.

            Economy.DailyUpkeep(state, cfg);
            Assert.True(state.Cash < 0m);
            Assert.True(Economy.IsBroke(state));
        }

        // ---- Save round-trip (exercises D10) ---------------------------------

        [Fact]
        public void SaveRoundTrip_MarketContinuesIdentically()
        {
            // Boost shock chance so the 100-day window almost surely includes
            // shock draws — exercising every piece of market state across the
            // save boundary.
            var cfg = MarketConfig.Default;
            cfg.ShockChancePerDay = 0.05;

            var original = new RanchState(20260719);
            for (int day = 0; day < 50; day++)
            {
                StepMarket(original, cfg);
            }

            RanchState restored = RanchSave.FromJson(RanchSave.ToJson(original));

            // The market state itself round-trips bit-identically.
            Assert.Equal(original.Market.DriftMultiplier, restored.Market.DriftMultiplier);
            Assert.Equal(original.Market.ShockOffset, restored.Market.ShockOffset);

            // And both continue the exact same trajectory for 50 more days.
            for (int day = 0; day < 50; day++)
            {
                StepMarket(original, cfg);
                StepMarket(restored, cfg);
                Assert.Equal(
                    original.Market.PricePerKg(cfg, original.Date.Season),
                    restored.Market.PricePerKg(cfg, restored.Date.Season));
            }

            Assert.Equal(original.Market.DriftMultiplier, restored.Market.DriftMultiplier);
            Assert.Equal(original.Market.ShockOffset, restored.Market.ShockOffset);
        }
    }
}
