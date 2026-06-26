using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using EsgSignalCreator.Markers;

namespace EsgSignalCreator.Ui.Markers
{
    /// <summary>
    /// A code-built (no designer/resx) <see cref="UserControl"/> for editing the per-sample
    /// marker stream of an ARB waveform.
    /// </summary>
    /// <remarks>
    /// Markers are a single bit per sample that travel <em>embedded in the waveform</em> (one
    /// bit per I/Q sample, asserted while the marker is on). They are distinct from "flags",
    /// which are separate per-step indicators carried alongside a sequence rather than inside
    /// the waveform samples — this editor handles markers only, not flags.
    /// <para>
    /// The user specifies a waveform length and a list of on/off spans, or uses one of the
    /// presets ("At start", "Every N…", "Range on/off"). Pressing <c>Build</c> produces the
    /// marker <see cref="byte"/>[] via <see cref="MarkerBuilder"/>, publishes it through
    /// <see cref="Markers"/>, and raises <see cref="MarkersChanged"/>.
    /// </para>
    /// </remarks>
    public sealed class MarkersEditorPanel : UserControl
    {
        private readonly NumericUpDown _length;
        private readonly NumericUpDown _everyN;
        private readonly NumericUpDown _spanStart;
        private readonly NumericUpDown _spanStop;
        private readonly ListBox _spans;
        private readonly Button _addSpan;
        private readonly Button _removeSpan;
        private readonly Button _presetAtStart;
        private readonly Button _presetEveryN;
        private readonly Button _presetRange;
        private readonly Button _build;
        private readonly Label _status;

        private readonly List<MarkerSpan> _spanList = new List<MarkerSpan>();

        private byte[] _markers = new byte[0];

        /// <summary>
        /// The most recently built marker stream (one byte per sample, value 1 = marker on).
        /// Never <c>null</c>; empty until <c>Build</c> (or a preset) has run.
        /// </summary>
        public byte[] Markers
        {
            get { return _markers; }
        }

        /// <summary>Raised whenever <see cref="Markers"/> is rebuilt.</summary>
        public event EventHandler MarkersChanged;

        public MarkersEditorPanel()
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

            _length = NewNumeric(1m, 1000000000m, 1024m, 0);
            _everyN = NewNumeric(1m, 1000000000m, 100m, 0);
            _spanStart = NewNumeric(0m, 1000000000m, 0m, 0);
            _spanStop = NewNumeric(0m, 1000000000m, 0m, 0);

            AddRow(layout, "Length (samples):", _length, null);
            AddRow(layout, "Span start:", _spanStart, _spanStop);

            // Add / remove spans.
            _addSpan = new Button { Text = "Add span", AutoSize = true };
            _removeSpan = new Button { Text = "Remove span", AutoSize = true };
            _addSpan.Click += (s, e) => OnAddSpan();
            _removeSpan.Click += (s, e) => OnRemoveSpan();
            var spanButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(4)
            };
            spanButtons.Controls.Add(_addSpan);
            spanButtons.Controls.Add(_removeSpan);

            _spans = new ListBox
            {
                Dock = DockStyle.Top,
                Height = 100,
                IntegralHeight = false
            };

            // Presets.
            _presetAtStart = new Button { Text = "At start", AutoSize = true };
            _presetEveryN = new Button { Text = "Every N…", AutoSize = true };
            _presetRange = new Button { Text = "Range on/off", AutoSize = true };
            _presetAtStart.Click += (s, e) => OnPresetAtStart();
            _presetEveryN.Click += (s, e) => OnPresetEveryN();
            _presetRange.Click += (s, e) => OnPresetRange();

            var presets = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(4)
            };
            presets.Controls.Add(new Label { Text = "Presets:", AutoSize = true, Padding = new Padding(0, 6, 4, 0) });
            presets.Controls.Add(_presetAtStart);
            presets.Controls.Add(_presetEveryN);
            presets.Controls.Add(new Label { Text = "N:", AutoSize = true, Padding = new Padding(8, 6, 2, 0) });
            presets.Controls.Add(_everyN);
            presets.Controls.Add(_presetRange);

            _build = new Button { Text = "Build", AutoSize = true };
            _build.Click += (s, e) => OnBuild();
            var buildBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(4)
            };
            buildBar.Controls.Add(_build);

            _status = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 24,
                Padding = new Padding(6, 4, 4, 4),
                Text = "No markers built."
            };

            // Docked controls are added in reverse visual order (last added = topmost).
            Controls.Add(_status);
            Controls.Add(buildBar);
            Controls.Add(presets);
            Controls.Add(_spans);
            Controls.Add(spanButtons);
            Controls.Add(layout);
        }

        private void OnAddSpan()
        {
            var span = new MarkerSpan((int)_spanStart.Value, (int)_spanStop.Value);
            _spanList.Add(span);
            _spans.Items.Add(Describe(span));
        }

        private void OnRemoveSpan()
        {
            int index = _spans.SelectedIndex;
            if (index < 0)
                return;
            _spanList.RemoveAt(index);
            _spans.Items.RemoveAt(index);
        }

        private void OnPresetAtStart()
        {
            Publish(MarkerBuilder.AtStart(Length), "At start");
        }

        private void OnPresetEveryN()
        {
            int n = (int)_everyN.Value;
            Publish(MarkerBuilder.EveryN(Length, n), "Every " + n.ToString(CultureInfo.InvariantCulture));
        }

        private void OnPresetRange()
        {
            int start = (int)_spanStart.Value;
            int stop = (int)_spanStop.Value;
            Publish(MarkerBuilder.RangeOnOff(Length, start, stop),
                "Range " + start.ToString(CultureInfo.InvariantCulture) + "–" + stop.ToString(CultureInfo.InvariantCulture));
        }

        private void OnBuild()
        {
            Publish(MarkerBuilder.FromSpans(Length, _spanList.ToArray()),
                _spanList.Count.ToString(CultureInfo.InvariantCulture) + " span(s)");
        }

        private int Length
        {
            get { return (int)_length.Value; }
        }

        private void Publish(byte[] markers, string detail)
        {
            _markers = markers ?? new byte[0];
            int on = 0;
            for (int i = 0; i < _markers.Length; i++)
                if (_markers[i] != 0)
                    on++;
            _status.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Built {0} samples, {1} marked ({2}).",
                _markers.Length, on, detail);
            MarkersChanged?.Invoke(this, EventArgs.Empty);
        }

        private static string Describe(MarkerSpan span)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "[{0}, {1}]",
                span.Start, span.Stop);
        }

        private static NumericUpDown NewNumeric(decimal min, decimal max, decimal value, int decimals)
        {
            return new NumericUpDown
            {
                Width = 120,
                DecimalPlaces = decimals,
                Minimum = min,
                Maximum = max,
                Value = value
            };
        }

        private static void AddRow(TableLayoutPanel layout, string label, Control first, Control second)
        {
            int row = layout.RowCount;
            layout.RowCount = row + 1;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(0, 6, 4, 0)
            }, 0, row);

            if (first != null)
                layout.Controls.Add(first, 1, row);
            if (second != null)
                layout.Controls.Add(second, 2, row);
        }
    }
}
