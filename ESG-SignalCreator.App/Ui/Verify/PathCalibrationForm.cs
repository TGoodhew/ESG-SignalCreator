using System;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using EsgSignalCreator;
using EsgSignalCreator.Verify;
using EsgSignalCreator.Visa;

namespace EsgSignalCreator.Ui.Verify
{
    /// <summary>
    /// Path-calibration wizard (#72): guided one-time CW generate→measure→store-offset. Drives the
    /// ESG to a clean unmodulated carrier at a known level, measures the channel power the E4406A
    /// actually sees, and offers the commanded−measured delta as the inline path-loss correction the
    /// verification harness uses. On OK, <see cref="AppliedPathLossDb"/> holds the value to store.
    /// </summary>
    public sealed class PathCalibrationForm : Form
    {
        private readonly EsgController _esg;
        private readonly VsaInstrument _vsa;
        private readonly RfPathSafety _safety;

        private readonly NumericUpDown _carrierMhz;
        private readonly NumericUpDown _commandedDbm;
        private readonly NumericUpDown _spanMhz;
        private readonly Button _measureBtn;
        private readonly Button _okBtn;
        private readonly Label _result;

        private PathCalibrationResult _last;

        /// <summary>The path loss (dB) the user accepted, or null if cancelled / never measured.</summary>
        public double? AppliedPathLossDb { get; private set; }

        public PathCalibrationForm(EsgController esg, VsaInstrument vsa, RfPathSafety safety)
        {
            _esg = esg ?? throw new ArgumentNullException(nameof(esg));
            _vsa = vsa ?? throw new ArgumentNullException(nameof(vsa));
            _safety = safety ?? new RfPathSafety();

            Text = "Path calibration";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(440, 280);

            var intro = new Label
            {
                Location = new Point(12, 10),
                Size = new Size(416, 56),
                Text = "Drives the ESG to a clean unmodulated carrier at the level below, measures it on " +
                       "the E4406A, and records commanded − measured as the inline path loss. Make sure the " +
                       "analyzer is cabled to the ESG RF output. RF is returned OFF when done."
            };

            double startCarrier;
            try { startCarrier = _esg.GetFrequencyHz() / 1e6; } catch { startCarrier = 1000; }
            if (startCarrier < 1 || startCarrier > 6000) startCarrier = 1000;

            _carrierMhz = Spin(76, "Carrier (MHz):", 1, 6000, (decimal)startCarrier, 3);
            _commandedDbm = Spin(106, "Commanded (dBm):", -130, 20, -10, 1);
            _spanMhz = Spin(136, "Span (MHz):", 0.1m, 100, 5, 1);

            _measureBtn = new Button { Text = "Measure", Location = new Point(150, 170), Size = new Size(90, 26) };
            _measureBtn.Click += (s, e) => RunMeasurement();

            _result = new Label { Location = new Point(12, 206), Size = new Size(416, 30), Text = "Not measured yet." };

            _okBtn = new Button { Text = "Apply", Location = new Point(254, 244), Size = new Size(80, 26), Enabled = false, DialogResult = DialogResult.OK };
            _okBtn.Click += (s, e) => { if (_last != null) AppliedPathLossDb = _last.PathLossDb; };
            var cancel = new Button { Text = "Cancel", Location = new Point(344, 244), Size = new Size(80, 26), DialogResult = DialogResult.Cancel };

            Controls.Add(intro);
            Controls.Add(_measureBtn);
            Controls.Add(_result);
            Controls.Add(_okBtn);
            Controls.Add(cancel);
            AcceptButton = _okBtn;
            CancelButton = cancel;
        }

        private NumericUpDown Spin(int y, string label, decimal min, decimal max, decimal value, int decimals)
        {
            Controls.Add(new Label { Location = new Point(12, y + 3), Size = new Size(130, 20), Text = label });
            var n = new NumericUpDown
            {
                Location = new Point(150, y),
                Size = new Size(140, 22),
                Minimum = min,
                Maximum = max,
                DecimalPlaces = decimals,
                Increment = decimals >= 3 ? 1m : (decimals == 1 ? 0.5m : 1m),
                Value = value < min ? min : (value > max ? max : value)
            };
            Controls.Add(n);
            return n;
        }

        private void RunMeasurement()
        {
            double carrierHz = (double)_carrierMhz.Value * 1e6;
            double commandedDbm = (double)_commandedDbm.Value;
            double spanHz = (double)_spanMhz.Value * 1e6;

            // Protect the analyzer with the *current* safety config before commanding any power.
            string reason;
            if (!PowerSafetyGate.IsSafe(commandedDbm, _safety, out reason))
            {
                _result.ForeColor = Color.Firebrick;
                _result.Text = reason;
                return;
            }

            _measureBtn.Enabled = false;
            Cursor = Cursors.WaitCursor;
            try
            {
                _esg.SetFrequencyHz(carrierHz);
                _esg.SetAmplitudeDbm(commandedDbm);
                _esg.SetArbState(false);      // bare carrier — no ARB
                _esg.SetModulation(false);    // unmodulated CW reference
                _esg.SetRfOutput(true);
                Thread.Sleep(400);            // let the carrier + analyzer settle

                _last = PathCalibration.Measure(_vsa, carrierHz, commandedDbm, spanHz);

                _result.ForeColor = Color.ForestGreen;
                _result.Text = _last.ToString();
                _okBtn.Enabled = true;
            }
            catch (Exception ex)
            {
                _last = null;
                _okBtn.Enabled = false;
                _result.ForeColor = Color.Firebrick;
                _result.Text = "Measurement failed: " + ex.Message;
            }
            finally
            {
                try { _esg.SetRfOutput(false); } catch { /* leave in whatever state we can */ }
                Cursor = Cursors.Default;
                _measureBtn.Enabled = true;
            }
        }
    }
}
