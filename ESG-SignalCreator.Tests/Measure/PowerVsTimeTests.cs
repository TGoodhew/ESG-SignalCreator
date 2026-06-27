using System.Collections.Generic;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Measure;
using EsgSignalCreator.Measure.Results;
using EsgSignalCreator.Visa;
using Xunit;

namespace EsgSignalCreator.Tests.Measure
{
    public class PowerVsTimeTests
    {
        /// <summary>
        /// Fake analyzer: returns a canned I/Q trace for <c>:READ:WAVeform0?</c> and a canned scalar
        /// set for <c>:FETCh:WAVeform?</c>. Three samples with |I/Q|² = 1, 0.25, 1 → a 6 dB dip in the middle.
        /// </summary>
        private sealed class FakeVsa : IInstrument
        {
            public readonly List<string> Writes = new List<string>();
            public string Trace = "0,1, 0,0.5, 0,1";                 // I,Q pairs; mag^2 = 1, 0.25, 1
            public string Scalars = "-10.0,-13.0,-13.0,-10.0,3.0";   // peak, mean, ...

            public string ResourceName => "GPIB0::17::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) => Writes.Add(command);
            public string ReadString() => "";

            public string Query(string command)
            {
                Writes.Add(command);
                if (command == ":READ:WAVeform0?") return Trace;
                if (command == ":FETCh:WAVeform?") return Scalars;
                return command.EndsWith("?") ? "0" : "";
            }

            public void WriteBinaryBlock(byte[] message) { }
            public void Dispose() { }
        }

        [Fact]
        public void Envelope_is_anchored_to_the_measured_peak_power()
        {
            var io = new FakeVsa();
            PowerVsTimeResult r = PowerVsTime.Measure(new VsaInstrument(io), 1e9, sampleIntervalSeconds: 1e-6);

            Assert.Equal(-10.0, r.PeakPowerDbm, 6);
            Assert.Equal(-13.0, r.MeanPowerDbm, 6);
            Assert.Equal(3, r.PowerDbm.Length);

            // Peak samples map to the measured peak; the 0.25 sample is 10*log10(0.25) = -6.02 dB below.
            Assert.Equal(-10.0, r.PowerDbm[0], 3);
            Assert.Equal(-16.0206, r.PowerDbm[1], 3);
            Assert.Equal(-10.0, r.PowerDbm[2], 3);

            // Time axis honors the supplied sample interval.
            Assert.Equal(0.0, r.TimeSeconds[0], 12);
            Assert.Equal(2e-6, r.TimeSeconds[2], 12);
        }

        [Fact]
        public void Measure_initiates_trace_then_fetches_scalars_from_same_acquisition()
        {
            var io = new FakeVsa();
            PowerVsTime.Measure(new VsaInstrument(io), 1e9);

            Assert.Contains(":INSTrument:SELect BASIC", io.Writes);
            Assert.Contains(":READ:WAVeform0?", io.Writes);
            Assert.Contains(":FETCh:WAVeform?", io.Writes);
        }

        [Fact]
        public void Supplied_mask_is_evaluated_against_the_envelope()
        {
            var io = new FakeVsa();
            // Upper limit -12 dBm across all three samples: the -10 peaks breach it, the -16 dip passes.
            var mask = new PowerMask(new[] { new PowerMaskSegment(0, 3e-6, double.NaN, -12.0) });

            PowerVsTimeResult r = PowerVsTime.Measure(new VsaInstrument(io), 1e9, 1e-6, mask);

            Assert.NotNull(r.Mask);
            Assert.False(r.Mask.Pass);
            Assert.Equal(2, r.Mask.Violations.Count); // samples 0 and 2
        }

        [Fact]
        public void No_mask_leaves_the_verdict_null()
        {
            var io = new FakeVsa();
            PowerVsTimeResult r = PowerVsTime.Measure(new VsaInstrument(io), 1e9);
            Assert.Null(r.Mask);
        }
    }
}
