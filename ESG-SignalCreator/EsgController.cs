using System;
using System.Globalization;
using System.Text;
using EsgSignalCreator.Instruments;
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

        /// <summary>Read back the current amplitude, in dBm.</summary>
        public double GetAmplitudeDbm()
        {
            string r = _io.Query(":POWer:LEVel?");
            return double.Parse(r, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        /// <summary>Query the standard event status register / error queue head.</summary>
        public string GetError() => _io.Query(":SYSTem:ERRor?");

        // ---- Dual ARB (Option 001/601 or 002/602) ----

        /// <summary>
        /// Download an I/Q waveform into volatile ARB memory (WFM1) via
        /// <c>:MEMory:DATA "WFM1:&lt;name&gt;",&lt;block&gt;</c>. The payload is interleaved 16-bit,
        /// two's-complement, big-endian (MSB first) — the format the E4438C ARB requires.
        /// </summary>
        public void DownloadWaveform(string segmentName, IqWaveform waveform)
        {
            if (waveform == null) throw new ArgumentNullException(nameof(waveform));
            ValidateSegmentName(segmentName);

            byte[] payload = waveform.ToArbPayload();
            string count = payload.Length.ToString(CultureInfo.InvariantCulture);
            string header = string.Format(CultureInfo.InvariantCulture,
                ":MEMory:DATA \"WFM1:{0}\",#{1}{2}", segmentName, count.Length, count);

            byte[] headerBytes = Encoding.ASCII.GetBytes(header);
            var message = new byte[headerBytes.Length + payload.Length];
            Buffer.BlockCopy(headerBytes, 0, message, 0, headerBytes.Length);
            Buffer.BlockCopy(payload, 0, message, headerBytes.Length, payload.Length);

            _io.WriteBinaryBlock(message);
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

        private static void ValidateSegmentName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A waveform segment name is required.", nameof(name));
            if (name.Length > 23)
                throw new ArgumentException("Segment name must be 23 characters or fewer.", nameof(name));
        }
    }
}
