namespace CattleRanch.Sim.Math
{
    /// <summary>
    /// Deterministic pseudo-random generator based on the well-known
    /// <c>SplitMix64</c> algorithm. Chosen (over wrapping
    /// <see cref="System.Random"/>) because its output is defined purely by
    /// integer arithmetic on a 64-bit state: identical seeds yield an identical
    /// stream on any runtime/architecture. This is the guarantee the simulation
    /// determinism rests on.
    /// </summary>
    public sealed class DeterministicRandom : IRandom
    {
        // SplitMix64 constants.
        private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
        private const ulong MixA = 0xBF58476D1CE4E5B9UL;
        private const ulong MixB = 0x94D049BB133111EBUL;

        // 2^53, the number of representable integers in a double's mantissa.
        private const double TwoPow53 = 9007199254740992.0;

        /// <summary>Parameterless constructor for serialization (Newtonsoft-friendly).</summary>
        public DeterministicRandom()
        {
        }

        public DeterministicRandom(long seed)
        {
            State = unchecked((ulong)seed);
        }

        /// <summary>
        /// The complete generator state (D10). Public-get / internal-set so it
        /// serializes with <see cref="RanchState"/> and a loaded game continues
        /// the exact stream a never-saved run would have produced. There is
        /// deliberately no other hidden state (no Box-Muller spare cache).
        /// </summary>
        public ulong State { get; internal set; }

        /// <summary>Advances the state one step and returns the next raw 64-bit value.</summary>
        private ulong NextUInt64()
        {
            unchecked
            {
                State += GoldenGamma;
                ulong z = State;
                z = (z ^ (z >> 30)) * MixA;
                z = (z ^ (z >> 27)) * MixB;
                return z ^ (z >> 31);
            }
        }

        public double NextDouble()
        {
            // Use the top 53 bits for a uniform value in [0, 1).
            return (NextUInt64() >> 11) / TwoPow53;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new System.ArgumentException(
                    "maxExclusive must be greater than minInclusive.", nameof(maxExclusive));
            }

            ulong range = (ulong)((long)maxExclusive - minInclusive);
            // Modulo reduction; bias is negligible for gameplay-scale ranges and
            // fully deterministic.
            ulong sample = NextUInt64() % range;
            return (int)((long)minInclusive + (long)sample);
        }

        public double NextGaussian(double mean, double stdDev)
        {
            // Box-Muller transform, WITHOUT the usual spare-value cache: the
            // generator's entire state must be the single State ulong so that
            // save/load reproduces the exact stream (D10). One discarded normal
            // per call is a negligible price. Draw u1 in (0, 1] so Log is finite.
            double u1 = 1.0 - NextDouble();
            double u2 = NextDouble();
            double magnitude = System.Math.Sqrt(-2.0 * System.Math.Log(u1));
            double angle = 2.0 * System.Math.PI * u2;

            return mean + stdDev * magnitude * System.Math.Cos(angle);
        }
    }
}
