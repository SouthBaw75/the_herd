using CattleRanch.Sim;
using CattleRanch.Sim.Scenarios;

namespace CattleRanch.Web;

/// <summary>
/// Thin alias over the sim's canonical starting scenario (D12: one copy,
/// shared by every frontend). Kept so page code reads naturally.
/// </summary>
internal static class WebNewGame
{
    public const decimal StartingCash = StartingRanch.StartingCash;
    public const long DefaultSeed = StartingRanch.DefaultSeed;

    public static RanchState Create(long seed) => StartingRanch.Create(seed);
}
