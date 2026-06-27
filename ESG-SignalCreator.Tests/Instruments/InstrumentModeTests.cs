using System.Collections.Generic;
using System.Linq;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Visa;
using Xunit;

namespace EsgSignalCreator.Tests.Instruments
{
    public class InstrumentModeTests
    {
        private sealed class FakeIo : IInstrument
        {
            public readonly List<string> Writes = new List<string>();
            public string Catalog = "\"BASIC\",\"GSM\",\"CDMA\"";
            public string Mode = "BASIC";

            public string ResourceName => "GPIB0::17::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) => Writes.Add(command);
            public string ReadString() => "";

            public string Query(string command)
            {
                Writes.Add(command);
                if (command == ":INSTrument:CATalog?") return Catalog;
                if (command == ":INSTrument:SELect?") return "\"" + Mode + "\"";
                return command.EndsWith("?") ? "0" : "";
            }

            public void WriteBinaryBlock(byte[] message) { }
            public void Dispose() { }
        }

        [Fact]
        public void ByMnemonic_is_case_insensitive_and_flags_base_modes()
        {
            Assert.Equal("Basic", InstrumentModeCatalog.ByMnemonic("basic").DisplayName);
            Assert.False(InstrumentModeCatalog.ByMnemonic("BASIC").IsStandardPersonality);
            Assert.False(InstrumentModeCatalog.ByMnemonic("SERVICE").IsStandardPersonality);
            Assert.True(InstrumentModeCatalog.ByMnemonic("WCDMA").IsStandardPersonality);
            Assert.Null(InstrumentModeCatalog.ByMnemonic("NOPE"));
        }

        [Fact]
        public void Resolve_maps_known_names_and_passes_unknowns_through()
        {
            var modes = InstrumentModeCatalog.Resolve(new[] { "BASIC", "GSM", "FUTUREMODE" });
            Assert.Equal(3, modes.Count);
            Assert.Equal("GSM", modes[1].DisplayName);
            InstrumentMode future = modes.First(m => m.Mnemonic == "FUTUREMODE");
            Assert.True(future.IsStandardPersonality); // unknown -> treated as a selectable personality
        }

        [Fact]
        public void StandardPersonalities_excludes_basic_and_service()
        {
            var std = InstrumentModeCatalog.StandardPersonalities(new[] { "BASIC", "SERVICE", "GSM", "WCDMA" });
            Assert.Equal(2, std.Count);
            Assert.DoesNotContain(std, m => m.Mnemonic == "BASIC");
            Assert.Contains(std, m => m.Mnemonic == "GSM");
        }

        [Fact]
        public void ModeCatalog_parses_quoted_csv_from_the_analyzer()
        {
            var vsa = new VsaInstrument(new FakeIo());
            string[] cat = vsa.ModeCatalog();
            Assert.Equal(new[] { "BASIC", "GSM", "CDMA" }, cat);
        }

        [Fact]
        public void SelectMode_emits_instrument_select()
        {
            var io = new FakeIo();
            new VsaInstrument(io).SelectMode("GSM");
            Assert.Contains(":INSTrument:SELect GSM", io.Writes);
        }

        [Fact]
        public void GetMode_strips_quotes()
        {
            var vsa = new VsaInstrument(new FakeIo { Mode = "WCDMA" });
            Assert.Equal("WCDMA", vsa.GetMode());
        }

        [Fact]
        public void Resolve_from_a_units_catalog_yields_selectable_personalities()
        {
            var vsa = new VsaInstrument(new FakeIo { Catalog = "\"BASIC\",\"GSM\",\"WCDMA\",\"CDMA2K\"" });
            var std = InstrumentModeCatalog.StandardPersonalities(vsa.ModeCatalog());
            Assert.Equal(new[] { "GSM", "WCDMA", "CDMA2K" }, std.Select(m => m.Mnemonic).ToArray());
        }
    }
}
