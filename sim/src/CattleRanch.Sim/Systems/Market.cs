using System;
using CattleRanch.Sim.Math;

namespace CattleRanch.Sim.Systems
{
    /// <summary>
    /// The cattle market price model (design doc §4.3). The effective price
    /// multiplier is
    /// <c>seasonal(season) × DriftMultiplier + ShockOffset</c>,
    /// clamped to the configured floor/ceiling fractions of the base price:
    /// <list type="bullet">
    ///   <item>the <b>seasonal baseline</b> encodes the cow-calf cycle (fall
    ///         glut cheapest, spring scarcity premium);</item>
    ///   <item><see cref="DriftMultiplier"/> is a mean-reverting random walk
    ///         around 1.0 — day-to-day wiggle that never wanders off forever;</item>
    ///   <item><see cref="ShockOffset"/> is jolted by rare shocks (drought
    ///         sell-offs crash prices; supply squeezes spike them) and decays
    ///         geometrically back to zero.</item>
    /// </list>
    /// Only the two fields above are state; both are public-get POCO properties
    /// so the market serializes inside <see cref="RanchState"/> and a loaded
    /// save continues bit-identically (D10 — all randomness is drawn from
    /// <see cref="RanchState.Rng"/>, never a locally constructed RNG). Tunables
    /// live in <see cref="MarketConfig"/>, passed in per call so saved games
    /// pick up rebalanced data without migration.
    /// </summary>
    public sealed class Market
    {
        /// <summary>Parameterless constructor for serialization (Newtonsoft-friendly).</summary>
        public Market()
        {
            DriftMultiplier = 1.0;
            ShockOffset = 0.0;
        }

        /// <summary>
        /// Mean-reverting multiplicative deviation from the seasonal baseline.
        /// 1.0 = exactly on baseline.
        /// </summary>
        public double DriftMultiplier { get; internal set; }

        /// <summary>
        /// Additive shock displacement of the price multiplier. Zero when no
        /// shock is active; decays geometrically after a shock.
        /// </summary>
        public double ShockOffset { get; internal set; }

        /// <summary>
        /// Advances the market one day. Draws from <paramref name="state"/>'s
        /// single RNG stream in a fixed order (drift step, shock roll, then
        /// shock parameters only if one fires) so runs are reproducible and a
        /// loaded save continues the exact stream.
        /// </summary>
        public void DailyUpdate(RanchState state, MarketConfig config)
        {
            IRandom rng = state.Rng;

            // 1. Drift: AR(1) pull back toward 1.0 plus a Gaussian step. Drawn
            //    unconditionally (even with stdDev 0) to keep the stream layout
            //    independent of tuning values.
            double step = rng.NextGaussian(0.0, config.DriftStdDev);
            DriftMultiplier += config.MeanReversionRate * (1.0 - DriftMultiplier) + step;

            // 2. Any active shock decays toward zero.
            ShockOffset *= 1.0 - config.ShockDecayRate;

            // 3. Rare shock: jolt the multiplier up or down by a magnitude in
            //    [min, max]. The roll is drawn unconditionally; the two shock
            //    parameter draws only happen when a shock actually fires.
            if (rng.NextDouble() < config.ShockChancePerDay)
            {
                double magnitude = config.ShockMagnitudeMin
                    + rng.NextDouble() * (config.ShockMagnitudeMax - config.ShockMagnitudeMin);
                double sign = rng.NextDouble() < 0.5 ? -1.0 : 1.0;
                ShockOffset += sign * magnitude;
            }
        }

        /// <summary>
        /// The effective price multiplier for a season: seasonal baseline ×
        /// drift + shock, clamped to the configured floor/ceiling.
        /// </summary>
        public double CurrentMultiplier(MarketConfig config, Season season)
        {
            double multiplier = config.SeasonalPriceMultiplier(season) * DriftMultiplier + ShockOffset;

            if (multiplier < config.PriceFloorFrac)
            {
                return config.PriceFloorFrac;
            }

            if (multiplier > config.PriceCeilingFrac)
            {
                return config.PriceCeilingFrac;
            }

            return multiplier;
        }

        /// <summary>Current effective sale price per kg liveweight ($/kg).</summary>
        public decimal PricePerKg(MarketConfig config, Season season)
        {
            return config.BasePricePerKg * (decimal)CurrentMultiplier(config, season);
        }

        /// <summary>
        /// What the sale barn offers for an animal right now, rounded to cents.
        /// Phase 1: flat price × liveweight. Class-based pricing (weaned-calf
        /// premium, cull discount, marbling/finished grades) lands here later —
        /// this is the seam where Animal age/sex/genome would modulate $/kg.
        /// </summary>
        public decimal QuoteFor(Animal animal, MarketConfig config, Season season)
        {
            if (animal == null)
            {
                throw new ArgumentNullException(nameof(animal));
            }

            return decimal.Round(
                PricePerKg(config, season) * (decimal)animal.WeightKg,
                2,
                MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>
    /// Cash transactions against a <see cref="RanchState"/>: sales, daily
    /// upkeep, and the go-broke check. Static and stateless — all state lives
    /// in <see cref="RanchState"/> so it serializes.
    /// </summary>
    public static class Economy
    {
        /// <summary>
        /// Sells an animal at the current market quote: removes it from the
        /// herd and from any paddock's occupant list, credits
        /// <see cref="RanchState.Cash"/>, and returns the sale amount.
        /// Throws <see cref="ArgumentException"/> for an unknown id.
        /// </summary>
        public static decimal SellAnimal(RanchState state, MarketConfig config, string animalId)
        {
            // Deterministic list-order scan (never hash-based lookup order).
            int index = -1;
            for (int i = 0; i < state.Herd.Count; i++)
            {
                if (state.Herd[i].Id == animalId)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                throw new ArgumentException(
                    $"No animal with id '{animalId}' exists in the herd.", nameof(animalId));
            }

            Animal animal = state.Herd[index];
            decimal quote = state.Market.QuoteFor(animal, config, state.Date.Season);

            state.Herd.RemoveAt(index);
            for (int i = 0; i < state.Paddocks.Count; i++)
            {
                state.Paddocks[i].OccupantIds.Remove(animalId);
            }

            state.Cash += quote;
            return quote;
        }

        /// <summary>
        /// Debits one day of running costs: heads × per-head upkeep (feed, vet,
        /// fuel aggregate) plus fixed ranch overhead.
        /// </summary>
        public static void DailyUpkeep(RanchState state, MarketConfig config)
        {
            state.Cash -= state.Herd.Count * config.UpkeepPerHeadPerDay + config.OverheadPerDay;
        }

        /// <summary>
        /// Go-broke = game over (Phase 1: broke is simply cash below zero).
        /// A debt mechanic — an operating line of credit with interest and a
        /// foreclosure threshold — would land here later, replacing the bare
        /// zero test with a credit-limit test.
        /// </summary>
        public static bool IsBroke(RanchState state) => state.Cash < 0m;
    }
}
