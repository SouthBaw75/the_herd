namespace CattleRanch.Sim.Math
{
    /// <summary>
    /// Injected source of randomness for the simulation. Every stochastic
    /// decision in a system flows through this interface so that a run is
    /// bit-for-bit reproducible given the same seed and inputs.
    /// <see cref="System.Random"/> / <c>UnityEngine.Random</c> are never called
    /// directly inside systems.
    /// </summary>
    public interface IRandom
    {
        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>Uniform double in [0, 1).</summary>
        double NextDouble();

        /// <summary>Normally distributed double with the given mean and standard deviation.</summary>
        double NextGaussian(double mean, double stdDev);
    }
}
