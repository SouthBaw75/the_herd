namespace CattleRanch.Sim
{
    /// <summary>
    /// Per-animal health condition. Phase 0 carries only the enum surface so the
    /// contract compiles; there is no health simulation yet.
    /// </summary>
    public enum HealthState
    {
        Healthy = 0,
        Sick = 1,
        Injured = 2,
        Parasites = 3,
        PregnantAtRisk = 4
    }
}
