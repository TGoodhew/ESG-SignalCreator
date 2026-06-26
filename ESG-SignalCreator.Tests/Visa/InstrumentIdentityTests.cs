using EsgSignalCreator.Visa;
using Xunit;

namespace EsgSignalCreator.Tests.Visa
{
    public class InstrumentIdentityTests
    {
        [Fact]
        public void Parse_extracts_all_four_fields_from_a_full_IDN()
        {
            var id = InstrumentIdentity.Parse("Agilent Technologies, E4438C, US44440123, C.05.84");

            Assert.Equal("Agilent Technologies", id.Manufacturer);
            Assert.Equal("E4438C", id.Model);
            Assert.Equal("US44440123", id.Serial);
            Assert.Equal("C.05.84", id.FirmwareRevision);
        }

        [Fact]
        public void Parse_tolerates_a_two_field_string()
        {
            var id = InstrumentIdentity.Parse("Keysight,N5182B");

            Assert.Equal("Keysight", id.Manufacturer);
            Assert.Equal("N5182B", id.Model);
            Assert.Equal("", id.Serial);
            Assert.Equal("", id.FirmwareRevision);
        }

        [Fact]
        public void Parse_tolerates_null_and_empty()
        {
            var fromNull = InstrumentIdentity.Parse(null);
            var fromEmpty = InstrumentIdentity.Parse("");

            Assert.Equal("", fromNull.Model);
            Assert.Equal("", fromEmpty.Manufacturer);
        }
    }
}
