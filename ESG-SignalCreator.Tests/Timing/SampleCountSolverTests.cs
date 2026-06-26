using System;
using EsgSignalCreator.Timing;
using Xunit;

namespace EsgSignalCreator.Tests.Timing
{
    public class SampleCountSolverTests
    {
        [Fact]
        public void Time_basis_multiplies_seconds_by_sample_rate()
        {
            // 1 ms at 10 MHz = 10 000 samples.
            Assert.Equal(10000, SampleCountSolver.Solve(LengthBasis.Time, 1e-3, 10e6));
        }

        [Fact]
        public void Samples_basis_is_passthrough()
        {
            Assert.Equal(4096, SampleCountSolver.Solve(LengthBasis.Samples, 4096, 10e6));
        }

        [Fact]
        public void Symbols_basis_uses_samples_per_symbol()
        {
            // fs=10 MHz, symbol rate=1 MHz -> 10 samples/symbol; 100 symbols -> 1000 samples.
            Assert.Equal(1000, SampleCountSolver.Solve(LengthBasis.Symbols, 100, 10e6, symbolRateHz: 1e6));
        }

        [Fact]
        public void Result_is_clamped_to_the_minimum()
        {
            Assert.Equal(60, SampleCountSolver.Solve(LengthBasis.Samples, 5, 10e6, minSamples: 60));
        }

        [Fact]
        public void Symbols_basis_requires_a_symbol_rate()
        {
            Assert.Throws<ArgumentException>(() => SampleCountSolver.Solve(LengthBasis.Symbols, 100, 10e6));
        }

        [Fact]
        public void Round_trips_through_seconds_and_symbols()
        {
            Assert.Equal(1e-3, SampleCountSolver.ToSeconds(10000, 10e6), 12);
            Assert.Equal(100.0, SampleCountSolver.ToSymbols(1000, 10e6, 1e6), 9);
        }
    }
}
