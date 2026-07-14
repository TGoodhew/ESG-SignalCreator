using System;
using System.Collections.Generic;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Visa;
using Xunit;

namespace EsgSignalCreator.Tests.Visa
{
    /// <summary>#129: N9010A measurement reads wait for completion via SRQ (Status-Byte MAV), so an
    /// auto-alignment of any length doesn't trip a fixed timeout. E4406A uses the plain blocking read.</summary>
    public class ServiceRequestCompletionTests
    {
        // Simulates an analyzer that only signals MAV after a number of SRQ waits (an "alignment").
        private sealed class FakeSrqVsa : IInstrument, ISupportsServiceRequest
        {
            public readonly List<string> Writes = new List<string>();
            public string IdnResponse = "Keysight Technologies,N9010A,MY1,A.20.14";
            public string ScalarResponse = "-10.0,-70.0";
            public int WaitsBeforeReady = 3; // 3 "still aligning" waits, then MAV
            public int WaitCalls;
            public bool SrqEnabled;

            public string ResourceName => "TCPIP0::n9010a::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) => Writes.Add(command);
            public string ReadString() => ScalarResponse;
            public string Query(string command)
            {
                Writes.Add(command);
                if (command == "*IDN?") return IdnResponse;
                return command.EndsWith("?") ? ScalarResponse : "";
            }
            public void WriteBinaryBlock(byte[] message) { }
            public void Dispose() { }

            public void EnableServiceRequest() => SrqEnabled = true;
            public void DisableServiceRequest() => SrqEnabled = false;
            public bool WaitForServiceRequest(int timeoutMs) => ++WaitCalls >= WaitsBeforeReady; // false until "aligned"
            public int ReadStatusByte() => 0x10; // MAV set once we report an SRQ
        }

        [Fact]
        public void N9010a_read_waits_via_srq_then_returns_the_response()
        {
            var io = new FakeSrqVsa { WaitsBeforeReady = 3, ScalarResponse = "-10.0,-70.0" };
            var vsa = new VsaInstrument(io);

            string result = vsa.QueryMeasurement(":READ:CHPower?");

            Assert.Equal("-10.0,-70.0", result);
            Assert.Equal(3, io.WaitCalls);                 // rode out 2 "aligning" waits, ready on the 3rd
            Assert.Contains("*SRE 16", io.Writes);         // armed MAV -> SRQ
            Assert.Contains(":READ:CHPower?", io.Writes);
            Assert.Contains("*SRE 0", io.Writes);          // disarmed afterwards
            Assert.False(io.SrqEnabled);                   // SRQ disabled in finally
        }

        [Fact]
        public void N9010a_read_times_out_only_after_the_overall_deadline()
        {
            // MAV never arrives -> the re-arming loop eventually gives up with a clear message.
            var io = new FakeSrqVsa { WaitsBeforeReady = int.MaxValue };
            var vsa = new VsaInstrument(io);

            TimeoutException ex = Assert.Throws<TimeoutException>(() => vsa.QueryMeasurement(":READ:CHPower?"));
            Assert.Contains("alignment", ex.Message);
            Assert.False(io.SrqEnabled); // still cleaned up
        }

        [Fact]
        public void MeasurementTrace_receives_command_and_raw_response()
        {
            var io = new FakeSrqVsa { WaitsBeforeReady = 1, ScalarResponse = "1,2,3,4,5,6,7,8,9,10" };
            var vsa = new VsaInstrument(io);
            string cmd = null, resp = null;
            vsa.MeasurementTrace = (c, r) => { cmd = c; resp = r; };

            vsa.QueryMeasurement(":READ:PSTatistic2?");

            Assert.Equal(":READ:PSTatistic2?", cmd);
            Assert.Equal("1,2,3,4,5,6,7,8,9,10", resp); // 10 values -> scalars (vs a 5001-point trace)
        }

        [Fact]
        public void E4406a_read_uses_a_plain_blocking_query_no_srq()
        {
            var io = new FakeSrqVsa { IdnResponse = "Agilent Technologies, E4406A, US1, A.05.00" };
            var vsa = new VsaInstrument(io);

            string result = vsa.QueryMeasurement(":READ:CHPower?");

            Assert.Equal("-10.0,-70.0", result);
            Assert.Equal(0, io.WaitCalls);            // no SRQ waiting on the E4406A path
            Assert.DoesNotContain("*SRE 16", io.Writes);
        }
    }
}
