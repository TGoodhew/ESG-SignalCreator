using System;
using EsgSignalCreator.Arb;
using Xunit;

namespace EsgSignalCreator.Tests.Arb
{
    public class EsgArbEncoderTests
    {
        [Fact]
        public void EncodePayload_is_interleaved_big_endian_int16()
        {
            // DC on I, zero on Q, with no backoff: every I maps to +full scale (0x7FFF), every Q to 0.
            int n = 64;
            var i = new float[n];
            var q = new float[n];
            for (int k = 0; k < n; k++) { i[k] = 1.0f; q[k] = 0.0f; }

            byte[] payload = EsgArbEncoder.EncodePayload(i, q, backoff: 1.0);

            Assert.Equal(n * 4, payload.Length); // 2 bytes I + 2 bytes Q per sample
            for (int k = 0; k < n; k++)
            {
                Assert.Equal(0x7F, payload[4 * k + 0]); // I MSB
                Assert.Equal(0xFF, payload[4 * k + 1]); // I LSB
                Assert.Equal(0x00, payload[4 * k + 2]); // Q MSB
                Assert.Equal(0x00, payload[4 * k + 3]); // Q LSB
            }
        }

        [Fact]
        public void EncodePayload_applies_digital_backoff_to_peak()
        {
            int n = 60;
            var i = new float[n];
            var q = new float[n];
            i[0] = 1.0f; // single peak of magnitude 1

            byte[] payload = EsgArbEncoder.EncodePayload(i, q, backoff: 0.95);

            // 0.95 * 32767 = 31128.65 -> 31129 = 0x7999
            Assert.Equal(0x79, payload[0]);
            Assert.Equal(0x99, payload[1]);
        }

        [Fact]
        public void EncodeBlock_wraps_payload_in_a_definite_length_block()
        {
            int n = 60;
            byte[] block = EsgArbEncoder.EncodeBlock(new float[n], new float[n]);

            // Payload is 60*4 = 240 bytes, so the header is "#3240" (5 bytes).
            Assert.Equal(5 + 240, block.Length);
            Assert.Equal((byte)'#', block[0]);
            Assert.Equal((byte)'3', block[1]);
        }

        [Fact]
        public void EncodePayload_rejects_fewer_than_60_samples()
        {
            var i = new float[59];
            var q = new float[59];
            Assert.Throws<ArgumentException>(() => EsgArbEncoder.EncodePayload(i, q));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1.5)]
        public void EncodePayload_rejects_backoff_outside_unit_interval(double backoff)
        {
            var i = new float[60];
            var q = new float[60];
            Assert.Throws<ArgumentOutOfRangeException>(() => EsgArbEncoder.EncodePayload(i, q, backoff));
        }
    }
}
