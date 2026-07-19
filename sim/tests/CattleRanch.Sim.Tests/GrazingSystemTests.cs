using CattleRanch.Sim;
using CattleRanch.Sim.Systems;

namespace CattleRanch.Sim.Tests
{
    public class GrazingSystemTests
    {
        private static Paddock FullPaddock(double areaHa, double soil, GrazingConfig cfg)
        {
            double cap = cfg.MaxCapacityPerHa * areaHa * soil;
            return new Paddock("P", areaHa, cap, soil, cap);
        }

        [Fact]
        public void UngrazedHealthyPaddock_GrowsMonotonically_AndNeverExceedsCapacity()
        {
            var cfg = GrazingConfig.Default;
            var sys = new GrazingSystem(cfg);
            // 10 ha, healthy soil, starting well below capacity.
            double cap = cfg.MaxCapacityPerHa * 10.0 * 1.0; // 30000
            var p = new Paddock("A", 10.0, 1000.0, 1.0, cap);

            double previous = p.GrassBiomass;
            for (int day = 0; day < 50; day++)
            {
                sys.UpdateDaily(p, headCount: 0, Season.Spring);

                Assert.True(p.GrassBiomass > previous,
                    $"biomass should strictly increase each day (day {day})");
                Assert.True(p.GrassBiomass <= p.CarryingCapacity,
                    $"biomass must never exceed capacity (day {day})");
                previous = p.GrassBiomass;
            }

            // Asymptotically approaches, but does not surpass, capacity.
            Assert.True(p.GrassBiomass > 0.99 * p.CarryingCapacity);
            Assert.True(p.GrassBiomass < p.CarryingCapacity);
            Assert.Equal(1.0, p.SoilHealth); // healthy soil stays healthy while rested
        }

        [Fact]
        public void WinterRegrowth_IsApproximatelyZero()
        {
            var cfg = GrazingConfig.Default;
            var sys = new GrazingSystem(cfg);
            var p = new Paddock("A", 10.0, 5000.0, 1.0, cfg.MaxCapacityPerHa * 10.0);
            double before = p.GrassBiomass;

            for (int day = 0; day < 28; day++) // a whole winter, ungrazed
            {
                sys.UpdateDaily(p, headCount: 0, Season.Winter);
            }

            Assert.Equal(before, p.GrassBiomass); // exactly zero regrowth in winter
        }

        [Fact]
        public void SeasonalGrowth_SpringOutgrowsSummerOutgrowsFall_WinterZero()
        {
            var cfg = GrazingConfig.Default;
            var sys = new GrazingSystem(cfg);

            double GrowthIn(Season s)
            {
                var p = new Paddock("A", 10.0, 5000.0, 1.0, cfg.MaxCapacityPerHa * 10.0);
                double before = p.GrassBiomass;
                sys.UpdateDaily(p, 0, s);
                return p.GrassBiomass - before;
            }

            double spring = GrowthIn(Season.Spring);
            double summer = GrowthIn(Season.Summer);
            double fall = GrowthIn(Season.Fall);
            double winter = GrowthIn(Season.Winter);

            Assert.True(spring > summer);
            Assert.True(summer > fall);
            Assert.True(fall > winter);
            Assert.Equal(0.0, winter);
        }

        [Fact]
        public void SustainedOverstock_CrashesBiomass_AndDegradesSoil()
        {
            var cfg = GrazingConfig.Default;
            var sys = new GrazingSystem(cfg);
            var p = FullPaddock(5.0, 1.0, cfg); // cap0 = 15000, soil 1.0
            double cap0 = p.CarryingCapacity;

            // 200 head => intake 2400 kg/day, above the ~1500 kg/day max regrowth.
            for (int day = 0; day < 40; day++)
            {
                sys.UpdateDaily(p, headCount: 200, Season.Spring);
            }

            // Biomass collapses to the residual root floor.
            Assert.True(p.GrassBiomass < 0.05 * cap0,
                $"biomass should crash under sustained overstock (was {p.GrassBiomass})");
            Assert.Equal(cfg.ResidualBiomassPerHa * 5.0, p.GrassBiomass, precision: 6);

            // And soil health is meaningfully degraded (fast to strip).
            Assert.True(p.SoilHealth < 0.80,
                $"soil should degrade under sustained overstock (was {p.SoilHealth})");
            Assert.True(p.SoilHealth >= cfg.SoilHealthMin);

            // Degraded soil lowers carrying capacity.
            Assert.True(p.CarryingCapacity < cap0);
        }

        [Fact]
        public void RestedPaddock_RecoversBiomassFast_ButSoilSlowly_TheAsymmetry()
        {
            var cfg = GrazingConfig.Default;
            var sys = new GrazingSystem(cfg);
            var p = FullPaddock(5.0, 1.0, cfg);
            double cap0 = p.CarryingCapacity; // 15000

            // --- Phase 1: strip it (fast to strip) ---
            for (int day = 0; day < 40; day++)
            {
                sys.UpdateDaily(p, headCount: 200, Season.Spring);
            }
            double biomassStripped = p.GrassBiomass; // ~250 (residual)
            double soilStripped = p.SoilHealth;       // ~0.69

            Assert.True(biomassStripped < 0.05 * cap0);
            Assert.True(soilStripped < 0.80);

            // --- Phase 2: rest it, same number of days (slow to heal) ---
            for (int day = 0; day < 60; day++)
            {
                sys.UpdateDaily(p, headCount: 0, Season.Spring);
            }
            double biomassRested = p.GrassBiomass;
            double soilRested = p.SoilHealth;

            // Grass rebounds almost fully to the (soil-limited) capacity...
            Assert.True(biomassRested > 0.90 * p.CarryingCapacity,
                $"grass should recover to near capacity when rested (was {biomassRested} of {p.CarryingCapacity})");
            Assert.True(biomassRested > 10.0 * biomassStripped,
                "grass biomass should multiply many-fold during rest");

            // ...but soil is still deeply depressed: over the SAME window it barely
            // moved. This is the core discipline of the game: fast to strip, slow
            // to heal.
            Assert.True(soilRested < 0.75,
                $"soil should remain far below healthy after only 60 rest days (was {soilRested})");
            Assert.True(System.Math.Abs(soilRested - soilStripped) < 0.05,
                $"soil barely changes over the rest window (strip {soilStripped} -> rest {soilRested})");

            // Concrete asymmetry: grass covered >90% of the gap to capacity while
            // soil closed <10% of its gap back to full health, in the same 60 days.
            double biomassGapClosed = (biomassRested - biomassStripped) / (p.CarryingCapacity - biomassStripped);
            double soilGapClosed = (soilRested - soilStripped) / (1.0 - soilStripped);
            Assert.True(biomassGapClosed > 0.90);
            Assert.True(soilGapClosed < 0.10);
            Assert.True(biomassGapClosed > soilGapClosed);
        }
    }
}
