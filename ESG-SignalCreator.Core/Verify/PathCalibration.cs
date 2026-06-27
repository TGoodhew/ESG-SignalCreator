using System;
using System.Globalization;
using EsgSignalCreator.Measure;
using EsgSignalCreator.Measure.Results;
using EsgSignalCreator.Visa;

namespace EsgSignalCreator.Verify
{
    /// <summary>
    /// One path-calibration measurement (#72): the difference between the ESG's commanded carrier
    /// power and the power the E4406A actually measured at its input. Captures cable loss + the
    /// analyzer's absolute-amplitude offset as a single <see cref="PathLossDb"/> correction.
    /// </summary>
    public sealed class PathCalibrationResult
    {
        public PathCalibrationResult(double carrierHz, double commandedDbm, double measuredDbm)
        {
            CarrierHz = carrierHz;
            CommandedDbm = commandedDbm;
            MeasuredDbm = measuredDbm;
        }

        /// <summary>Carrier frequency the calibration was taken at, in hertz.</summary>
        public double CarrierHz { get; }

        /// <summary>Power commanded at the ESG output, in dBm.</summary>
        public double CommandedDbm { get; }

        /// <summary>Channel power the analyzer measured, in dBm.</summary>
        public double MeasuredDbm { get; }

        /// <summary>
        /// Inline path loss, in dB: <c>commanded − measured</c>. A positive value means the signal
        /// lost level between the ESG output and the analyzer input (the usual case for a cable).
        /// This is the value the verification harness subtracts from a commanded level to predict
        /// the analyzer reading, so feeding it back into the profile makes future verifies self-consistent.
        /// </summary>
        public double PathLossDb => CommandedDbm - MeasuredDbm;

        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "Path cal @ {0:0.###} MHz: commanded {1:0.###} dBm, measured {2:0.###} dBm → path loss {3:+0.###;-0.###} dB",
            CarrierHz / 1e6, CommandedDbm, MeasuredDbm, PathLossDb);
    }

    /// <summary>
    /// Path-calibration helper (#72): measure a known carrier on the analyzer and derive the
    /// inline path loss. The orchestration (driving the ESG to a clean carrier at the commanded
    /// level, RF on, modulation off) lives in the caller; this layer does the measurement + math
    /// so it stays unit-testable against a fake analyzer.
    /// </summary>
    public static class PathCalibration
    {
        /// <summary>Compose a result directly from a known commanded and measured level.</summary>
        public static PathCalibrationResult FromMeasurement(double carrierHz, double commandedDbm, double measuredDbm) =>
            new PathCalibrationResult(carrierHz, commandedDbm, measuredDbm);

        /// <summary>
        /// Measure channel power at <paramref name="carrierHz"/> on a connected analyzer and return
        /// the path-loss correction relative to <paramref name="commandedDbm"/>. The carrier must
        /// already be playing at the commanded level when this is called.
        /// </summary>
        public static PathCalibrationResult Measure(
            VsaInstrument vsa, double carrierHz, double commandedDbm, double spanHz = 5e6)
        {
            if (vsa == null) throw new ArgumentNullException(nameof(vsa));
            double span = spanHz > 0 ? spanHz : 5e6;
            ChannelPowerResult cp = ChannelPower.Measure(vsa, carrierHz, span, span);
            return new PathCalibrationResult(carrierHz, commandedDbm, cp.TotalPowerDbm);
        }
    }
}
