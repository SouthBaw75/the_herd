using System.Collections.Generic;
using CattleRanch.Sim.Math;

namespace CattleRanch.Sim.Systems
{
    /// <summary>
    /// What breeding did today — the presentation layer (terminal prototype,
    /// later Unity) renders announcements from this. Plain output object; it is
    /// NOT sim state and is never serialized.
    /// </summary>
    public sealed class BreedingReport
    {
        /// <summary>Cows that conceived today (herd list order).</summary>
        public List<Animal> Conceptions { get; } = new List<Animal>();

        /// <summary>Calves born today (dam order = herd list order).</summary>
        public List<Animal> BornToday { get; } = new List<Animal>();
    }

    /// <summary>
    /// Bare-bones breeding (design doc §4.1, Phase 1): fall conception →
    /// 87-day gestation → a spring calf whose traits blend BOTH parents.
    /// <para>
    /// <b>RNG draw order (D10 — load-bearing, do not reorder):</b> all draws
    /// come from <c>state.Rng</c>, and the layout of the stream is a pure
    /// function of sim state — never of tuning values — so a save loaded
    /// mid-pregnancy continues bit-identically:
    /// <list type="number">
    ///   <item><b>Conception phase</b> (only when <c>state.Date.Season</c> is
    ///         the breeding season): for each open breeding-age cow in herd
    ///         list order that has a breeding-age bull in her paddock, exactly
    ///         ONE uniform draw — drawn unconditionally and then compared
    ///         against the (tuning-derived) chance, never short-circuited on
    ///         config, so tuning the chance cannot shift the stream layout.</item>
    ///   <item><b>Gestation phase</b>: no draws.</item>
    ///   <item><b>Birth phase</b>: per calf, in fixed sequence — 7 Gaussian
    ///         draws (one per trait, in <see cref="Genome"/> field declaration
    ///         order: GrowthRate, CalvingEase, Marbling, Mothering,
    ///         HeatTolerance, DiseaseResistance, Fertility), then 1 uniform
    ///         for the hybrid-vigor/dud fate (always drawn, even when both
    ///         chances are 0), then 1 uniform for sex, then 1 Gaussian for
    ///         birth weight. 10 draws per calf, dams in herd list order.</item>
    /// </list>
    /// Newly born calves are appended to the herd but are never bred, gestated,
    /// or birthed in the same tick: every phase iterates over a snapshot of the
    /// herd count taken before any calf is added.
    /// </para>
    /// </summary>
    public sealed class BreedingSystem
    {
        private readonly BreedingConfig _config;

        public BreedingSystem(BreedingConfig? config = null)
        {
            _config = config ?? BreedingConfig.Default;
        }

        public BreedingConfig Config => _config;

        /// <summary>
        /// Advances breeding one day: conception rolls (breeding season only),
        /// then gestation, then births at term. Call once per tick, after the
        /// date has advanced. Returns today's conceptions and births for the
        /// caller to announce.
        /// </summary>
        public BreedingReport DailyUpdate(RanchState state)
        {
            var report = new BreedingReport();

            // Snapshot: calves born below are appended to Herd and must not be
            // bred/gestated/birthed in the same tick.
            int herdCountAtStart = state.Herd.Count;

            // ---- Phase 1: conception (breeding season only) ----------------
            if (state.Date.Season == _config.BreedingSeason)
            {
                for (int i = 0; i < herdCountAtStart; i++)
                {
                    Animal cow = state.Herd[i];
                    if (cow.Sex != Sex.Female
                        || cow.Pregnancy.IsPregnant
                        || cow.AgeDays < _config.MinBreedingAgeDays)
                    {
                        continue;
                    }

                    // The sire is the FIRST breeding-age bull (herd list order)
                    // sharing her paddock — deterministic. State-based
                    // eligibility may gate the draw (it is save/load-safe);
                    // tuning values may not.
                    Animal? sire = FindSire(state, cow);
                    if (sire == null)
                    {
                        continue;
                    }

                    // One unconditional draw per eligible cow-with-sire.
                    double roll = state.Rng.NextDouble();

                    // Chance scales with the COW's Fertility. The bull's
                    // fertility could factor in later — e.g. multiply by
                    // FertilityFactor(sire.Genome.Fertility) — making bull
                    // selection a genetics decision beyond his passed-on traits.
                    double chance = _config.BaseConceptionChancePerDay
                                    * _config.FertilityFactor(cow.Genome.Fertility);

                    if (roll < chance)
                    {
                        cow.Pregnancy = new PregnancyState
                        {
                            IsPregnant = true,
                            DaysPregnant = 0,
                            SireGenome = sire.Genome,
                        };
                        report.Conceptions.Add(cow);
                    }
                }
            }

            // ---- Phase 2: gestation (no RNG) -------------------------------
            // Every cow pregnant BEFORE today advances one day; a cow that
            // conceived this tick stays at DaysPregnant = 0 (DaysPregnant =
            // whole days elapsed since conception).
            for (int i = 0; i < herdCountAtStart; i++)
            {
                Animal cow = state.Herd[i];
                if (!cow.Pregnancy.IsPregnant || report.Conceptions.Contains(cow))
                {
                    continue;
                }

                PregnancyState pregnancy = cow.Pregnancy;
                pregnancy.DaysPregnant += 1;
                cow.Pregnancy = pregnancy;
            }

            // ---- Phase 3: birth at term ------------------------------------
            for (int i = 0; i < herdCountAtStart; i++)
            {
                Animal dam = state.Herd[i];
                if (!dam.Pregnancy.IsPregnant
                    || dam.Pregnancy.DaysPregnant < _config.GestationDays)
                {
                    continue;
                }

                report.BornToday.Add(DeliverCalf(state, dam));
            }

            return report;
        }

        /// <summary>
        /// The first breeding-age bull (herd list order) sharing the cow's
        /// paddock, or null. A cow in no paddock has no sire available.
        /// </summary>
        private Animal? FindSire(RanchState state, Animal cow)
        {
            Paddock? paddock = FindPaddockOf(state, cow.Id);
            if (paddock == null)
            {
                return null;
            }

            for (int i = 0; i < state.Herd.Count; i++)
            {
                Animal candidate = state.Herd[i];
                if (candidate.Sex == Sex.Male
                    && candidate.AgeDays >= _config.MinBreedingAgeDays
                    && paddock.OccupantIds.Contains(candidate.Id))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>The animal's paddock, scanning paddocks in list order; null when unassigned.</summary>
        private static Paddock? FindPaddockOf(RanchState state, string animalId)
        {
            for (int i = 0; i < state.Paddocks.Count; i++)
            {
                if (state.Paddocks[i].OccupantIds.Contains(animalId))
                {
                    return state.Paddocks[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Creates the calf (10 RNG draws, fixed sequence — see class doc),
        /// adds it to the herd and the dam's paddock, and opens the dam.
        /// </summary>
        private Animal DeliverCalf(RanchState state, Animal dam)
        {
            IRandom rng = state.Rng;
            Genome damGenome = dam.Genome;
            Genome sireGenome = dam.Pregnancy.SireGenome;

            // Draws 1–7: mean of the parents ± Gaussian variance, per trait,
            // in Genome field declaration order.
            double growthRate = BlendTrait(damGenome.GrowthRate, sireGenome.GrowthRate, rng);
            double calvingEase = BlendTrait(damGenome.CalvingEase, sireGenome.CalvingEase, rng);
            double marbling = BlendTrait(damGenome.Marbling, sireGenome.Marbling, rng);
            double mothering = BlendTrait(damGenome.Mothering, sireGenome.Mothering, rng);
            double heatTolerance = BlendTrait(damGenome.HeatTolerance, sireGenome.HeatTolerance, rng);
            double diseaseResistance = BlendTrait(damGenome.DiseaseResistance, sireGenome.DiseaseResistance, rng);
            double fertility = BlendTrait(damGenome.Fertility, sireGenome.Fertility, rng);

            // Draw 8: hybrid-vigor / dud fate. One uniform walked through
            // cumulative buckets (vigor first, then dud), always drawn so the
            // stream layout is independent of the tuned chances.
            double fateRoll = rng.NextDouble();
            double shift = 0.0;
            if (fateRoll < _config.HybridVigorChance)
            {
                shift = _config.HybridVigorBoost;
            }
            else if (fateRoll < _config.HybridVigorChance + _config.DudChance)
            {
                shift = -_config.DudPenalty;
            }

            var calfGenome = new Genome(
                ClampTrait(growthRate + shift),
                ClampTrait(calvingEase + shift),
                ClampTrait(marbling + shift),
                ClampTrait(mothering + shift),
                ClampTrait(heatTolerance + shift),
                ClampTrait(diseaseResistance + shift),
                ClampTrait(fertility + shift));

            // Draw 9: sex, one uniform.
            Sex sex = rng.NextDouble() < 0.5 ? Sex.Female : Sex.Male;

            // Draw 10: birth weight, floored above zero.
            double weightKg = rng.NextGaussian(_config.NewbornWeightKg, _config.NewbornWeightStdDevKg);
            if (weightKg < _config.NewbornWeightFloorKg)
            {
                weightKg = _config.NewbornWeightFloorKg;
            }

            var calf = new Animal(
                state.AllocateId("A"),
                string.Empty,
                sex,
                dam.Breed,
                0,
                weightKg,
                calfGenome);

            state.Herd.Add(calf);

            // The calf joins the dam's paddock (paddocks scanned in list
            // order); a dam in no paddock leaves the calf unassigned too.
            Paddock? damPaddock = FindPaddockOf(state, dam.Id);
            damPaddock?.OccupantIds.Add(calf.Id);

            dam.Pregnancy = PregnancyState.None;
            return calf;
        }

        /// <summary>Parent mean plus one Gaussian variance draw (sim math in double; storage clamps to float).</summary>
        private double BlendTrait(float damTrait, float sireTrait, IRandom rng) =>
            (damTrait + sireTrait) / 2.0 + rng.NextGaussian(0.0, _config.TraitBlendStdDev);

        /// <summary>Clamps a computed trait into the genome's [0, 100] scale.</summary>
        private static float ClampTrait(double value)
        {
            if (value < 0.0)
            {
                return 0f;
            }

            if (value > 100.0)
            {
                return 100f;
            }

            return (float)value;
        }
    }
}
