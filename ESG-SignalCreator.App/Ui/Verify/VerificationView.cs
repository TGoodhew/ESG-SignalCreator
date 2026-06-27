using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using EsgSignalCreator.Verify;

namespace EsgSignalCreator.Ui.Verify
{
    /// <summary>
    /// Expected-vs-Measured verification table (E4406A doc §9): shows each metric's expected and
    /// measured value, the delta, the tolerance, and pass/fail, with a summary line. Populated from a
    /// <see cref="VerificationHarness"/> run.
    /// </summary>
    public sealed class VerificationView : UserControl
    {
        private readonly DataGridView _grid;
        private readonly Label _summary;

        public VerificationView()
        {
            _summary = new Label { Dock = DockStyle.Top, AutoSize = false, Height = 26, Padding = new Padding(6, 5, 0, 0), Text = "No verification run yet." };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            _grid.Columns.Add(Col("metric", "Metric"));
            _grid.Columns.Add(Col("expected", "Expected"));
            _grid.Columns.Add(Col("measured", "Measured"));
            _grid.Columns.Add(Col("delta", "Δ"));
            _grid.Columns.Add(Col("tol", "Tolerance"));
            _grid.Columns.Add(Col("result", "Result"));

            Controls.Add(_grid);
            Controls.Add(_summary);
        }

        /// <summary>Populate the table from a verification run (rows colored by pass/fail).</summary>
        public void Show(IReadOnlyList<VerificationResult> results)
        {
            _grid.Rows.Clear();
            if (results == null || results.Count == 0)
            {
                _summary.Text = "No results.";
                return;
            }

            foreach (VerificationResult r in results)
            {
                int idx = _grid.Rows.Add(
                    r.Metric,
                    F(r.Expected) + " " + r.Unit,
                    F(r.Measured) + " " + r.Unit,
                    r.Delta.ToString("+0.###;-0.###", CultureInfo.InvariantCulture),
                    "±" + F(r.Tolerance),
                    r.Pass ? "PASS" : "FAIL");
                DataGridViewCellStyle style = _grid.Rows[idx].DefaultCellStyle;
                style.ForeColor = r.Pass ? Color.ForestGreen : Color.Firebrick;
                if (!r.Pass) style.Font = new Font(_grid.Font, FontStyle.Bold);
            }

            int passed = results.Count(r => r.Pass);
            bool all = passed == results.Count;
            _summary.Text = passed + " / " + results.Count + (all ? " passed — VERIFIED" : " passed — FAILED");
            _summary.ForeColor = all ? Color.ForestGreen : Color.Firebrick;
        }

        private static DataGridViewTextBoxColumn Col(string name, string header) =>
            new DataGridViewTextBoxColumn { Name = name, HeaderText = header };

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
