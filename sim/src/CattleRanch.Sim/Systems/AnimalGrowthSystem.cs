using System.Collections.Generic;

namespace CattleRanch.Sim.Systems
{
    /// <summary>
    /// Daily aging and weight change for every animal in the herd. Each day,
    /// per animal (herd iterated by index for determinism):
    /// <list type="number">
    ///   <item>AgeDays advances by exactly 1;</item>
    ///   <item>potential gain = <c>BaseDailyGainKg × geneticFactor ×
    ///         (1 − Weight/MatureWeight)</c> — the asymptotic term IS the age
    ///         curve: light calves gain fast, gain tapers to zero at the
    ///         genome-scaled mature weight, so adults trend toward but never
    ///         overshoot their cap;</item>
    ///   <item>gain is scaled by forage satisfaction (grass available above the
    ///         root residual in the animal's paddock vs. the herd's demand
    ///         there) and by the weather stress multiplier;</item>
    ///   <item>a maintenance shortfall of <c>MaintenanceLossKgPerDay ×
    ///         (1 − satisfaction)</c> is subtracted — an animal in no paddock or
    ///         on residual-only grass loses weight;</item>
    ///   <item>weight is floored at <c>MinWeightKg</c> (never negative).</item>
    /// </list>
    /// Pure arithmetic — consumes NO randomness, so it cannot perturb the
    /// shared RNG stream (D10). Phase 2 hooks land here: body-condition score
    /// derived from weight vs. mature weight, and health effects (sickness
    /// slowing gain, sustained starvation degrading <c>Animal.Health</c>).
    /// </summary>
    public sealed class AnimalGrowthSystem
    {
        private readonly AnimalGrowthConfig _config;

        public AnimalGrowthSystem(AnimalGrowthConfig? config = null)
        {
            _config = config ?? AnimalGrowthConfig.Default;
        }

        public AnimalGrowthConfig Config => _config;

        /// <summary>
        /// Advances every animal one day. <paramref name="animalStressMultiplier"/>
        /// is today's <see cref="WeatherSystem.AnimalStressMultiplier"/> (1.0 when
        /// running without weather). Call once per tick, after
        /// <see cref="WeatherSystem.DailyUpdate"/> and before grazing depletes
        /// the day's grass, so satisfaction reflects the grass the herd walked
        /// out onto.
        /// </summary>
        public void DailyUpdate(RanchState state, double animalStressMultiplier = 1.0)
        {
            // Pass 1: forage satisfaction per animal id, from paddock occupancy.
            // Paddocks and their occupant lists are iterated in list order; the
            // dictionary is only ever read by key, so its ordering is irrelevant
            // to determinism.
            var satisfactionByAnimalId = new Dictionary<string, double>();
            for (int p = 0; p < state.Paddocks.Count; p++)
            {
                Paddock paddock = state.Paddocks[p];
                int headCount = paddock.OccupantIds.Count;
                if (headCount == 0)
                {
                    continue;
                }

                double residual = _config.ResidualBiomassPerHa * paddock.AreaHectares;
                double available = paddock.GrassBiomass - residual;
                if (available < 0.0)
                {
                    available = 0.0;
                }

                double demand = headCount * _config.DailyIntakeKg;
                double satisfaction = demand <= 0.0 ? 1.0 : available / demand;
                if (satisfaction > 1.0)
                {
                    satisfaction = 1.0;
                }

                for (int o = 0; o < paddock.OccupantIds.Count; o++)
                {
                    satisfactionByAnimalId[paddock.OccupantIds[o]] = satisfaction;
                }
            }

            // Pass 2: age and grow each animal, herd in list order.
            for (int i = 0; i < state.Herd.Count; i++)
            {
                Animal animal = state.Herd[i];
                animal.AgeDays += 1;

                double satisfaction = satisfactionByAnimalId.TryGetValue(animal.Id, out double s)
                    ? s
                    : _config.UnassignedForageSatisfaction;

                double geneticFactor = _config.GrowthRateFactor(animal.Genome.GrowthRate);
                double matureWeight = _config.MatureWeightKg(animal.Sex, animal.Genome.GrowthRate);

                // Distance to maturity: 1 for a newborn, 0 at (or beyond) the
                // asymptote — an animal already at its cap simply stops gaining.
                double distanceToMaturity = 1.0 - animal.WeightKg / matureWeight;
                if (distanceToMaturity < 0.0)
                {
                    distanceToMaturity = 0.0;
                }

                double gain = _config.BaseDailyGainKg
                              * geneticFactor
                              * distanceToMaturity
                              * satisfaction
                              * animalStressMultiplier;
                double maintenanceShortfall = _config.MaintenanceLossKgPerDay * (1.0 - satisfaction);

                double weight = animal.WeightKg + gain - maintenanceShortfall;
                if (weight < _config.MinWeightKg)
                {
                    weight = _config.MinWeightKg;
                }

                animal.WeightKg = weight;

                // Phase 2: update body-condition score here (weight relative to
                // mature weight) and apply health effects (Animal.Health) —
                // deliberately out of Phase 1 scope.
            }
        }
    }
}
