using EsgSignalCreator;
using EsgSignalCreator.Instruments;
using Xunit;

namespace EsgSignalCreator.Tests.Control
{
    public class EsgControllerFrequencyTests
    {
        private sealed class FakeInstrument : IInstrument
        {
            public string ResourceName => "FAKE";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) { }
            public string ReadString() => "";
            public string Query(string command)
            {
                if (command == ":FREQuency:FIXed? MAX") return "3.0E+9";
                if (command == ":FREQuency:FIXed? MIN") return "250000";
                return "0";
            }
            public void WriteBinaryBlock(byte[] message) { }
            public void Dispose() { }
        }

        [Fact]
        public void GetMaxAndMinFrequency_parse_the_option_limits()
        {
            var esg = new EsgController(new FakeInstrument());
            Assert.Equal(3.0e9, esg.GetMaxFrequencyHz());
            Assert.Equal(250000.0, esg.GetMinFrequencyHz());
        }
    }
}
