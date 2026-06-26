using System.Text;
using EsgSignalCreator.Arb;
using Xunit;

namespace EsgSignalCreator.Tests.Arb
{
    public class Ieee4882BlockTests
    {
        [Theory]
        [InlineData(2400, "#42400")] // canonical example from the rebuild spec §5.3
        [InlineData(240, "#3240")]
        [InlineData(8, "#18")]
        [InlineData(0, "#10")]
        public void BuildHeader_matches_definite_length_format(int payloadBytes, string expected)
        {
            string header = Encoding.ASCII.GetString(Ieee4882Block.BuildHeader(payloadBytes));
            Assert.Equal(expected, header);
        }

        [Fact]
        public void Frame_prepends_header_to_payload()
        {
            var payload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            byte[] block = Ieee4882Block.Frame(payload);

            // "#14" header (1 length digit, value 4) then the 4 payload bytes.
            Assert.Equal(new byte[] { 0x23, 0x31, 0x34, 0xDE, 0xAD, 0xBE, 0xEF }, block);
        }

        [Fact]
        public void Message_concatenates_command_prefix_then_block()
        {
            byte[] msg = Ieee4882Block.Message(":MEM:DATA \"WFM1:seg\",", new byte[] { 0x01, 0x02 });
            string text = Encoding.ASCII.GetString(msg);

            Assert.Equal(":MEM:DATA \"WFM1:seg\",#12\x01\x02", text);
        }
    }
}
