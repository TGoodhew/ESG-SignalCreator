using System.Collections.Generic;
using EsgSignalCreator;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Visa;
using Xunit;

namespace EsgSignalCreator.Tests.Instruments
{
    public class ReferenceLockTests
    {
        /// <summary>Records writes; answers ROSCillator source queries from a settable field.</summary>
        private sealed class FakeIo : IInstrument
        {
            public readonly List<string> Writes = new List<string>();
            public string RefResponse = "INT";

            public string ResourceName => "GPIB0::19::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) => Writes.Add(command);
            public string ReadString() => "";

            public string Query(string command)
            {
                Writes.Add(command);
                if (command.EndsWith("ROSCillator:SOURce?")) return RefResponse;
                return command.EndsWith("?") ? "0" : "";
            }

            public void WriteBinaryBlock(byte[] message) { }
            public void Dispose() { }
        }

        [Fact]
        public void Source_text_round_trips()
        {
            Assert.Equal("EXT", ReferenceSourceText.Scpi(ReferenceSource.External));
            Assert.Equal("INT", ReferenceSourceText.Scpi(ReferenceSource.Internal));
            Assert.Equal(ReferenceSource.External, ReferenceSourceText.Parse("EXTernal\n"));
            Assert.Equal(ReferenceSource.External, ReferenceSourceText.Parse("\"EXT\""));
            Assert.Equal(ReferenceSource.Internal, ReferenceSourceText.Parse("INT"));
            Assert.Equal(ReferenceSource.Internal, ReferenceSourceText.Parse(""));
        }

        [Fact]
        public void Vsa_set_reference_emits_sense_roscillator_source()
        {
            var io = new FakeIo();
            new VsaInstrument(io).SetReferenceSource(ReferenceSource.External);
            Assert.Contains(":SENSe:ROSCillator:SOURce EXT", io.Writes);
        }

        [Fact]
        public void Esg_set_reference_auto_emits_roscillator_source_auto()
        {
            var io = new FakeIo();
            new EsgController(io).SetReferenceAuto(true);
            Assert.Contains(":ROSCillator:SOURce:AUTO ON", io.Writes);
        }

        [Fact]
        public void Apply_common_external_sets_esg_auto_and_vsa_external()
        {
            var esgIo = new FakeIo();
            var vsaIo = new FakeIo();
            ReferenceLock.Apply(new EsgController(esgIo), new VsaInstrument(vsaIo), ReferenceScheme.CommonExternal);

            Assert.Contains(":ROSCillator:SOURce:AUTO ON", esgIo.Writes);
            Assert.Contains(":SENSe:ROSCillator:SOURce EXT", vsaIo.Writes);
        }

        [Fact]
        public void Apply_independent_sets_vsa_internal()
        {
            var esgIo = new FakeIo();
            var vsaIo = new FakeIo();
            ReferenceLock.Apply(new EsgController(esgIo), new VsaInstrument(vsaIo), ReferenceScheme.Independent);
            Assert.Contains(":SENSe:ROSCillator:SOURce INT", vsaIo.Writes);
        }

        [Fact]
        public void Read_reports_locked_when_both_external()
        {
            var esgIo = new FakeIo { RefResponse = "EXT" };
            var vsaIo = new FakeIo { RefResponse = "EXT" };
            ReferenceStatus st = ReferenceLock.Read(new EsgController(esgIo), new VsaInstrument(vsaIo));

            Assert.Equal(ReferenceSource.External, st.Esg);
            Assert.Equal(ReferenceSource.External, st.Vsa);
            Assert.True(st.Locked);
        }

        [Fact]
        public void Read_not_locked_when_one_internal()
        {
            var esgIo = new FakeIo { RefResponse = "INT" };
            var vsaIo = new FakeIo { RefResponse = "EXT" };
            ReferenceStatus st = ReferenceLock.Read(new EsgController(esgIo), new VsaInstrument(vsaIo));
            Assert.False(st.Locked);
        }
    }
}
