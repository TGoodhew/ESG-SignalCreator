using System;
using System.Windows.Forms;
using EsgSignalCreator.Personalities;
using EsgSignalCreator.Timing;

namespace EsgSignalCreator.Ui.Sources
{
    /// <summary>
    /// Shared header for every source-configuration panel (UX brief §4): segment Name, Sample rate
    /// (with unit), Length expressed in Samples / Time / Symbols, Oversampling, and a Calculate
    /// button. The length is solved into a concrete sample count via
    /// <see cref="SampleCountSolver"/> ("state intent, not arithmetic").
    /// </summary>
    public abstract class SourcePanelBase : UserControl, ISignalSourcePanel
    {
        private readonly TextBox _name;
        private readonly NumericUpDown _sampleRate;
        private readonly ComboBox _sampleRateUnit;
        private readonly ComboBox _lengthBasis;
        private readonly NumericUpDown _lengthValue;
        private readonly ComboBox _lengthUnit;
        private readonly NumericUpDown _oversampling;

        /// <summary>Subclass content goes here.</summary>
        protected Panel Body { get; }

        public event EventHandler CalculateRequested;

        protected SourcePanelBase()
        {
            Dock = DockStyle.Fill;

            var header = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(4)
            };

            _name = new TextBox { Width = 110, Text = "SEG1" };
            _sampleRate = new NumericUpDown { Width = 90, DecimalPlaces = 4, Minimum = 0.0001m, Maximum = 1000000000m, Value = 10m };
            _sampleRateUnit = NewCombo(new object[] { "Hz", "kHz", "MHz" }, "MHz");
            _lengthBasis = NewCombo(new object[] { "Samples", "Time", "Symbols" }, "Samples");
            _lengthValue = new NumericUpDown { Width = 100, Minimum = 1, Maximum = 1000000000, Value = 4096 };
            _lengthUnit = NewCombo(new object[] { "ms", "µs", "ns" }, "µs");
            _lengthUnit.Visible = false;
            _oversampling = new NumericUpDown { Width = 60, Minimum = 1, Maximum = 1024, Value = 1 };

            var calculate = new Button { Text = "Calculate", AutoSize = true };
            calculate.Click += (s, e) => OnCalculateRequested();
            _lengthBasis.SelectedIndexChanged += (s, e) => _lengthUnit.Visible = Basis == LengthBasis.Time;

            AddLabeled(header, "Name:", _name);
            AddLabeled(header, "Sample rate:", _sampleRate);
            header.Controls.Add(_sampleRateUnit);
            AddLabeled(header, "Length:", _lengthValue);
            header.Controls.Add(_lengthBasis);
            header.Controls.Add(_lengthUnit);
            AddLabeled(header, "Oversampling:", _oversampling);
            header.Controls.Add(calculate);

            Body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };

            Controls.Add(Body);
            Controls.Add(header);
        }

        // ---- ISignalSourcePanel ----
        public abstract string PersonalityId { get; }
        public abstract IWaveformPersonality BuildPersonality();
        public abstract object GetConfig();
        public abstract void LoadConfig(object cfg);
        public Control AsControl() => this;

        // ---- header values ----
        public string SegmentName => _name.Text.Trim();

        /// <summary>Sample rate in hertz, from the numeric value and its unit.</summary>
        public double SampleRateHz
        {
            get
            {
                double mult;
                switch ((string)_sampleRateUnit.SelectedItem)
                {
                    case "Hz": mult = 1; break;
                    case "kHz": mult = 1e3; break;
                    default: mult = 1e6; break;
                }
                return (double)_sampleRate.Value * mult;
            }
        }

        /// <summary>Symbol rate, for the Symbols length basis. Default 0 (overridden by modulation panels).</summary>
        protected virtual double SymbolRateHz => 0;

        private LengthBasis Basis
        {
            get
            {
                switch ((string)_lengthBasis.SelectedItem)
                {
                    case "Time": return LengthBasis.Time;
                    case "Symbols": return LengthBasis.Symbols;
                    default: return LengthBasis.Samples;
                }
            }
        }

        /// <summary>Resolve the header's length entry to a concrete sample count.</summary>
        public int SolveSampleCount()
        {
            double value = (double)_lengthValue.Value;
            if (Basis == LengthBasis.Time)
            {
                switch ((string)_lengthUnit.SelectedItem)
                {
                    case "ms": value *= 1e-3; break;
                    case "µs": value *= 1e-6; break;
                    case "ns": value *= 1e-9; break;
                }
            }
            double symbolRate = SymbolRateHz;
            return SampleCountSolver.Solve(Basis, value, SampleRateHz, symbolRate);
        }

        /// <summary>Push values into the header (used when loading a saved config).</summary>
        protected void SetHeader(string name, double sampleRateHz, int lengthSamples)
        {
            if (!string.IsNullOrEmpty(name)) _name.Text = name;
            _sampleRateUnit.SelectedItem = "MHz";
            _sampleRate.Value = Clamp((decimal)(sampleRateHz / 1e6), _sampleRate.Minimum, _sampleRate.Maximum);
            _lengthBasis.SelectedItem = "Samples";
            _lengthValue.Value = Clamp(lengthSamples, _lengthValue.Minimum, _lengthValue.Maximum);
        }

        protected virtual void OnCalculateRequested() => CalculateRequested?.Invoke(this, EventArgs.Empty);

        private static decimal Clamp(decimal v, decimal lo, decimal hi) => v < lo ? lo : (v > hi ? hi : v);

        private static ComboBox NewCombo(object[] items, object selected)
        {
            var c = new ComboBox { Width = 70, DropDownStyle = ComboBoxStyle.DropDownList };
            c.Items.AddRange(items);
            c.SelectedItem = selected;
            return c;
        }

        private static void AddLabeled(FlowLayoutPanel host, string label, Control control)
        {
            host.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(8, 6, 2, 0) });
            host.Controls.Add(control);
        }
    }
}
