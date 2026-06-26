using System;
using System.Windows.Forms;
using EsgSignalCreator.Instruments;

namespace EsgSignalCreator.Ui.Instrument
{
    /// <summary>
    /// A raw-SCPI console: a timestamped log of bus traffic plus a command entry box with
    /// Send / Query / Clear. Lets the operator poke the instrument directly (issue #10).
    /// Attach an <see cref="IInstrument"/> to enable I/O; pass <c>null</c> to disable Send/Query.
    /// </summary>
    public sealed class ScpiConsolePanel : UserControl
    {
        private readonly TextBox _log;
        private readonly TextBox _entry;
        private readonly Button _send;
        private readonly Button _query;
        private readonly Button _clear;
        private IInstrument _io;

        public ScpiConsolePanel()
        {
            _log = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new System.Drawing.Font("Consolas", 9f),
                BackColor = System.Drawing.Color.White
            };

            var bottom = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(2)
            };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _entry = new TextBox { Dock = DockStyle.Fill, Font = new System.Drawing.Font("Consolas", 9f) };
            _entry.KeyDown += OnEntryKeyDown;

            _send = new Button { Text = "Send", AutoSize = true };
            _send.Click += (s, e) => DoSend();

            _query = new Button { Text = "Query", AutoSize = true };
            _query.Click += (s, e) => DoQuery();

            _clear = new Button { Text = "Clear", AutoSize = true };
            _clear.Click += (s, e) => _log.Clear();

            bottom.Controls.Add(_entry, 0, 0);
            bottom.Controls.Add(_send, 1, 0);
            bottom.Controls.Add(_query, 2, 0);
            bottom.Controls.Add(_clear, 3, 0);

            // Add fill control first, then dock the bottom strip below it.
            Controls.Add(_log);
            Controls.Add(bottom);

            UpdateEnabled();
        }

        /// <summary>
        /// Bind the console to a transport. Pass <c>null</c> to detach and disable Send/Query.
        /// </summary>
        public void Attach(IInstrument io)
        {
            _io = io;
            if (InvokeRequired) BeginInvoke((Action)UpdateEnabled);
            else UpdateEnabled();
        }

        /// <summary>The full log contents (newline-separated timestamped lines).</summary>
        public string LogText
        {
            get { return _log.Text; }
        }

        /// <summary>
        /// Append a timestamped line <c>[HH:mm:ss] message</c> to the log. Safe to call from any
        /// thread; marshals to the UI thread when needed.
        /// </summary>
        public void Log(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke((Action<string>)Log, message);
                return;
            }
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + (message ?? string.Empty);
            _log.AppendText(line + Environment.NewLine);
        }

        private void OnEntryKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true; // avoid the WinForms "ding"
                DoSend();
            }
        }

        private void DoSend()
        {
            string cmd = _entry.Text;
            if (string.IsNullOrWhiteSpace(cmd)) return;
            IInstrument io = _io;
            if (io == null) { Log("Error: no instrument attached"); return; }

            Log("> " + cmd);
            try
            {
                io.Write(cmd);
                _entry.Clear();
            }
            catch (Exception ex)
            {
                Log("Error: " + ex.Message);
            }
        }

        private void DoQuery()
        {
            string cmd = _entry.Text;
            if (string.IsNullOrWhiteSpace(cmd)) return;
            IInstrument io = _io;
            if (io == null) { Log("Error: no instrument attached"); return; }

            Log("> " + cmd);
            try
            {
                string response = io.Query(cmd);
                Log("< " + (response ?? string.Empty).TrimEnd('\r', '\n'));
                _entry.Clear();
            }
            catch (Exception ex)
            {
                Log("Error: " + ex.Message);
            }
        }

        private void UpdateEnabled()
        {
            bool on = _io != null;
            _send.Enabled = on;
            _query.Enabled = on;
        }
    }
}
