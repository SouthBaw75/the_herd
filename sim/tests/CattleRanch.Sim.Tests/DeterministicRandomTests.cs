using System;
using System.Collections.Generic;
using CattleRanch.Sim.Math;

namespace CattleRanch.Sim.Tests
{
    public class DeterministicRandomTests
    {
        [Fact]
        public void SameSeed_ProducesIdenticalDoubleStream()
        {
            var a = new DeterministicRandom(1234567);
            var b = new DeterministicRandom(1234567);

            for (int i = 0; i < 10_000; i++)
            {
                Assert.Equal(a.NextDouble(), b.NextDouble());
            }
        }

        [Fact]
        public void SameSeed_ProducesIdenticalIntStream()
        {
            var a = new DeterministicRandom(-42);
            var b = new DeterministicRandom(-42);

            for (int i = 0; i < 10_000; i++)
            {
                Assert.Equal(a.NextInt(0, 1000), b.NextInt(0, 1000));
            }
        }

        [Fact]
        public void SameSeed_ProducesIdenticalGaussianStream()
        {
            var a = new DeterministicRandom(99);
            var b = new DeterministicRandom(99);

            for (int i = 0; i < 10_000; i++)
            {
                Assert.Equal(a.NextGaussian(5.0, 2.0), b.NextGaussian(5.0, 2.0));
            }
        }

        [Fact]
        public void DifferentSeeds_DivergeQuickly()
        {
            var a = new DeterministicRandom(1);
            var b = new DeterministicRandom(2);

            var seqA = new List<double>();
            var seqB = new List<double>();
            for (int i = 0; i < 100; i++)
            {
                seqA.Add(a.NextDouble());
                seqB.Add(b.NextDouble());
            }

            Assert.NotEqual(seqA, seqB);
            // First draw already differs — no shared prefix.
            Assert.NotEqual(seqA[0], seqB[0]);
        }

        [Fact]
        public void NextDouble_StaysInUnitInterval()
        {
            var r = new DeterministicRandom(7);
            for (int i = 0; i < 100_000; i++)
            {
                double d = r.NextDouble();
                Assert.InRange(d, 0.0, 1.0);
                Assert.NotEqual(1.0, d); // upper bound is exclusive
            }
        }

        [Fact]
        public void NextInt_RespectsBounds()
        {
            var r = new DeterministicRandom(123);
            for (int i = 0; i < 100_000; i++)
            {
                int v = r.NextInt(-5, 5);
                Assert.InRange(v, -5, 4); // maxExclusive
            }
        }

        [Fact]
        public void NextInt_ThrowsWhenRangeInvalid()
        {
            var r = new DeterministicRandom(1);
            Assert.Throws<ArgumentException>(() => r.NextInt(5, 5));
            Assert.Throws<ArgumentException>(() => r.NextInt(5, 4));
        }

        [Fact]
        public void NextGaussian_ApproximatesRequestedMeanAndStdDev()
        {
            var r = new DeterministicRandom(2024);
            const int n = 200_000;
            double sum = 0.0;
            double sumSq = 0.0;

            for (int i = 0; i < n; i++)
            {
                double g = r.NextGaussian(10.0, 2.0);
                sum += g;
                sumSq += g * g;
            }

            double mean = sum / n;
            double variance = (sumSq / n) - (mean * mean);
            double stdDev = System.Math.Sqrt(variance);

            Assert.InRange(mean, 9.95, 10.05);
            Assert.InRange(stdDev, 1.95, 2.05);
        }
    }
}
