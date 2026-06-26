using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;
using EsgSignalCreator.Sequencing;

namespace EsgSignalCreator.Ui.Sequencing
{
    /// <summary>
    /// The sequencer (UX brief §5): a spreadsheet table of <see cref="SequenceStep"/>s for everyone,
    /// a power-user script view over the same model (round-tripped via <see cref="SequenceScript"/>),
    /// and a batch-compile mini-tool. Two editors, one model.
    /// </summary>
    public sealed class SequencePanel : UserControl
    {
        private readonly BindingList<SequenceStep> _steps = new BindingList<SequenceStep>();
        private readonly DataGridView _grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = true };
        private readonly TextBox _script = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new System.Drawing.Font("Consolas", 9f) };
        private readonly Label _batchInfo = new Label { AutoSize = true, Margin = new Padding(6, 6, 0, 0) };

        public SequencePanel()
        {
            BuildColumns();
            _grid.DataSource = _steps;

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            split.Panel1.Controls.Add(_grid);
            split.Panel2.Controls.Add(_script);

            // table toolbar
            var tableBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            tableBar.Controls.Add(MakeButton("Add step", (s, e) => _steps.Add(new SequenceStep { Waveform = "SEG1" })));
            tableBar.Controls.Add(MakeButton("Remove", (s, e) => RemoveSelected()));
            tableBar.Controls.Add(MakeButton("Table → Script", (s, e) => _script.Text = SequenceScript.Format(ToSequence())));
            tableBar.Controls.Add(MakeButton("Script → Table", (s, e) => FromScript()));
            tableBar.Controls.Add(MakeButton("Play order", (s, e) => ShowPlayOrder()));

            // batch-compile mini panel
            var batchBar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
            var param = new TextBox { Width = 90, Text = "cn" };
            var lo = NumericBox(0); var hi = NumericBox(20); var step = NumericBox(5);
            batchBar.Controls.Add(new Label { Text = "Batch:", AutoSize = true, Margin = new Padding(2, 6, 2, 0) });
            batchBar.Controls.Add(param);
            batchBar.Controls.Add(new Label { Text = "low", AutoSize = true, Margin = new Padding(6, 6, 0, 0) }); batchBar.Controls.Add(lo);
            batchBar.Controls.Add(new Label { Text = "high", AutoSize = true, Margin = new Padding(6, 6, 0, 0) }); batchBar.Controls.Add(hi);
            batchBar.Controls.Add(new Label { Text = "step", AutoSize = true, Margin = new Padding(6, 6, 0, 0) }); batchBar.Controls.Add(step);
            batchBar.Controls.Add(MakeButton("Preview", (s, e) =>
            {
                var pts = BatchCompiler.Expand(param.Text, (double)lo.Value, (double)hi.Value, (double)step.Value);
                _batchInfo.Text = pts.Count + " point(s): " + string.Join(", ", System.Linq.Enumerable.Select(pts, p => p.Name));
            }));
            batchBar.Controls.Add(_batchInfo);

            var host = new Panel { Dock = DockStyle.Fill };
            host.Controls.Add(split);
            host.Controls.Add(tableBar);
            Controls.Add(host);
            Controls.Add(batchBar);
        }

        /// <summary>The current sequence (table model).</summary>
        public Sequence ToSequence()
        {
            var seq = new Sequence();
            foreach (SequenceStep s in _steps) seq.Steps.Add(s);
            return seq;
        }

        public void LoadSequence(Sequence seq)
        {
            _steps.Clear();
            if (seq != null) foreach (SequenceStep s in seq.Steps) _steps.Add(s);
        }

        private void FromScript()
        {
            try { LoadSequence(SequenceScript.Parse(_script.Text)); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Script error", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void ShowPlayOrder()
        {
            var order = ToSequence().CompilePlayOrder(2000);
            MessageBox.Show(string.Join(" → ", order), "Play order (" + order.Count + ")");
        }

        private void RemoveSelected()
        {
            if (_grid.CurrentRow != null && _grid.CurrentRow.DataBoundItem is SequenceStep s) _steps.Remove(s);
        }

        private void BuildColumns()
        {
            _grid.Columns.Add(TextCol("Waveform", "Waveform"));
            _grid.Columns.Add(TextCol("Repeat", "Repeat"));
            _grid.Columns.Add(TextCol("PowerDb", "Power dB"));
            var wait = new DataGridViewComboBoxColumn { DataPropertyName = "Wait", HeaderText = "Wait", DataSource = Enum.GetValues(typeof(WaitMode)) };
            _grid.Columns.Add(wait);
            _grid.Columns.Add(TextCol("GoTo", "Go-To"));
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "MarkerEnabled", HeaderText = "Marker" });
            _grid.Columns.Add(TextCol("IdleSamples", "Idle"));
        }

        private static DataGridViewTextBoxColumn TextCol(string prop, string header) =>
            new DataGridViewTextBoxColumn { DataPropertyName = prop, HeaderText = header };

        private static Button MakeButton(string text, EventHandler onClick)
        {
            var b = new Button { Text = text, AutoSize = true };
            b.Click += onClick;
            return b;
        }

        private static NumericUpDown NumericBox(decimal value) =>
            new NumericUpDown { Width = 70, Minimum = -1000000, Maximum = 1000000, DecimalPlaces = 2, Value = value };
    }
}
