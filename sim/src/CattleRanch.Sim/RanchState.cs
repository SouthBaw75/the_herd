using System.Collections.Generic;

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
        }

        public RanchState(long seed)
            : this()
        {
            Seed = seed;
        }

        /// <summary>Master seed; every stochastic system derives its RNG from this.</summary>
        public long Seed { get; internal set; }

        /// <summary>Current date. Advanced by the simulation engine.</summary>
        public GameDate Date { get; internal set; }

        /// <summary>Cash on hand. Money is <see cref="decimal"/> to avoid float drift.</summary>
        public decimal Cash { get; internal set; }

        /// <summary>The herd, iterated in list order for determinism.</summary>
        public List<Animal> Herd { get; internal set; }

        /// <summary>The paddocks, iterated in list order for determinism.</summary>
        public List<Paddock> Paddocks { get; internal set; }

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
