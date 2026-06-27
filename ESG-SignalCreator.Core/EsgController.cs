using System;
using System.Globalization;
using EsgSignalCreator.Arb;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Model;
using EsgSignalCreator.Waveform;

namespace EsgSignalCreator
{
    /// <summary>
    /// High-level SCPI helpers for an Agilent/Keysight ESG-series RF signal generator
    /// (e.g. E4438C, E4400-series). Wraps any <see cref="IInstrument"/> transport.
    /// </summary>
    public sealed class EsgController
    {
        private readonly IInstrument _io;

        public EsgController(IInstrument io)
        {
            _io = io ?? throw new ArgumentNullException(nameof(io));
        }

        /// <summary>Query *IDN? — the instrument identification string.</summary>
        public string Identify() => _io.Query("*IDN?");

        /// <summary>Reset to a known state (*RST) and clear status (*CLS).</summary>
        public void Reset()
        {
            _io.Write("*RST");
            _io.Write("*CLS");
        }

        /// <summary>Set the RF carrier frequency, in hertz.</summary>
        public void SetFrequencyHz(double hertz)
        {
            _io.Write(":FREQuency:FIXed " + hertz.ToString("G17", CultureInfo.InvariantCulture) + " Hz");
        }

        /// <summary>Set the RF output amplitude, in dBm.</summary>
        public void SetAmplitudeDbm(double dbm)
        {
            _io.Write(":POWer:LEVel " + dbm.ToString("G17", CultureInfo.InvariantCulture) + " dBm");
        }

        /// <summary>Turn the RF output on or off (:OUTPut:STATe).</summary>
        public void SetRfOutput(bool on)
        {
            _io.Write(":OUTPut:STATe " + (on ? "ON" : "OFF"));
        }

        /// <summary>Enable or disable all modulation (:OUTPut:MODulation:STATe).</summary>
        public void SetModulation(bool on)
        {
            _io.Write(":OUTPut:MODulation:STATe " + (on ? "ON" : "OFF"));
        }

        /// <summary>Read back the current carrier frequency, in hertz.</summary>
        public double GetFrequencyHz()
        {
            string r = _io.Query(":FREQuency:FIXed?");
            return double.Parse(r, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        /// <summary>Query the maximum settable carrier frequency for the installed option, in hertz.</summary>
        public double GetMaxFrequencyHz()
        {
            string r = _io.Query(":FREQuency:FIXed? MAX");
            return double.Parse(r, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        /// <summary>Query the minimum settable carrier frequency, in hertz.</summary>
        public double GetMinFrequencyHz()
        {
            string r = _io.Query(":FREQuency:FIXed? MIN");
            return double.Parse(r, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        /// <summary>Read back the current amplitude, in dBm.</summary>
        public double GetAmplitudeDbm()
        {
            string r = _io.Query(":POWer:LEVel?");
            return double.Parse(r, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        /// <summary>Query the standard event status register / error queue head.</summary>
        public string GetError() => _io.Query(":SYSTem:ERRor?");

        /// <summary>
        /// Enable/disable automatic timebase selection (<c>:ROSCillator:SOURce:AUTO</c>). With auto on,
        /// the ESG locks to a valid external 10 MHz at its REF IN when present and falls back to its
        /// internal timebase otherwise (#75).
        /// </summary>
        public void SetReferenceAuto(bool on) =>
            _io.Write(":ROSCillator:SOURce:AUTO " + (on ? "ON" : "OFF"));

        /// <summary>Read which timebase the ESG is currently using (<c>:ROSCillator:SOURce?</c>).</summary>
        public Instruments.ReferenceSource GetReferenceSource() =>
            Instruments.ReferenceSourceText.Parse(_io.Query(":ROSCillator:SOURce?"));

        // ---- Dual ARB (Option 001/601 or 002/602) ----

        /// <summary>
        /// Download an I/Q waveform into volatile ARB memory (WFM1) via
        /// <c>:MEMory:DATA "WFM1:&lt;name&gt;",&lt;block&gt;</c>. The payload is interleaved 16-bit,
        /// two's-complement, big-endian (MSB first) — the format the E4438C ARB requires.
        /// </summary>
        public void DownloadWaveform(string segmentName, IqWaveform waveform,
            double backoff = EsgArbEncoder.DefaultBackoff)
        {
            if (waveform == null) throw new ArgumentNullException(nameof(waveform));
            ValidateSegmentName(segmentName);
            DownloadPayload(segmentName, EsgArbEncoder.EncodePayload(waveform.I, waveform.Q, backoff));
        }

        /// <summary>
        /// Download a <see cref="WaveformModel"/> (the neutral output of a signal personality) into
        /// volatile ARB memory (WFM1).
        /// </summary>
        public void DownloadWaveform(string segmentName, WaveformModel waveform,
            double backoff = EsgArbEncoder.DefaultBackoff)
        {
            if (waveform == null) throw new ArgumentNullException(nameof(waveform));
            ValidateSegmentName(segmentName);
            DownloadPayload(segmentName, EsgArbEncoder.EncodePayload(waveform.I, waveform.Q, backoff));
        }

        private void DownloadPayload(string segmentName, byte[] payload)
        {
            // Turn the ARB off first so a download never overwrites the segment currently playing
            // (rebuild spec §5.3). The encoded payload is framed as an IEEE-488.2 definite-length
            // block and sent as one bus transaction (END asserted only on the final byte).
            SetArbState(false);
            byte[] message = Ieee4882Block.Message(
                string.Format(CultureInfo.InvariantCulture, ":MEMory:DATA \"WFM1:{0}\",", segmentName),
                payload);
            _io.WriteBinaryBlock(message);
        }

        /// <summary>
        /// Legacy E443xB-compatible download: send I and Q as separate <c>ARBI:</c>/<c>ARBQ:</c>
        /// blocks (the E4438C auto-converts them). Use the primary <see cref="DownloadWaveform(string,IqWaveform,double)"/>
        /// WFM1 path unless a tool specifically needs this fallback (rebuild spec §5.3).
        /// </summary>
        public void DownloadWaveformLegacy(string segmentName, IqWaveform waveform,
            double backoff = EsgArbEncoder.DefaultBackoff)
        {
            if (waveform == null) throw new ArgumentNullException(nameof(waveform));
            ValidateSegmentName(segmentName);

            var i = new float[waveform.Length];
            var q = new float[waveform.Length];
            for (int n = 0; n < waveform.Length; n++) { i[n] = (float)waveform.I[n]; q[n] = (float)waveform.Q[n]; }

            EsgArbEncoder.EncodeChannelsSeparate(i, q, backoff, out byte[] iBytes, out byte[] qBytes);
            SetArbState(false);
            _io.WriteBinaryBlock(Ieee4882Block.Message(
                string.Format(CultureInfo.InvariantCulture, ":MEMory:DATA \"ARBI:{0}\",", segmentName), iBytes));
            _io.WriteBinaryBlock(Ieee4882Block.Message(
                string.Format(CultureInfo.InvariantCulture, ":MEMory:DATA \"ARBQ:{0}\",", segmentName), qBytes));
        }

        /// <summary>
        /// Download a marker stream (one byte per sample, value 1 = marker on) to the segment's
        /// marker file via <c>:MEMory:DATA "MKR1:&lt;name&gt;"</c>. Optional — the instrument supplies
        /// default markers when none is downloaded (rebuild spec §5.1/§5.3).
        /// </summary>
        public void DownloadMarkers(string segmentName, byte[] markers)
        {
            if (markers == null) throw new ArgumentNullException(nameof(markers));
            ValidateSegmentName(segmentName);
            _io.WriteBinaryBlock(Ieee4882Block.Message(
                string.Format(CultureInfo.InvariantCulture, ":MEMory:DATA \"MKR1:{0}\",", segmentName), markers));
        }

        /// <summary>Select a downloaded segment for the dual ARB player (:RADio:ARB:WAVeform).</summary>
        public void SelectWaveform(string segmentName)
        {
            ValidateSegmentName(segmentName);
            _io.Write(":RADio:ARB:WAVeform \"WFM1:" + segmentName + "\"");
        }

        /// <summary>Set the ARB sample (playback) clock in hertz (:RADio:ARB:SCLock:RATE, max 100 MHz).</summary>
        public void SetSampleClockHz(double hertz)
        {
            _io.Write(":RADio:ARB:SCLock:RATE " + hertz.ToString("G17", CultureInfo.InvariantCulture));
        }

        /// <summary>Set ARB waveform runtime scaling, in percent (:RADio:ARB:RSCaling).</summary>
        public void SetRuntimeScaling(double percent)
        {
            _io.Write(":RADio:ARB:RSCaling " + percent.ToString("0.###", CultureInfo.InvariantCulture));
        }

        /// <summary>Enable or disable the arbitrary waveform generator (:RADio:ARB:STATe).</summary>
        public void SetArbState(bool on)
        {
            _io.Write(":RADio:ARB:STATe " + (on ? "ON" : "OFF"));
        }

        /// <summary>
        /// Select a downloaded segment and start it: point the dual ARB at <c>WFM1:&lt;name&gt;</c>,
        /// set the sample clock and runtime scaling, then turn the ARB on. RF output is controlled
        /// separately (see <see cref="SetRfOutput"/>), so this can be armed before enabling RF.
        /// </summary>
        public void PlayWaveform(string segmentName, double sampleClockHz, double runtimeScalingPercent = 70)
        {
            SelectWaveform(segmentName);
            SetSampleClockHz(sampleClockHz);
            SetRuntimeScaling(runtimeScalingPercent);
            SetArbState(true);
        }

        /// <summary>Copy a volatile WFM1 segment into non-volatile ARB storage (NVWFM).</summary>
        public void CopyToNonVolatile(string segmentName)
        {
            ValidateSegmentName(segmentName);
            _io.Write(string.Format(CultureInfo.InvariantCulture,
                ":MEMory:COPY \"WFM1:{0}\",\"NVWFM:{0}\"", segmentName));
        }

        /// <summary>Load a non-volatile NVWFM segment back into volatile WFM1 memory for playback.</summary>
        public void LoadFromNonVolatile(string segmentName)
        {
            ValidateSegmentName(segmentName);
            _io.Write(string.Format(CultureInfo.InvariantCulture,
                ":MEMory:COPY \"NVWFM:{0}\",\"WFM1:{0}\"", segmentName));
        }

        private static void ValidateSegmentName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A waveform segment name is required.", nameof(name));
            if (name.Length > 23)
                throw new ArgumentException("Segment name must be 23 characters or fewer.", nameof(name));
        }
    }
}
