using System;
using System.Drawing;
using System.Windows.Forms;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Visa;

namespace EsgSignalCreator.Ui.Instrument
{
    /// <summary>
    /// Modal dialog (GitHub issue #8) that lets the user pick a transport — NI-VISA or NI-488.2 / GPIB —
    /// discover and select a resource, connect, and confirm the instrument identity (<c>*IDN?</c>) and
    /// installed options (<c>*OPT?</c>) before the dialog is accepted.
    ///
    /// On a successful Connect the opened <see cref="EsgInstrument"/> is held in <see cref="Connected"/>.
    /// Pressing OK keeps it (and closes with <see cref="DialogResult.OK"/>); pressing Cancel disposes it
    /// and leaves <see cref="Connected"/> null. The UI is built entirely in code (no designer/resx).
    /// </summary>
    public sealed class ConnectionManagerForm : Form
    {
        private readonly RadioButton _visaRadio;
        private readonly RadioButton _gpibRadio;

        // VISA controls
        private readonly ComboBox _visaResource;
        private readonly Button _visaRefresh;

        // GPIB controls
        private readonly NumericUpDown _gpibBoard;
        private readonly NumericUpDown _gpibAddress;
        private readonly ComboBox _gpibFound;
        private readonly Button _gpibRefresh;

        private readonly Button _connectButton;
        private readonly Button _okButton;
        private readonly Button _cancelButton;
        private readonly Label _statusLabel;
        private readonly TextBox _detailsBox;

        /// <summary>
        /// The instrument opened by a successful Connect, or null if nothing connected (or the dialog
        /// was cancelled). Ownership transfers to the caller when the dialog closes with OK.
        /// </summary>
        public EsgInstrument Connected { get; private set; }

        public ConnectionManagerForm()
        {
            Text = "Connection Manager";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(440, 420);

            // --- Interface selection ---------------------------------------------------------
            var interfaceGroup = new GroupBox
            {
                Text = "Interface",
                Location = new Point(12, 12),
                Size = new Size(416, 50)
            };
            _visaRadio = new RadioButton { Text = "VISA", Location = new Point(16, 20), AutoSize = true, Checked = true };
            _gpibRadio = new RadioButton { Text = "GPIB (488.2)", Location = new Point(120, 20), AutoSize = true };
            _visaRadio.CheckedChanged += (s, e) => UpdateInterfaceEnabled();
            _gpibRadio.CheckedChanged += (s, e) => UpdateInterfaceEnabled();
            interfaceGroup.Controls.Add(_visaRadio);
            interfaceGroup.Controls.Add(_gpibRadio);

            // --- VISA group ------------------------------------------------------------------
            var visaGroup = new GroupBox
            {
                Text = "VISA resource",
                Location = new Point(12, 70),
                Size = new Size(416, 80)
            };
            _visaResource = new ComboBox
            {
                Location = new Point(16, 28),
                Size = new Size(290, 23),
                DropDownStyle = ComboBoxStyle.DropDown // free-text entry allowed
            };
            _visaRefresh = new Button { Text = "Refresh", Location = new Point(316, 27), Size = new Size(84, 25) };
            _visaRefresh.Click += (s, e) => RefreshVisa();
            var visaHint = new Label
            {
                Text = "e.g. TCPIP0::192.168.0.10::INSTR  or  GPIB0::19::INSTR",
                Location = new Point(16, 55),
                AutoSize = true,
                ForeColor = SystemColors.GrayText
            };
            visaGroup.Controls.Add(_visaResource);
            visaGroup.Controls.Add(_visaRefresh);
            visaGroup.Controls.Add(visaHint);

            // --- GPIB group ------------------------------------------------------------------
            var gpibGroup = new GroupBox
            {
                Text = "GPIB (NI-488.2)",
                Location = new Point(12, 158),
                Size = new Size(416, 110)
            };
            var boardLabel = new Label { Text = "Board:", Location = new Point(16, 30), AutoSize = true };
            _gpibBoard = new NumericUpDown { Location = new Point(70, 27), Size = new Size(60, 23), Minimum = 0, Maximum = 31, Value = 0 };
            var addrLabel = new Label { Text = "Address:", Location = new Point(150, 30), AutoSize = true };
            _gpibAddress = new NumericUpDown { Location = new Point(210, 27), Size = new Size(60, 23), Minimum = 0, Maximum = 30, Value = 19 };

            var foundLabel = new Label { Text = "Listeners:", Location = new Point(16, 66), AutoSize = true };
            _gpibFound = new ComboBox
            {
                Location = new Point(80, 63),
                Size = new Size(226, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _gpibFound.SelectedIndexChanged += (s, e) => ApplyFoundListener();
            _gpibRefresh = new Button { Text = "Refresh", Location = new Point(316, 62), Size = new Size(84, 25) };
            _gpibRefresh.Click += (s, e) => RefreshGpib();

            gpibGroup.Controls.Add(boardLabel);
            gpibGroup.Controls.Add(_gpibBoard);
            gpibGroup.Controls.Add(addrLabel);
            gpibGroup.Controls.Add(_gpibAddress);
            gpibGroup.Controls.Add(foundLabel);
            gpibGroup.Controls.Add(_gpibFound);
            gpibGroup.Controls.Add(_gpibRefresh);

            // --- Connect + status ------------------------------------------------------------
            _connectButton = new Button { Text = "Connect", Location = new Point(12, 276), Size = new Size(100, 28) };
            _connectButton.Click += (s, e) => DoConnect();

            _statusLabel = new Label
            {
                Text = "Not connected",
                Location = new Point(124, 282),
                AutoSize = true,
                ForeColor = Color.DarkRed,
                Font = new Font(Font, FontStyle.Bold)
            };

            _detailsBox = new TextBox
            {
                Location = new Point(12, 312),
                Size = new Size(416, 60),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = SystemColors.Window
            };

            // --- OK / Cancel -----------------------------------------------------------------
            _okButton = new Button { Text = "OK", Location = new Point(268, 382), Size = new Size(75, 26), Enabled = false };
            _okButton.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            _cancelButton = new Button { Text = "Cancel", Location = new Point(353, 382), Size = new Size(75, 26) };
            _cancelButton.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            AcceptButton = _okButton;
            CancelButton = _cancelButton;

            Controls.Add(interfaceGroup);
            Controls.Add(visaGroup);
            Controls.Add(gpibGroup);
            Controls.Add(_connectButton);
            Controls.Add(_statusLabel);
            Controls.Add(_detailsBox);
            Controls.Add(_okButton);
            Controls.Add(_cancelButton);

            UpdateInterfaceEnabled();
        }

        /// <summary>
        /// Show the dialog modally and return the connected instrument, or null if the user cancelled or
        /// never connected. Convenience wrapper around constructing the form and reading <see cref="Connected"/>.
        /// </summary>
        public static EsgInstrument Connect(IWin32Window owner)
        {
            using (var form = new ConnectionManagerForm())
            {
                return form.ShowDialog(owner) == DialogResult.OK ? form.Connected : null;
            }
        }

        private bool UseVisa => _visaRadio.Checked;

        private void UpdateInterfaceEnabled()
        {
            bool visa = UseVisa;

            _visaResource.Enabled = visa;
            _visaRefresh.Enabled = visa;

            _gpibBoard.Enabled = !visa;
            _gpibAddress.Enabled = !visa;
            _gpibFound.Enabled = !visa;
            _gpibRefresh.Enabled = !visa;
        }

        private void RefreshVisa()
        {
            try
            {
                _visaResource.Items.Clear();
                foreach (string resource in VisaInstrument.FindResources())
                    _visaResource.Items.Add(resource);

                if (_visaResource.Items.Count > 0)
                {
                    _visaResource.SelectedIndex = 0;
                    SetStatus(string.Format("Found {0} VISA resource(s).", _visaResource.Items.Count), Color.Black);
                }
                else
                {
                    SetStatus("No VISA resources found.", Color.DarkRed);
                }
            }
            catch (Exception ex)
            {
                SetStatus("VISA scan failed: " + ex.Message, Color.DarkRed);
            }
        }

        private void RefreshGpib()
        {
            try
            {
                _gpibFound.Items.Clear();
                int board = (int)_gpibBoard.Value;
                foreach (string listener in VisaInstrument.FindResources("GPIB" + board + "::?*INSTR"))
                    _gpibFound.Items.Add(listener);

                if (_gpibFound.Items.Count > 0)
                {
                    _gpibFound.SelectedIndex = 0; // also applies the address via ApplyFoundListener
                    SetStatus(string.Format("Found {0} GPIB listener(s).", _gpibFound.Items.Count), Color.Black);
                }
                else
                {
                    SetStatus("No GPIB listeners found.", Color.DarkRed);
                }
            }
            catch (Exception ex)
            {
                SetStatus("GPIB scan failed: " + ex.Message, Color.DarkRed);
            }
        }

        /// <summary>Parse the selected "GPIBb::addr::INSTR" listener and copy its address into the numeric input.</summary>
        private void ApplyFoundListener()
        {
            string selected = _gpibFound.SelectedItem as string;
            if (string.IsNullOrEmpty(selected)) return;

            // Format: GPIB{board}::{address}::INSTR
            string[] parts = selected.Split(new[] { "::" }, StringSplitOptions.None);
            if (parts.Length >= 2 && byte.TryParse(parts[1], out byte addr) && addr <= _gpibAddress.Maximum)
                _gpibAddress.Value = addr;
        }

        private void DoConnect()
        {
            // Drop any previous connection before opening a new one.
            DisposeConnected();
            _okButton.Enabled = false;
            _detailsBox.Clear();

            try
            {
                var settings = new ConnectionSettings();
                if (UseVisa)
                {
                    string resource = (_visaResource.Text ?? string.Empty).Trim();
                    if (resource.Length == 0)
                    {
                        SetStatus("Enter or select a VISA resource.", Color.DarkRed);
                        return;
                    }
                    settings.Kind = ConnectionKind.Visa;
                    settings.VisaResource = resource;
                }
                else
                {
                    settings.Kind = ConnectionKind.Gpib;
                    settings.GpibBoard = (int)_gpibBoard.Value;
                    settings.GpibAddress = (byte)_gpibAddress.Value;
                }

                var esg = EsgInstrument.Open(settings);
                try
                {
                    InstrumentIdentity idn = esg.Identify();
                    string[] options = esg.Options();

                    Connected = esg;
                    esg = null; // ownership transferred to Connected; don't dispose in finally

                    _detailsBox.Text =
                        "Resource: " + Connected.ResourceName + Environment.NewLine +
                        "*IDN?: " + idn + Environment.NewLine +
                        "*OPT?: " + (options.Length > 0 ? string.Join(", ", options) : "(none)");

                    SetStatus("Connected", Color.Green);
                    _okButton.Enabled = true;
                }
                finally
                {
                    // If Identify()/Options() threw, the half-open instrument is disposed here.
                    esg?.Dispose();
                }
            }
            catch (Exception ex)
            {
                DisposeConnected();
                SetStatus("Connect failed: " + ex.Message, Color.DarkRed);
                _detailsBox.Text = ex.ToString();
            }
        }

        private void SetStatus(string text, Color color)
        {
            _statusLabel.Text = text;
            _statusLabel.ForeColor = color;
        }

        private void DisposeConnected()
        {
            if (Connected != null)
            {
                try { Connected.Dispose(); } catch { /* best effort */ }
                Connected = null;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // If the dialog is closing for any reason other than OK, the caller does not receive the
            // instrument, so dispose it here to avoid leaking the open transport.
            if (DialogResult != DialogResult.OK)
                DisposeConnected();

            base.OnFormClosing(e);
        }
    }
}
