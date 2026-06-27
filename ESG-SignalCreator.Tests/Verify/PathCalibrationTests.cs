using EsgSignalCreator.Instruments;
using EsgSignalCreator.Verify;
using EsgSignalCreator.Visa;
using Xunit;

namespace EsgSignalCreator.Tests.Verify
{
    public class PathCalibrationTests
    {
        /// <summary>Fake analyzer returning a fixed channel-power reading.</summary>
        private sealed class FakeVsa : IInstrument
        {
            public string ResourceName => "GPIB0::17::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) { }
            public string ReadString() => "";
            public string Query(string command)
            {
                if (command == ":READ:CHPower?") return "-10.8,-71.0";
                return command.EndsWith("?") ? "0" : "";
            }
            public void WriteBinaryBlock(byte[] message) { }
            public void Dispose() { }
        }

        [Fact]
        public void Measure_path_loss_is_commanded_minus_measured()
        {
            var vsa = new VsaInstrument(new FakeVsa());
            PathCalibrationResult r = PathCalibration.Measure(vsa, 1e9, -10.0);

            Assert.Equal(1e9, r.CarrierHz, 0);
            Assert.Equal(-10.0, r.CommandedDbm, 6);
            Assert.Equal(-10.8, r.MeasuredDbm, 6);
            Assert.Equal(0.8, r.PathLossDb, 6); // 0.8 dB of inline loss
        }

        [Fact]
        public void FromMeasurement_computes_loss_directly()
        {
            PathCalibrationResult r = PathCalibration.FromMeasurement(1e9, -10.0, -16.0);
            Assert.Equal(6.0, r.PathLossDb, 6);
        }

        [Fact]
        public void Measured_above_commanded_gives_negative_loss()
        {
            PathCalibrationResult r = PathCalibration.FromMeasurement(1e9, -10.0, -9.5);
            Assert.Equal(-0.5, r.PathLossDb, 6);
        }
    }
}
