using System.Collections.Generic;

namespace CattleRanch.Sim
{
    /// <summary>
    /// A grazing paddock — the "heartbeat" of the sim. Grass grows on a logistic
    /// curve toward <see cref="CarryingCapacity"/> (itself a function of
    /// <see cref="SoilHealth"/> and area) and is stripped by grazing. The daily
    /// state transition lives in <c>CattleRanch.Sim.Systems.GrazingSystem</c>;
    /// the mutable fields here are set only by that system (internal setters keep
    /// the paddock read-only to presentation/Unity code).
    /// </summary>
    public sealed class Paddock
    {
        /// <summary>Parameterless constructor for serialization (Newtonsoft-friendly).</summary>
        public Paddock()
        {
            Id = string.Empty;
            OccupantIds = new List<string>();
        }

        public Paddock(
            string id,
            double areaHectares,
            double grassBiomass,
            double soilHealth,
            double carryingCapacity)
        {
            Id = id;
            AreaHectares = areaHectares;
            GrassBiomass = grassBiomass;
            SoilHealth = soilHealth;
            CarryingCapacity = carryingCapacity;
            OccupantIds = new List<string>();
        }

        /// <summary>Stable identifier.</summary>
        public string Id { get; internal set; }

        /// <summary>Area in hectares.</summary>
        public double AreaHectares { get; internal set; }

        /// <summary>Available grass, in kg dry matter (DM).</summary>
        public double GrassBiomass { get; internal set; }

        /// <summary>Soil health in [0, 1]; a multiplier on regrowth and carrying capacity.</summary>
        public double SoilHealth { get; internal set; }

        /// <summary>Biomass ceiling (kg DM) — a function of soil health and area.</summary>
        public double CarryingCapacity { get; internal set; }

        /// <summary>
        /// Ids of animals currently grazing here. Head count for the grazing
        /// model is derived from this list. Iterated in list order for
        /// determinism.
        /// </summary>
        public List<string> OccupantIds { get; internal set; }
    }
}
