using System;
using System.Globalization;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Waveform;

namespace EsgSignalCreator
{
    public partial class MainForm : Form
    {
        private const string InterfaceVisa = "NI-VISA";
        private const string InterfaceGpib = "NI-488.2 (GPIB)";

        private IInstrument _instrument;
        private EsgController _esg;
        private WaveformResult _waveform;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            cmbInterface.Items.AddRange(new object[] { InterfaceVisa, InterfaceGpib });
            cmbInterface.SelectedIndex = 0;

            cmbFreqUnit.Items.AddRange(new object[] { "Hz", "kHz", "MHz", "GHz" });
            cmbFreqUnit.SelectedItem = "MHz";

            numAmplitude.Value = -10.0m;

            cmbSigType.Items.AddRange(new object[] { "Single tone (offset CW)", "AM", "FM", "PM" });
            cmbSampleRateUnit.Items.AddRange(new object[] { "Hz", "kHz", "MHz" });
            cmbSampleRateUnit.SelectedItem = "MHz";
            cmbSigType.SelectedIndex = 0; // triggers cmbSigType_SelectedIndexChanged

            SetupCharts();

            Log("Ready. Select an interface and connect to an ESG signal generator.");
        }

        private void cmbInterface_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool isGpib = (string)cmbInterface.SelectedItem == InterfaceGpib;
            lblBoard.Enabled = isGpib;
            numBoard.Enabled = isGpib;
            lblAddress.Enabled = isGpib;
            numAddress.Enabled = isGpib;
            lblResource.Enabled = !isGpib;
            cmbResource.Enabled = !isGpib;
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            cmbResource.Items.Clear();
            try
            {
                bool isGpib = (string)cmbInterface.SelectedItem == InterfaceGpib;
                int count = 0;
                if (isGpib)
                {
                    foreach (string r in Gpib488Instrument.FindListeners((int)numBoard.Value))
                    {
                        cmbResource.Items.Add(r);
                        count++;
                    }
                }
                else
                {
                    foreach (string r in VisaInstrument.FindResources())
                    {
                        cmbResource.Items.Add(r);
                        count++;
                    }
                }

                if (count > 0)
                {
                    cmbResource.SelectedIndex = 0;
                    Log(string.Format("Found {0} instrument(s).", count));
                }
                else
                {
                    Log("No instruments found.");
                }
            }
            catch (Exception ex)
            {
                Log("Find failed: " + ex.Message);
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                bool isGpib = (string)cmbInterface.SelectedItem == InterfaceGpib;
                if (isGpib)
                {
                    _instrument = new Gpib488Instrument((int)numBoard.Value, (byte)numAddress.Value);
                }
                else
                {
                    string resource = cmbResource.Text.Trim();
                    if (string.IsNullOrEmpty(resource))
                    {
                        Log("Enter or select a VISA resource first.");
                        return;
                    }
                    _instrument = new VisaInstrument(resource);
                }

                _esg = new EsgController(_instrument);
                SetConnectedState(true);
                Log("Connected to " + _instrument.ResourceName);

                try
                {
                    Log("IDN: " + _esg.Identify());
                }
                catch (Exception ex)
                {
                    Log("Connected, but *IDN? failed: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Log("Connect failed: " + ex.Message);
                CleanupInstrument();
                SetConnectedState(false);
            }
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            CleanupInstrument();
            SetConnectedState(false);
            Log("Disconnected.");
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            Guarded(() =>
            {
                double hz = (double)numFrequency.Value * FrequencyMultiplier();
                _esg.SetFrequencyHz(hz);
                Log(string.Format(CultureInfo.InvariantCulture, "Set frequency -> {0} Hz", hz));

                double dbm = (double)numAmplitude.Value;
                _esg.SetAmplitudeDbm(dbm);
                Log(string.Format(CultureInfo.InvariantCulture, "Set amplitude -> {0} dBm", dbm));

                _esg.SetModulation(chkModOn.Checked);
                Log("Modulation -> " + (chkModOn.Checked ? "ON" : "OFF"));

                _esg.SetRfOutput(chkRfOn.Checked);
                Log("RF output -> " + (chkRfOn.Checked ? "ON" : "OFF"));

                string err = _esg.GetError();
                Log("Status: " + err);
            });
        }

        private void btnReadback_Click(object sender, EventArgs e)
        {
            Guarded(() =>
            {
                double hz = _esg.GetFrequencyHz();
                double dbm = _esg.GetAmplitudeDbm();
                Log(string.Format(CultureInfo.InvariantCulture,
                    "Read back: frequency = {0} Hz, amplitude = {1} dBm", hz, dbm));
            });
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            Guarded(() =>
            {
                _esg.Reset();
                Log("Instrument reset (*RST, *CLS).");
            });
        }

        private void btnIdn_Click(object sender, EventArgs e)
        {
            Guarded(() => Log("IDN: " + _esg.Identify()));
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            string cmd = txtCommand.Text.Trim();
            if (cmd.Length == 0) return;
            Guarded(() =>
            {
                _instrument.Write(cmd);
                Log("> " + cmd);
            });
        }

        private void btnQuery_Click(object sender, EventArgs e)
        {
            string cmd = txtCommand.Text.Trim();
            if (cmd.Length == 0) return;
            Guarded(() =>
            {
                string r = _instrument.Query(cmd);
                Log("> " + cmd);
                Log("< " + r);
            });
        }

        private void btnClearLog_Click(object sender, EventArgs e)
        {
            txtLog.Clear();
        }

        // ---- ARB Signal Builder ----

        private void cmbSigType_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (SelectedSignalType())
            {
                case SignalType.SingleTone:
                    lblParam1.Text = "Offset:"; lblParam1Unit.Text = "Hz";
                    numParam1.Minimum = -100000000m; numParam1.Maximum = 100000000m; numParam1.Value = 100000m;
                    SetParam2(false, "—", "", 0m, 0m, 0m);
                    break;
                case SignalType.Am:
                    lblParam1.Text = "Rate:"; lblParam1Unit.Text = "Hz";
                    numParam1.Minimum = 0m; numParam1.Maximum = 100000000m; numParam1.Value = 1000m;
                    SetParam2(true, "Depth:", "%", 0m, 100m, 50m);
                    break;
                case SignalType.Fm:
                    lblParam1.Text = "Rate:"; lblParam1Unit.Text = "Hz";
                    numParam1.Minimum = 0m; numParam1.Maximum = 100000000m; numParam1.Value = 1000m;
                    SetParam2(true, "Deviation:", "Hz", 0m, 100000000m, 10000m);
                    break;
                case SignalType.Pm:
                    lblParam1.Text = "Rate:"; lblParam1Unit.Text = "Hz";
                    numParam1.Minimum = 0m; numParam1.Maximum = 100000000m; numParam1.Value = 1000m;
                    SetParam2(true, "Deviation:", "deg", 0m, 100000m, 90m);
                    break;
            }
        }

        private void SetParam2(bool enabled, string label, string unit, decimal min, decimal max, decimal val)
        {
            lblParam2.Enabled = enabled;
            numParam2.Enabled = enabled;
            lblParam2Unit.Enabled = enabled;
            lblParam2.Text = label;
            lblParam2Unit.Text = unit;
            if (enabled)
            {
                numParam2.Minimum = min;
                numParam2.Maximum = max;
                numParam2.Value = val;
            }
        }

        private void btnGenerate_Click(object sender, EventArgs e)
        {
            try
            {
                double fs = ArbSampleRateHz();
                if (fs > 100e6)
                {
                    Log("ARB sample clock exceeds the E4438C maximum of 100 MHz.");
                    return;
                }

                WaveformSpec spec = BuildSpec();
                if (spec.Type == SignalType.SingleTone && Math.Abs(spec.OffsetHz) > fs / 2)
                {
                    Log(string.Format(CultureInfo.InvariantCulture,
                        "Offset {0} Hz exceeds Nyquist (±{1} Hz) for this sample clock.", spec.OffsetHz, fs / 2));
                    return;
                }

                _waveform = WaveformGenerator.Generate(spec);
                IqWaveform wf = _waveform.Waveform;
                UpdatePreview(wf);

                double durationUs = wf.Length / fs * 1e6;
                lblArbInfo.Text = _waveform.Summary + Environment.NewLine
                    + string.Format(CultureInfo.InvariantCulture, "Sample clock: {0:0.######} MHz", fs / 1e6) + Environment.NewLine
                    + string.Format(CultureInfo.InvariantCulture, "Loop duration: {0:0.###} µs", durationUs) + Environment.NewLine
                    + string.Format(CultureInfo.InvariantCulture, "Download size: {0:n0} bytes", (long)wf.Length * 4);

                Log("Generated: " + _waveform.Summary);
                btnDownloadPlay.Enabled = _instrument != null && _instrument.IsConnected;
            }
            catch (Exception ex)
            {
                Log("Generate failed: " + ex.Message);
            }
        }

        private void btnDownloadPlay_Click(object sender, EventArgs e)
        {
            if (_waveform == null) { Log("Generate a waveform first."); return; }
            Guarded(() =>
            {
                string name = txtSegName.Text.Trim();
                IqWaveform wf = _waveform.Waveform;

                Log(string.Format(CultureInfo.InvariantCulture,
                    "Downloading '{0}' ({1:n0} samples) to WFM1...", name, wf.Length));
                _esg.DownloadWaveform(name, wf);
                _esg.SelectWaveform(name);
                _esg.SetSampleClockHz(wf.SampleRateHz);
                _esg.SetRuntimeScaling((double)numScaling.Value);
                _esg.SetArbState(true);

                double hz = (double)numFrequency.Value * FrequencyMultiplier();
                double dbm = (double)numAmplitude.Value;
                _esg.SetFrequencyHz(hz);
                _esg.SetAmplitudeDbm(dbm);
                _esg.SetRfOutput(true);

                btnArbOff.Enabled = true;
                chkRfOn.Checked = true;

                string err = _esg.GetError();
                Log(string.Format(CultureInfo.InvariantCulture,
                    "Playing '{0}' on {1:0.######} MHz carrier at {2:0.##} dBm. Status: {3}",
                    name, hz / 1e6, dbm, err));
            });
        }

        private void btnArbOff_Click(object sender, EventArgs e)
        {
            Guarded(() =>
            {
                _esg.SetArbState(false);
                btnArbOff.Enabled = false;
                Log("ARB turned off.");
            });
        }

        private SignalType SelectedSignalType()
        {
            switch (cmbSigType.SelectedIndex)
            {
                case 1: return SignalType.Am;
                case 2: return SignalType.Fm;
                case 3: return SignalType.Pm;
                default: return SignalType.SingleTone;
            }
        }

        private double ArbSampleRateHz()
        {
            double mult;
            switch ((string)cmbSampleRateUnit.SelectedItem)
            {
                case "Hz": mult = 1d; break;
                case "kHz": mult = 1e3; break;
                default: mult = 1e6; break;
            }
            return (double)numSampleRate.Value * mult;
        }

        private WaveformSpec BuildSpec()
        {
            var spec = new WaveformSpec
            {
                Type = SelectedSignalType(),
                SampleRateHz = ArbSampleRateHz(),
                TargetLength = (int)numLength.Value
            };
            switch (spec.Type)
            {
                case SignalType.SingleTone:
                    spec.OffsetHz = (double)numParam1.Value;
                    break;
                case SignalType.Am:
                    spec.RateHz = (double)numParam1.Value;
                    spec.AmDepthPercent = (double)numParam2.Value;
                    break;
                case SignalType.Fm:
                    spec.RateHz = (double)numParam1.Value;
                    spec.FmDeviationHz = (double)numParam2.Value;
                    break;
                case SignalType.Pm:
                    spec.RateHz = (double)numParam1.Value;
                    spec.PmDeviationDeg = (double)numParam2.Value;
                    break;
            }
            return spec;
        }

        private void SetupCharts()
        {
            chartIq.ChartAreas.Clear();
            var caIq = new ChartArea("iq");
            caIq.AxisX.Title = "Sample";
            caIq.AxisY.Title = "Amplitude (norm.)";
            caIq.AxisX.MajorGrid.LineColor = System.Drawing.Color.Gainsboro;
            caIq.AxisY.MajorGrid.LineColor = System.Drawing.Color.Gainsboro;
            chartIq.ChartAreas.Add(caIq);
            chartIq.Legends.Clear();
            chartIq.Legends.Add(new Legend("leg") { Docking = Docking.Top });
            chartIq.Titles.Clear();
            chartIq.Titles.Add("I / Q  (time domain)");

            chartSpectrum.ChartAreas.Clear();
            var caSp = new ChartArea("sp");
            caSp.AxisX.Title = "Frequency offset (MHz)";
            caSp.AxisY.Title = "Magnitude (dB)";
            caSp.AxisY.Minimum = -120;
            caSp.AxisY.Maximum = 5;
            caSp.AxisX.MajorGrid.LineColor = System.Drawing.Color.Gainsboro;
            caSp.AxisY.MajorGrid.LineColor = System.Drawing.Color.Gainsboro;
            chartSpectrum.ChartAreas.Add(caSp);
            chartSpectrum.Titles.Clear();
            chartSpectrum.Titles.Add("Baseband spectrum (centered on carrier)");
        }

        private void UpdatePreview(IqWaveform wf)
        {
            chartIq.Series.Clear();
            var sI = new Series("I") { ChartType = SeriesChartType.FastLine, Color = System.Drawing.Color.RoyalBlue };
            var sQ = new Series("Q") { ChartType = SeriesChartType.FastLine, Color = System.Drawing.Color.OrangeRed };

            int step = Math.Max(1, wf.Length / 2000); // decimate for a responsive plot
            for (int n = 0; n < wf.Length; n += step)
            {
                sI.Points.AddXY(n, wf.I[n]);
                sQ.Points.AddXY(n, wf.Q[n]);
            }
            chartIq.Series.Add(sI);
            chartIq.Series.Add(sQ);

            double[] f, mag;
            Fft.MagnitudeSpectrumDb(wf.I, wf.Q, wf.SampleRateHz, out f, out mag);
            chartSpectrum.Series.Clear();
            var sp = new Series("Spectrum") { ChartType = SeriesChartType.FastLine, Color = System.Drawing.Color.SeaGreen };
            for (int k = 0; k < f.Length; k++)
                sp.Points.AddXY(f[k] / 1e6, mag[k]);
            chartSpectrum.Series.Add(sp);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            CleanupInstrument();
        }

        // ---- helpers ----

        private double FrequencyMultiplier()
        {
            switch ((string)cmbFreqUnit.SelectedItem)
            {
                case "Hz": return 1d;
                case "kHz": return 1e3;
                case "MHz": return 1e6;
                case "GHz": return 1e9;
                default: return 1d;
            }
        }

        private void Guarded(Action action)
        {
            if (_instrument == null || !_instrument.IsConnected)
            {
                Log("Not connected.");
                return;
            }
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log("Error: " + ex.Message);
            }
        }

        private void SetConnectedState(bool connected)
        {
            grpSignal.Enabled = connected;
            grpScpi.Enabled = connected;
            btnConnect.Enabled = !connected;
            btnDisconnect.Enabled = connected;
            cmbInterface.Enabled = !connected;
            cmbResource.Enabled = !connected && (string)cmbInterface.SelectedItem != InterfaceGpib;
            numBoard.Enabled = !connected && (string)cmbInterface.SelectedItem == InterfaceGpib;
            numAddress.Enabled = !connected && (string)cmbInterface.SelectedItem == InterfaceGpib;
            btnRefresh.Enabled = !connected;

            lblStatus.Text = connected ? "Connected" : "Disconnected";
            lblStatus.ForeColor = connected ? System.Drawing.Color.ForestGreen : System.Drawing.Color.Firebrick;

            btnDownloadPlay.Enabled = connected && _waveform != null;
            if (!connected)
            {
                btnArbOff.Enabled = false;
            }
        }

        private void CleanupInstrument()
        {
            try { _instrument?.Dispose(); }
            catch { /* ignore */ }
            _instrument = null;
            _esg = null;
        }

        private void Log(string message)
        {
            string line = string.Format("[{0:HH:mm:ss}] {1}{2}", DateTime.Now, message, Environment.NewLine);
            txtLog.AppendText(line);
        }
    }
}
