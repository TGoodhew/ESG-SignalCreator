using System;
using System.Globalization;
using System.Windows.Forms;

namespace EsgSignalCreator.Ui.Instrument
{
    /// <summary>
    /// A code-built (no designer/resx) <see cref="UserControl"/> that exposes the core
    /// instrument settings of an ESG-series generator: RF carrier frequency, amplitude,
    /// RF-output and modulation state, plus the dual-ARB sample clock and runtime scaling.
    /// Attach an <see cref="EsgController"/> via <see cref="Attach"/>; passing <c>null</c>
    /// disables the controls. All instrument writes are guarded and never throw to the UI —
    /// any error is shown in the status label instead.
    /// </summary>
    public sealed class InstrumentSettingsPanel : UserControl
    {
        private readonly NumericUpDown _frequency;
        private readonly ComboBox _frequencyUnit;
        private readonly NumericUpDown _amplitude;
        private readonly CheckBox _rfOutput;
        private readonly CheckBox _modulation;
        private readonly NumericUpDown _sampleClock;
        private readonly ComboBox _sampleClockUnit;
        private readonly NumericUpDown _runtimeScaling;
        private readonly Button _apply;
        private readonly Button _readBack;
        private readonly Label _status;

        private EsgController _esg;

        public InstrumentSettingsPanel()
        {
            Dock = DockStyle.Fill;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                Padding = new Padding(4)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            // Frequency value + unit.
            _frequency = new NumericUpDown
            {
                Width = 120,
                DecimalPlaces = 6,
                Minimum = 0m,
                Maximum = 1000000000000m,
                Value = 1000m
            };
            _frequencyUnit = NewCombo(new object[] { "Hz", "kHz", "MHz", "GHz" }, "MHz");

            // Amplitude (dBm).
            _amplitude = new NumericUpDown
            {
                Width = 90,
                DecimalPlaces = 2,
                Minimum = -200m,
                Maximum = 30m,
                Value = -10m
            };

            // RF output / modulation.
            _rfOutput = new CheckBox { Text = "RF output on", AutoSize = true };
            _modulation = new CheckBox { Text = "Modulation on", AutoSize = true };

            // ARB sample clock value + unit.
            _sampleClock = new NumericUpDown
            {
                Width = 120,
                DecimalPlaces = 4,
                Minimum = 0.0001m,
                Maximum = 1000000000m,
                Value = 10m
            };
            _sampleClockUnit = NewCombo(new object[] { "Hz", "kHz", "MHz" }, "MHz");

            // ARB runtime scaling (%).
            _runtimeScaling = new NumericUpDown
            {
                Width = 70,
                DecimalPlaces = 1,
                Minimum = 0m,
                Maximum = 100m,
                Value = 70m
            };

            AddRow(layout, "Frequency:", _frequency, _frequencyUnit);
            AddRow(layout, "Amplitude (dBm):", _amplitude, null);
            AddRow(layout, "", _rfOutput, null);
            AddRow(layout, "", _modulation, null);
            AddRow(layout, "ARB sample clock:", _sampleClock, _sampleClockUnit);
            AddRow(layout, "ARB runtime scaling (%):", _runtimeScaling, null);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(4)
            };
            _apply = new Button { Text = "Apply", AutoSize = true };
            _readBack = new Button { Text = "Read back", AutoSize = true };
            _apply.Click += (s, e) => OnApply();
            _readBack.Click += (s, e) => OnReadBack();
            buttons.Controls.Add(_apply);
            buttons.Controls.Add(_readBack);

            _status = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 24,
                Padding = new Padding(6, 4, 4, 4),
                Text = "Not attached."
            };

            // Docked controls are added in reverse visual order (last added = topmost).
            Controls.Add(_status);
            Controls.Add(buttons);
            Controls.Add(layout);

            SetControlsEnabled(false);
        }

        /// <summary>
        /// Attach the controller used for instrument writes/reads. Passing <c>null</c>
        /// disables all controls; the panel performs no instrument I/O while detached.
        /// </summary>
        public void Attach(EsgController esg)
        {
            _esg = esg;
            bool attached = esg != null;
            SetControlsEnabled(attached);
            _status.Text = attached ? "Attached." : "Not attached.";
        }

        private void OnApply()
        {
            if (_esg == null)
            {
                _status.Text = "Not attached.";
                return;
            }

            try
            {
                _esg.SetFrequencyHz(FrequencyHz);
                _esg.SetAmplitudeDbm((double)_amplitude.Value);
                _esg.SetRfOutput(_rfOutput.Checked);
                _esg.SetModulation(_modulation.Checked);
                _esg.SetSampleClockHz(SampleClockHz);
                _esg.SetRuntimeScaling((double)_runtimeScaling.Value);

                string err = SafeGetError();
                _status.Text = "Applied. " + err;
            }
            catch (Exception ex)
            {
                _status.Text = "Apply failed: " + ex.Message;
            }
        }

        private void OnReadBack()
        {
            if (_esg == null)
            {
                _status.Text = "Not attached.";
                return;
            }

            try
            {
                double hz = _esg.GetFrequencyHz();
                double dbm = _esg.GetAmplitudeDbm();
                SetFrequencyHz(hz);
                _amplitude.Value = Clamp((decimal)dbm, _amplitude.Minimum, _amplitude.Maximum);

                string err = SafeGetError();
                _status.Text = "Read back. " + err;
            }
            catch (Exception ex)
            {
                _status.Text = "Read back failed: " + ex.Message;
            }
        }

        private string SafeGetError()
        {
            try
            {
                string err = _esg.GetError();
                return string.IsNullOrWhiteSpace(err) ? string.Empty : ("Error: " + err.Trim());
            }
            catch (Exception ex)
            {
                return "Error query failed: " + ex.Message;
            }
        }

        /// <summary>Carrier frequency in hertz, from the numeric value and its unit.</summary>
        /// <summary>The carrier frequency currently entered in the panel, in hertz. Used by the
        /// Calculate-time validation to check the intended carrier against the instrument's range.</summary>
        public double FrequencyHz
        {
            get
            {
                double mult;
                switch ((string)_frequencyUnit.SelectedItem)
                {
                    case "Hz": mult = 1; break;
                    case "kHz": mult = 1e3; break;
                    case "MHz": mult = 1e6; break;
                    default: mult = 1e9; break;
                }
                return (double)_frequency.Value * mult;
            }
        }

        /// <summary>ARB sample clock in hertz, from the numeric value and its unit.</summary>
        private double SampleClockHz
        {
            get
            {
                double mult;
                switch ((string)_sampleClockUnit.SelectedItem)
                {
                    case "Hz": mult = 1; break;
                    case "kHz": mult = 1e3; break;
                    default: mult = 1e6; break;
                }
                return (double)_sampleClock.Value * mult;
            }
        }

        /// <summary>Display a frequency in hertz, choosing a sensible unit for the magnitude.</summary>
        private void SetFrequencyHz(double hertz)
        {
            string unit;
            double scaled;
            double abs = Math.Abs(hertz);
            if (abs >= 1e9) { unit = "GHz"; scaled = hertz / 1e9; }
            else if (abs >= 1e6) { unit = "MHz"; scaled = hertz / 1e6; }
            else if (abs >= 1e3) { unit = "kHz"; scaled = hertz / 1e3; }
            else { unit = "Hz"; scaled = hertz; }

            _frequencyUnit.SelectedItem = unit;
            _frequency.Value = Clamp((decimal)scaled, _frequency.Minimum, _frequency.Maximum);
        }

        private void SetControlsEnabled(bool enabled)
        {
            _frequency.Enabled = enabled;
            _frequencyUnit.Enabled = enabled;
            _amplitude.Enabled = enabled;
            _rfOutput.Enabled = enabled;
            _modulation.Enabled = enabled;
            _sampleClock.Enabled = enabled;
            _sampleClockUnit.Enabled = enabled;
            _runtimeScaling.Enabled = enabled;
            _apply.Enabled = enabled;
            _readBack.Enabled = enabled;
        }

        private static decimal Clamp(decimal v, decimal lo, decimal hi) => v < lo ? lo : (v > hi ? hi : v);

        private static ComboBox NewCombo(object[] items, object selected)
        {
            var c = new ComboBox { Width = 70, DropDownStyle = ComboBoxStyle.DropDownList };
            c.Items.AddRange(items);
            c.SelectedItem = selected;
            return c;
        }

        private static void AddRow(TableLayoutPanel host, string label, Control value, Control unit)
        {
            int row = host.RowCount;
            host.RowCount = row + 1;
            host.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            host.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(4, 6, 4, 0) }, 0, row);
            host.Controls.Add(value, 1, row);
            if (unit != null)
                host.Controls.Add(unit, 2, row);
        }
    }
}
