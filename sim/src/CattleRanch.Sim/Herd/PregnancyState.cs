namespace CattleRanch.Sim
{
    /// <summary>
    /// Pregnancy status for a female animal. Plain serialization-friendly
    /// struct (public fields, no attributes — persistence policy lives in
    /// <c>RanchSave</c>, D8). Set/cleared by
    /// <c>CattleRanch.Sim.Systems.BreedingSystem</c>.
    /// </summary>
    public struct PregnancyState
    {
        public bool IsPregnant;

        /// <summary>Days into gestation, when pregnant.</summary>
        public int DaysPregnant;

        /// <summary>
        /// The sire's genome, captured at conception. Stored on the pregnancy
        /// (not looked up at calving) because the sire may be sold or die
        /// before the calf is born — the calf must still blend BOTH parents.
        /// Meaningful only while <see cref="IsPregnant"/> is true.
        /// </summary>
        public Genome SireGenome;

        /// <summary>A convenient "not pregnant" value.</summary>
        public static PregnancyState None => default;
    }
}
