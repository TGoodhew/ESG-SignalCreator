using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using EsgSignalCreator.Validation;

namespace EsgSignalCreator.Ui
{
    /// <summary>
    /// A dockable notifications panel that lists <see cref="ValidationResult"/> findings in a
    /// Details-view <see cref="ListView"/> (Severity, Message, Field, Time). Row text is colored by
    /// severity, and double-clicking a row raises <see cref="JumpToFieldRequested"/> with the item's
    /// <see cref="ValidationResult.Field"/> so the host can focus the offending input control.
    /// Built entirely in code (no designer / resx).
    /// </summary>
    public sealed class NotificationsDock : UserControl
    {
        private static readonly Color ErrorColor = Color.Red;
        private static readonly Color WarningColor = Color.DarkGoldenrod;
        private static readonly Color InfoColor = Color.Gray;

        private readonly ListView _list;

        /// <summary>
        /// Raised with the <see cref="ValidationResult.Field"/> of the double-clicked row so the host
        /// can focus / highlight the offending field. Not raised when the row has no associated field.
        /// </summary>
        public event EventHandler<string> JumpToFieldRequested;

        public NotificationsDock()
        {
            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                UseCompatibleStateImageBehavior = false
            };
            _list.Columns.Add("Severity", 80, HorizontalAlignment.Left);
            _list.Columns.Add("Message", 360, HorizontalAlignment.Left);
            _list.Columns.Add("Field", 120, HorizontalAlignment.Left);
            _list.Columns.Add("Time", 90, HorizontalAlignment.Left);
            _list.DoubleClick += OnListDoubleClick;

            Controls.Add(_list);
        }

        /// <summary>Clears the list and repopulates it from <paramref name="results"/>.</summary>
        public void Show(IEnumerable<ValidationResult> results)
        {
            _list.BeginUpdate();
            try
            {
                _list.Items.Clear();
                if (results != null)
                {
                    foreach (ValidationResult result in results)
                        AddItem(result);
                }
            }
            finally
            {
                _list.EndUpdate();
            }
        }

        /// <summary>Appends a single finding to the list without clearing existing rows.</summary>
        public void Append(ValidationResult result)
        {
            if (result == null) return;
            AddItem(result);
        }

        /// <summary>Removes all findings from the list.</summary>
        public void Clear()
        {
            _list.Items.Clear();
        }

        private void AddItem(ValidationResult result)
        {
            if (result == null) return;

            var item = new ListViewItem(result.Severity.ToString())
            {
                ForeColor = ColorFor(result.Severity),
                Tag = result.Field
            };
            item.SubItems.Add(result.Message ?? string.Empty);
            item.SubItems.Add(result.Field ?? string.Empty);
            item.SubItems.Add(DateTime.Now.ToString("HH:mm:ss"));
            _list.Items.Add(item);
        }

        private static Color ColorFor(ValidationSeverity severity)
        {
            switch (severity)
            {
                case ValidationSeverity.Error: return ErrorColor;
                case ValidationSeverity.Warning: return WarningColor;
                default: return InfoColor;
            }
        }

        private void OnListDoubleClick(object sender, EventArgs e)
        {
            if (_list.SelectedItems.Count == 0) return;
            var field = _list.SelectedItems[0].Tag as string;
            if (string.IsNullOrEmpty(field)) return;
            JumpToFieldRequested?.Invoke(this, field);
        }
    }
}
