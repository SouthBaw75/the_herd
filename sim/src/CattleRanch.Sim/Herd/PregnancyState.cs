namespace CattleRanch.Sim
{
    /// <summary>
    /// Pregnancy status for a female animal. Phase 0 minimal: enough to compile
    /// and serialize; breeding/gestation simulation arrives in a later phase.
    /// </summary>
    public struct PregnancyState
    {
        public bool IsPregnant;

        /// <summary>Days into gestation, when pregnant.</summary>
        public int DaysPregnant;

        /// <summary>A convenient "not pregnant" value.</summary>
        public static PregnancyState None => default;
    }
}
