using System.Collections.Generic;
using CattleRanch.Sim.Math;
using CattleRanch.Sim.Systems;

namespace CattleRanch.Sim
{
    /// <summary>
    /// The serializable root that owns all game data. Save/load is
    /// serialize/deserialize of this object (Newtonsoft Json.NET on the Unity
    /// side). No game state lives outside it. Plain POCO — no UnityEngine.
    /// </summary>
    public sealed class RanchState
    {
        /// <summary>Parameterless constructor for serialization (Newtonsoft-friendly).</summary>
        public RanchState()
        {
            Date = GameDate.Start;
            Herd = new List<Animal>();
            Paddocks = new List<Paddock>();
            Rng = new DeterministicRandom();
            Weather = new WeatherState();
            Market = new Systems.Market();
        }

        public RanchState(long seed)
            : this()
        {
            Seed = seed;
            Rng = new DeterministicRandom(seed);
        }

        /// <summary>Master seed; the initial value of <see cref="Rng"/>.</summary>
        public long Seed { get; internal set; }

        /// <summary>
        /// THE randomness stream for the whole sim (D10). Systems draw from this
        /// (never construct their own RNG), always in the engine's fixed update
        /// order, so runs are reproducible and a loaded save continues the exact
        /// stream. Serializes with the state because its full state is public.
        /// </summary>
        public DeterministicRandom Rng { get; internal set; }

        /// <summary>Current date. Advanced by the simulation engine.</summary>
        public GameDate Date { get; internal set; }

        /// <summary>Cash on hand. Money is <see cref="decimal"/> to avoid float drift.</summary>
        public decimal Cash { get; internal set; }

        /// <summary>The herd, iterated in list order for determinism.</summary>
        public List<Animal> Herd { get; internal set; }

        /// <summary>The paddocks, iterated in list order for determinism.</summary>
        public List<Paddock> Paddocks { get; internal set; }

        /// <summary>Current weather (Phase 1). Advanced daily by <see cref="WeatherSystem"/>.</summary>
        public WeatherState Weather { get; internal set; }

        /// <summary>Market price-model state (drift + shock). Serializes with the state.</summary>
        public Systems.Market Market { get; internal set; }

        /// <summary>
        /// Deterministic entity-id counter. Ids come from here — never from
        /// <c>Guid.NewGuid()</c> — so a replay assigns the same ids.
        /// </summary>
        public long NextEntityId { get; internal set; }

        /// <summary>Allocates the next deterministic id with the given prefix (e.g. "A" → "A-0").</summary>
        public string AllocateId(string prefix)
        {
            string id = prefix + "-" + NextEntityId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            NextEntityId++;
            return id;
        }
    }
}
