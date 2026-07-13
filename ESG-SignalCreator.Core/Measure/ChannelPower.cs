using System;
using System.Globalization;
using EsgSignalCreator.Measure.Results;
using EsgSignalCreator.Visa;

namespace EsgSignalCreator.Measure
{
    /// <summary>
    /// E4406A Channel Power measurement (GitHub issue #62). Sets up Basic single-measurement mode at
    /// the requested center/span, optionally constrains the integration bandwidth, then reads the
    /// <c>CHPower</c> scalar result set and maps it to a <see cref="ChannelPowerResult"/>.
    /// </summary>
    public static class ChannelPower
    {
        /// <summary>SCPI mnemonic for the integration bandwidth of the channel-power measurement.</summary>
        private const string IntegrationBandwidthCommand = ":SENSe:CHPower:BANDwidth:INTegration";

        /// <summary>Per-measurement frequency span for Channel Power (the E4406A has no global span).</summary>
        private const string SpanCommand = ":SENSe:CHPower:FREQuency:SPAN";

        /// <summary>SCPI measurement root for Channel Power.</summary>
        private const string Root = "CHPower";

        /// <summary>
        /// Perform a Channel Power measurement.
        /// </summary>
        /// <param name="vsa">Open analyzer facade.</param>
        /// <param name="centerHz">Channel center frequency, in hertz.</param>
        /// <param name="spanHz">Frequency span, in hertz (0 leaves the current span).</param>
        /// <param name="channelBandwidthHz">
        /// Integration bandwidth, in hertz. When &gt; 0 it is written via
        /// <c>:SENSe:CHPower:BANDwidth:INTegration</c> before the read; 0 leaves the analyzer's setting.
        /// </param>
        /// <remarks>
        /// The <c>CHPower</c> scalar result set is <c>[total power dBm, power spectral density dBm/Hz]</c>.
        /// This measurement is cross-model with no SCPI change: the N9010A (SA-mode Channel Power) uses
        /// the same root, the same <c>:SENSe:CHPower:FREQuency:SPAN</c> / <c>:SENSe:CHPower:BANDwidth:INTegration</c>
        /// commands, and the same <c>[0]=power, [1]=PSD</c> ordering at n=1 (SA Reference 9018-06099); the
        /// only difference — SA vs Basic mode — is handled by <see cref="BasicMeasurement.Setup"/>. A
        /// short/empty response yields <see cref="double.NaN"/> for the missing field(s) rather than throwing.
        /// </remarks>
        public static ChannelPowerResult Measure(
            VsaInstrument vsa,
            double centerHz,
            double spanHz,
            double channelBandwidthHz = 0)
        {
            if (vsa == null) throw new ArgumentNullException(nameof(vsa));

            var basic = new BasicMeasurement(vsa);
            basic.Setup(VsaMeasurement.ChannelPower, centerHz);

            if (spanHz > 0)
            {
                vsa.Write(SpanCommand + " " + spanHz.ToString("G17", CultureInfo.InvariantCulture) + " Hz");
            }
            if (channelBandwidthHz > 0)
            {
                vsa.Write(IntegrationBandwidthCommand + " " +
                          channelBandwidthHz.ToString("G17", CultureInfo.InvariantCulture) + " Hz");
            }

            double[] scalars = basic.Read(Root);

            return new ChannelPowerResult
            {
                Measurement = Root,
                Raw = scalars,
                TotalPowerDbm = scalars.Length > 0 ? scalars[0] : double.NaN,
                PowerSpectralDensityDbmHz = scalars.Length > 1 ? scalars[1] : double.NaN
            };
        }
    }
}
